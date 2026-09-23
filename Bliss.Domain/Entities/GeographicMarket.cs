namespace Bliss.Domain.Entities;

/// <summary>
/// A country or city/metro advertising market. Launch markets are rows, not engine code.
/// </summary>
public class GeographicMarket
{
    public Guid Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string? CityName { get; set; }
    public string? MetroName { get; set; }
    public string MarketCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<MarketBenchmarkObservation> BenchmarkObservations { get; set; } =
        new List<MarketBenchmarkObservation>();
}
