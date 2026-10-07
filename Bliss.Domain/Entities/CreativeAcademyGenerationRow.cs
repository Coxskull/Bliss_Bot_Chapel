namespace Bliss.Domain.Entities;

public sealed class CreativeAcademyGenerationRow
{
    public Guid Id { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string TeacherKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string ProviderRequestId { get; set; } = string.Empty;
    public string RecipeSha256 { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string CostStatus { get; set; } = "UNRECORDED";
    public decimal? Cost { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int ModelCalls { get; set; }
    public bool CampaignReady { get; set; }
    public string Delivery { get; set; } = string.Empty;
    public string Notice { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
