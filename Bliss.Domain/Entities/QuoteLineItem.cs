namespace Bliss.Domain.Entities;

/// <summary>An explicit quoted amount linked to the recommendation that informed it.</summary>
public class QuoteLineItem
{
    public Guid Id { get; set; }
    public Guid QuoteVersionId { get; set; }
    public Guid RateRecommendationId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitAmount { get; set; }
    public decimal LineAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal RecommendationLow { get; set; }
    public decimal RecommendationTarget { get; set; }
    public decimal RecommendationHigh { get; set; }

    public QuoteVersion QuoteVersion { get; set; } = null!;
    public RateRecommendation RateRecommendation { get; set; } = null!;
}
