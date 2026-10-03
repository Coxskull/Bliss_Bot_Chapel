namespace Bliss.Domain.Demonstrations;

public sealed record AcceptanceRecord(string PhaseKey);

public sealed record AcceptanceReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string PhaseKey,
    int Phase,
    bool Accepted,
    bool Duplicate,
    int PlacementCount,
    int CampaignCount,
    string HistoryLine,
    bool RepricingAuthorized,
    bool SettlementAuthorized,
    bool RecommendationRewritten);

/// <summary>
/// Records owner acceptance of Economics Phase 9 and cites stored history.
/// An empty ledger stays unrecorded. A pricing rule is not changed.
/// </summary>
public static class EconomicsPhaseAcceptance
{
    public const int Phase = 9;

    public const string PhaseKey = "economics-phase-9";

    public const string Notice =
        "Economics Phase 9 is accepted by the owner. Historical actuals stay append-only. A pricing rule is not changed. A settlement is not created. An empty history stays unrecorded. Economics remains the only price authority. Green does not send. Delivery remains NOT_SENT.";

    public const string Unrecorded =
        "No historical actual is on file. None was invented.";

    public const string DuplicateNotice =
        "Economics Phase 9 is already accepted. A pricing rule was not changed. Delivery remains NOT_SENT.";

    public static AcceptanceReading Accept(
        int? phase,
        bool reprice,
        bool settle,
        int placementCount,
        int campaignCount,
        decimal? contractedAmount,
        string? currency,
        IReadOnlyList<AcceptanceRecord>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("The acceptance history is required. None is invented.");
        }

        if (reprice)
        {
            throw new InvalidOperationException("A pricing rule is not changed. None was invented.");
        }

        if (settle)
        {
            throw new InvalidOperationException("A settlement is not created. None was invented.");
        }

        if (phase != Phase)
        {
            throw new InvalidOperationException("Economics Phase 9 is the accepted phase. None is invented.");
        }

        var line = Cite(placementCount, campaignCount, contractedAmount, currency);
        var prior = existing.Any(item => string.Equals(item.PhaseKey, PhaseKey, StringComparison.Ordinal));
        return new AcceptanceReading(
            prior ? DuplicateNotice : Notice,
            false,
            "NOT_SENT",
            PhaseKey,
            Phase,
            true,
            prior,
            placementCount,
            campaignCount,
            line,
            false,
            false,
            false);
    }

    public static string Cite(
        int placementCount,
        int campaignCount,
        decimal? contractedAmount,
        string? currency)
    {
        if (placementCount < 0 || campaignCount < 0)
        {
            throw new InvalidOperationException("A historical count must be zero or greater. None is invented.");
        }

        if (placementCount == 0 && campaignCount == 0)
        {
            if (contractedAmount is not null || !string.IsNullOrWhiteSpace(currency))
            {
                throw new InvalidOperationException("A historical amount is not on file. None was invented.");
            }

            return Unrecorded;
        }

        if (contractedAmount is null || string.IsNullOrWhiteSpace(currency))
        {
            throw new InvalidOperationException("A stored historical amount is required. None is invented.");
        }

        var amount = contractedAmount.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return "A stored historical actual is on file. Contracted " + amount + " " + currency.Trim() + ". The recommendation was not changed.";
    }
}
