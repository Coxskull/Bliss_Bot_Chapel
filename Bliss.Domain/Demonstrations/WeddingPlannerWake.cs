namespace Bliss.Domain.Demonstrations;

public sealed record WeddingPlannerWakeDecision(
    string Status,
    string Notice,
    Guid? QuoteId,
    Guid? AdvertiserId,
    Guid? WorkspaceId,
    Guid? SessionId);

/// <summary>
/// Wedding Planner wakes only after Economics has accepted a result.
/// A discovered business without that result is not planned.
/// </summary>
public static class WeddingPlannerWake
{
    public const string Asleep = "ASLEEP";
    public const string ContextOnly = "CONTEXT";
    public const string Awake = "AWAKE";

    public const string AsleepNotice =
        "Wedding Planner stays asleep. Commercial progression has not been accepted. This business is not planned. Delivery remains NOT_SENT.";

    public const string SuppressedNotice =
        "Wedding Planner stays asleep. Suppression comes before a campaign. This business is not planned. Delivery remains NOT_SENT.";

    public static WeddingPlannerWakeDecision Decide(
        string businessName,
        string market,
        bool suppressed,
        AcceptedEconomicsPrice? accepted,
        Guid? quoteId,
        Guid? advertiserId)
    {
        if (suppressed)
        {
            return new WeddingPlannerWakeDecision(Asleep, SuppressedNotice, null, null, null, null);
        }

        var spoken = EconomicsPriceSpeech.Speak(accepted?.Amount, accepted?.CurrencyCode);
        if (accepted is null || spoken is null || quoteId is null)
        {
            return new WeddingPlannerWakeDecision(Asleep, AsleepNotice, null, null, null, null);
        }

        if (advertiserId is null)
        {
            return new WeddingPlannerWakeDecision(
                ContextOnly,
                ContextNotice(businessName, market, spoken),
                quoteId,
                null,
                null,
                null);
        }

        return new WeddingPlannerWakeDecision(
            Awake,
            AwakeNotice(businessName, market, spoken),
            quoteId,
            advertiserId,
            null,
            null);
    }

    public static string AwakeNotice(string businessName, string market, string spoken) =>
        "Wedding Planner is awake for " + businessName + " in " + market
        + ". The accepted Economics amount is " + spoken
        + ". No campaign is planned. Delivery remains NOT_SENT.";

    public static string ContextNotice(string businessName, string market, string spoken) =>
        "Wedding Planner has the accepted Economics context for " + businessName + " in " + market
        + ". The accepted Economics amount is " + spoken
        + ". No advertiser workspace is on file, so no campaign is opened. Delivery remains NOT_SENT.";
}
