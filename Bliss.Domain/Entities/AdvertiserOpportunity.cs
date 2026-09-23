namespace Bliss.Domain.Entities;

public class AdvertiserOpportunity
{
    public Guid Id { get; set; }
    public Guid AdvertiserProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? MarketCountryCode { get; set; }
    public string? Language { get; set; }
    /// <summary>
    /// Affiliate-opportunity commercial term. Not inventory market value and not
    /// an Economics Engine rate. Compensation splits are versioned elsewhere.
    /// </summary>
    public decimal? CommissionPercentage { get; set; }

    /// <summary>
    /// Affiliate-opportunity commercial term. Not a creator inventory quote.
    /// </summary>
    public decimal? FixedFee { get; set; }

    public string? CommissionType { get; set; }
    public string? ExternalOpportunityId { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public AdvertiserProgram AdvertiserProgram { get; set; } = null!;
    public ICollection<BlissMatch> BlissMatches { get; set; } = new List<BlissMatch>();
}
