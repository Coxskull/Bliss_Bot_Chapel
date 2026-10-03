namespace Bliss.Domain.Entities;

/// <summary>
/// One append-only research note. It does not change the prospect.
/// </summary>
public class LearningNoteRow
{
    public Guid Id { get; set; }
    public string ProspectSlug { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool LaboratoryGraduated { get; set; }
    public bool AuthorizedTraffic { get; set; }
    public bool ProductionChanged { get; set; }
    public bool BehaviorChanged { get; set; }
    public int ModelCalls { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
