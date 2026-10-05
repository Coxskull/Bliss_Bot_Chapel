namespace Bliss.Domain.Entities;

public sealed class CreativeAcademyDnaRow
{
    public Guid Id { get; set; }
    public string DnaKey { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string Hero { get; set; } = string.Empty;
    public string Palette { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string TeacherKey { get; set; } = string.Empty;
    public bool TeacherOnFile { get; set; }
    public bool Distinct { get; set; }
    public int ModelCalls { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
