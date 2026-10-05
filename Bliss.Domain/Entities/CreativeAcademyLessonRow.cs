namespace Bliss.Domain.Entities;

public sealed class CreativeAcademyLessonRow
{
    public Guid Id { get; set; }
    public string LessonKey { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ProperNouns { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? Score { get; set; }
    public bool Critical { get; set; }
    public bool VisualRecorded { get; set; }
    public string Defects { get; set; } = string.Empty;
    public string PreserveList { get; set; } = string.Empty;
    public string RepairList { get; set; } = string.Empty;
    public int ModelCalls { get; set; }
    public bool CampaignReady { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
