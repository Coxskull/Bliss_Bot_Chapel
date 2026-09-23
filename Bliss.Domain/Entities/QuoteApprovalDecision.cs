namespace Bliss.Domain.Entities;

/// <summary>A human decision on one exact immutable quote version.</summary>
public class QuoteApprovalDecision
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Guid QuoteVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string ReviewerLabel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Quote Quote { get; set; } = null!;
    public QuoteVersion QuoteVersion { get; set; } = null!;
}
