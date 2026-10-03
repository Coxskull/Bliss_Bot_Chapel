namespace Bliss.Domain.Entities;

public sealed class ClosedModelRow
{
    public Guid Id { get; set; }
    public string ReadingKey { get; set; } = string.Empty;
    public int ConfiguredModels { get; set; }
    public int ModelCalls { get; set; }
    public int NoteCount { get; set; }
    public bool LaboratoryPassed { get; set; }
    public int PassedCount { get; set; }
    public int ScenarioCount { get; set; }
    public bool ModelsConfigured { get; set; }
    public bool AuthorizedTraffic { get; set; }
    public bool ProductionChanged { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
