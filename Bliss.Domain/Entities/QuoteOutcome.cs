namespace Bliss.Domain.Entities;

/// <summary>An advertiser response to an approved quote version.</summary>
public class QuoteOutcome
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Guid QuoteVersionId { get; set; }
    public Guid? NewQuoteVersionId { get; set; }
    public string Response { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Quote Quote { get; set; } = null!;
    public QuoteVersion QuoteVersion { get; set; } = null!;
    public QuoteVersion? NewQuoteVersion { get; set; }
}
