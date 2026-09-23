namespace Bliss.Domain.Entities;

public class RateRecommendationFactor
{
    public Guid Id { get; set; }
    public Guid RateRecommendationId { get; set; }
    public string FactorCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public decimal? AdjustmentMultiplier { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public RateRecommendation RateRecommendation { get; set; } = null!;
}
