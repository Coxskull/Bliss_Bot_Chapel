using Bliss.Domain.Financials;
using Npgsql;

namespace Bliss.Infrastructure.Financials;

// Inactive foundation: no public endpoints or application DI registration yet.
public sealed class PostgresJournalStore(NpgsqlDataSource dataSource)
{
    public Task<Guid> Append(FinancialJournal journal, CancellationToken cancellationToken = default) => AppendCore(journal, null, cancellationToken);

    public Task<Guid> AppendVerifiedPayment(FinancialJournal journal, VerifiedSandboxPayment payment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payment.EventId) || payment.EventId.Length > 120 || !payment.EventId.StartsWith("evt_", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(payment.PaymentIntentId) || payment.Currency != journal.Currency || payment.AmountMinor <= 0 ||
            journal.Lines.Where(x => x.Side == JournalSide.Debit).Sum(x => x.MinorUnits) != payment.AmountMinor || journal.ReversesId is not null)
            throw new ArgumentException("Verified payment and journal must agree.");
        return AppendCore(journal, payment, cancellationToken);
    }

    private async Task<Guid> AppendCore(FinancialJournal journal, VerifiedSandboxPayment? payment, CancellationToken cancellationToken)
    {
        await using var connection=await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction=await connection.BeginTransactionAsync(cancellationToken);
        var id=await AppendWithin(connection,transaction,journal,payment,cancellationToken);
        await transaction.CommitAsync(cancellationToken);return id;
    }
    internal async Task<Guid> AppendWithin(NpgsqlConnection connection,NpgsqlTransaction transaction,FinancialJournal journal,VerifiedSandboxPayment? payment,CancellationToken cancellationToken)
    {
        string? receiptFingerprint = null;
        if (payment is not null)
        {
            receiptFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { journal.Fingerprint, payment })));
            await using var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", connection, transaction);
            gate.Parameters.AddWithValue("key", "stripe-sandbox:" + payment.EventId);
            await gate.ExecuteNonQueryAsync(cancellationToken);
            await using var receipt = new NpgsqlCommand("SELECT journal_id,fingerprint FROM financials.stripe_receipts WHERE event_id=@e", connection, transaction);
            receipt.Parameters.AddWithValue("e", payment.EventId);
            await using var reader = await receipt.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetString(1) != receiptFingerprint) throw new InvalidOperationException("Event replay conflicts with original financial evidence.");
                var storedId=reader.GetGuid(0);await reader.DisposeAsync();return storedId;
            }
        }

        // Scope-qualified unique key arbitrates concurrent inserts; retries must match content.
        await using var insert = new NpgsqlCommand("""
            INSERT INTO financials.journals(id, merchant_id, module, campaign_id, currency, retry_key, evidence, fingerprint, reverses_id)
            VALUES (@id,@merchant,@module,@campaign,@currency,@key,@evidence,@fingerprint,@reverses)
            ON CONFLICT DO NOTHING RETURNING id
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", journal.Id);
        insert.Parameters.AddWithValue("merchant", journal.Scope.MerchantId);
        insert.Parameters.AddWithValue("module", journal.Scope.Module.ToString());
        insert.Parameters.AddWithValue("campaign", journal.Scope.CampaignId);
        insert.Parameters.AddWithValue("currency", journal.Currency);
        insert.Parameters.AddWithValue("key", journal.IdempotencyKey);
        insert.Parameters.AddWithValue("evidence", journal.EvidenceReference);
        insert.Parameters.AddWithValue("fingerprint", journal.Fingerprint);
        insert.Parameters.AddWithValue("reverses", NpgsqlTypes.NpgsqlDbType.Uuid, (object?)journal.ReversesId ?? DBNull.Value);
        var created = await insert.ExecuteScalarAsync(cancellationToken);
        if (created is null)
        {
            await using var existing = new NpgsqlCommand("""
                SELECT id,fingerprint FROM financials.journals WHERE merchant_id=@m AND module=@o
                AND campaign_id=@c AND currency=@u AND retry_key=@k
                """, connection, transaction);
            existing.Parameters.AddWithValue("m", journal.Scope.MerchantId);
            existing.Parameters.AddWithValue("o", journal.Scope.Module.ToString());
            existing.Parameters.AddWithValue("c", journal.Scope.CampaignId);
            existing.Parameters.AddWithValue("u", journal.Currency);
            existing.Parameters.AddWithValue("k", journal.IdempotencyKey);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || reader.GetString(1) != journal.Fingerprint)
                throw new InvalidOperationException("Idempotency key conflicts with immutable journal content.");
            var id = reader.GetGuid(0);
            await reader.DisposeAsync();
            await RecordReceipt(id);  return id;
        }
        for (var i = 0; i < journal.Lines.Length; i++)
        {
            var line = journal.Lines[i];
            await using var command = new NpgsqlCommand("""
                INSERT INTO financials.lines(journal_id,ordinal,account_id,owner_id,role,side,minor_units)
                VALUES (@j,@n,@a,@o,@r,@s,@v)
                """, connection, transaction);
            command.Parameters.AddWithValue("j", journal.Id); command.Parameters.AddWithValue("n", i);
            command.Parameters.AddWithValue("a", line.AccountId); command.Parameters.AddWithValue("o", line.OwnerId);
            command.Parameters.AddWithValue("r", line.Role); command.Parameters.AddWithValue("s", line.Side.ToString());
            command.Parameters.AddWithValue("v", line.MinorUnits); await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await RecordReceipt(journal.Id);  return journal.Id;

        async Task RecordReceipt(Guid journalId)
        {
            if (payment is null) return;
            await using var command = new NpgsqlCommand("INSERT INTO financials.stripe_receipts(event_id,payment_intent_id,journal_id,fingerprint) VALUES(@e,@p,@j,@f)", connection, transaction);
            command.Parameters.AddWithValue("e", payment.EventId);command.Parameters.AddWithValue("p", payment.PaymentIntentId);
            command.Parameters.AddWithValue("j",journalId);command.Parameters.AddWithValue("f",receiptFingerprint!);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
