namespace Bliss.Domain.Entities;

/// <summary>
/// One local database measurement. Hosted acceptance is not claimed.
/// </summary>
public class BatchMeasurementRow
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public int StoredProspects { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public long? WorkingSetBytes { get; set; }
    public string ResourceLine { get; set; } = string.Empty;
    public string CostLine { get; set; } = string.Empty;
    public int Retries { get; set; }
    public int PartialFailures { get; set; }
    public bool Recovered { get; set; }
    public bool Leakage { get; set; }
    public bool HostedAcceptanceClaimed { get; set; }
    public bool FactoryTargetClaimed { get; set; }
    public bool CensusClaimed { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Failures { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
