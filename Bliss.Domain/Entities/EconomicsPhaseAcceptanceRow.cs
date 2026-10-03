namespace Bliss.Domain.Entities;

public sealed class EconomicsPhaseAcceptanceRow
{
    public Guid Id { get; set; }
    public string PhaseKey { get; set; } = string.Empty;
    public int Phase { get; set; }
    public string HistoryLine { get; set; } = string.Empty;
    public bool RepricingAuthorized { get; set; }
    public bool SettlementAuthorized { get; set; }
    public bool RecommendationRewritten { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime AcceptedAt { get; set; }
}
