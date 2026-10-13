using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bliss.Domain.Financials;

public enum FinancialModule { BlissBot, CreatorAcademy, WeddingPlanner }
public enum JournalSide { Debit, Credit }
public sealed record FinancialScope(Guid MerchantId, FinancialModule Module, Guid CampaignId);
public sealed record JournalLine(Guid AccountId, Guid OwnerId, string Role, JournalSide Side, long MinorUnits);

// Construction is confined to a trusted application boundary, never a browser DTO.
public sealed class FinancialJournal
{
    public Guid Id { get; }
    public FinancialScope Scope { get; }
    public string Currency { get; }
    public string IdempotencyKey { get; }
    public string EvidenceReference { get; }
    public ImmutableArray<JournalLine> Lines { get; }
    public Guid? ReversesId { get; }
    public string Fingerprint { get; }

    public FinancialJournal(FinancialScope scope, string currency, string idempotencyKey,
        string evidenceReference, IEnumerable<JournalLine> lines, Guid? reversesId = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.MerchantId == Guid.Empty || scope.CampaignId == Guid.Empty || !Enum.IsDefined(scope.Module))
            throw new ArgumentException("A trusted merchant, module and campaign are required.");
        if (currency is not ("USD" or "PHP" or "MYR" or "IDR" or "COP" or "PAB" or "DOP" or "THB"))
            throw new ArgumentException("Currency is outside the explicit initial catalog.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 120 ||
            string.IsNullOrWhiteSpace(evidenceReference) || evidenceReference.Length > 300)
            throw new ArgumentException("Bounded idempotency and evidence references are required.");
        Lines = lines?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(lines));
        if (Lines.Length is < 2 or > 100 || Lines.Any(x => x is null || x.AccountId == Guid.Empty ||
            x.OwnerId == Guid.Empty || string.IsNullOrWhiteSpace(x.Role) || x.Role.Length > 64 ||
            !Enum.IsDefined(x.Side) || x.MinorUnits <= 0))
            throw new ArgumentException("Journal lines require accounts, owners, roles and positive minor units.");
        long debit = 0, credit = 0;
        checked { foreach (var line in Lines) { if (line.Side == JournalSide.Debit) debit += line.MinorUnits; else credit += line.MinorUnits; } }
        if (debit != credit) throw new ArgumentException("Journal must balance in a single currency.");
        if (reversesId == Guid.Empty) throw new ArgumentException("Invalid reversal reference.");
        Id = Guid.NewGuid(); Scope = scope; Currency = currency; IdempotencyKey = idempotencyKey;
        EvidenceReference = evidenceReference; ReversesId = reversesId;
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { Scope, Currency, EvidenceReference, ReversesId, Lines }))));
    }

    public FinancialJournal Reverse(string key, string evidence) => new(Scope, Currency, key, evidence,
        Lines.Select(x => x with { Side = x.Side == JournalSide.Debit ? JournalSide.Credit : JournalSide.Debit }), Id);
}
