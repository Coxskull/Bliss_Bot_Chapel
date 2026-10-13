using Bliss.Domain.Financials;
using Npgsql;

namespace Bliss.Infrastructure.Financials;

// Inactive foundation: no public endpoints or application DI registration yet.
public sealed class PostgresJournalStore(NpgsqlDataSource dataSource)
{
    public async Task<Guid> Append(FinancialJournal journal, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
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
            await transaction.CommitAsync(cancellationToken); return id;
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
        await transaction.CommitAsync(cancellationToken); return journal.Id;
    }
}
