namespace Bliss.Domain.Entities;

/// <summary>
/// A public or approved source from which economics observations were collected.
/// </summary>
public class ResearchSource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<MarketBenchmarkObservation> BenchmarkObservations { get; set; } =
        new List<MarketBenchmarkObservation>();
}
