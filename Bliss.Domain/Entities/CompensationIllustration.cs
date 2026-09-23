namespace Bliss.Domain.Entities;

/// <summary>An immutable allocation preview. It creates no payable or settlement.</summary>
public class CompensationIllustration
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Guid QuoteVersionId { get; set; }
    public Guid QuoteOutcomeId { get; set; }
    public Guid CompensationRuleVersionId { get; set; }
    public decimal GrossAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string InputSnapshotJson { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Quote Quote { get; set; } = null!;
    public QuoteVersion QuoteVersion { get; set; } = null!;
    public QuoteOutcome QuoteOutcome { get; set; } = null!;
    public CompensationRuleVersion CompensationRuleVersion { get; set; } = null!;
    public ICollection<CompensationIllustrationLine> Lines { get; set; } =
        new List<CompensationIllustrationLine>();
}
