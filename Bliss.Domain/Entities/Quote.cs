namespace Bliss.Domain.Entities;

/// <summary>A commercial envelope whose immutable versions remain distinct from rate recommendations.</summary>
public class Quote
{
    public Guid Id { get; set; }
    public Guid? AdvertiserOpportunityId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CurrentVersionNumber { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AdvertiserOpportunity? AdvertiserOpportunity { get; set; }
    public ICollection<QuoteVersion> Versions { get; set; } = new List<QuoteVersion>();
    public ICollection<QuoteApprovalDecision> ApprovalDecisions { get; set; } =
        new List<QuoteApprovalDecision>();
    public ICollection<QuoteOutcome> Outcomes { get; set; } = new List<QuoteOutcome>();
}
