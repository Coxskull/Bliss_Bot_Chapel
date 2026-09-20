namespace Bliss.Domain.Entities;

/// <summary>
/// Minimal campaign foundation for future placement work. Phase 1 does not execute campaigns.
/// </summary>
public class Campaign
{
    public Guid Id { get; set; }
    public Guid? AdvertiserOpportunityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAt { get; set; }

    public AdvertiserOpportunity? AdvertiserOpportunity { get; set; }
    public ICollection<CampaignPlacement> Placements { get; set; } = new List<CampaignPlacement>();
}
