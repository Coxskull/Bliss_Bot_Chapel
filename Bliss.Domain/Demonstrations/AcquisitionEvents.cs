namespace Bliss.Domain.Demonstrations;

public sealed record AcquisitionEventResult(
    bool Accepted,
    string Kind,
    string Observation,
    string Error,
    int AiCalls);

public static class AcquisitionEvents
{
    public const string OpinionRefusal =
        "An event records what happened. Alpha will not store an opinion.";

    public const string DeliveryRefusal =
        "Nothing was sent. A delivery event is not recorded.";

    public const string WatcherRefusal =
        "A page open does not name who watched.";

    public const string CatalogRefusal =
        "An observable event needs a kind from the catalog.";

    public static readonly string[] ObservableKinds =
    [
        "PROSPECT_RECORDED",
        "DEMONSTRATION_PREPARED",
        "PAGE_OPENED",
        "MESSAGE_RECEIVED",
        "DECISION_MAKER_RECORDED",
        "CONTACT_ROAD_RECORDED",
        "SUPPRESSED"
    ];

    private static readonly Dictionary<string, string> Observations = new(StringComparer.Ordinal)
    {
        ["PROSPECT_RECORDED"] = "The business was stored from a public source.",
        ["DEMONSTRATION_PREPARED"] = "A demonstration was manufactured.",
        ["PAGE_OPENED"] = "The demonstration page was opened. The watcher is not named.",
        ["MESSAGE_RECEIVED"] = "A visitor message was received on the page.",
        ["DECISION_MAKER_RECORDED"] = "Public decision-maker evidence was stored.",
        ["CONTACT_ROAD_RECORDED"] = "A public contact road was copied.",
        ["SUPPRESSED"] = "A suppression reason was stored. Nothing was sent."
    };

    public static AcquisitionEventResult Accept(string? kind, string? watcher)
    {
        var key = Key(kind);
        if (IsDelivery(key))
        {
            return Refuse(DeliveryRefusal);
        }

        if (!string.IsNullOrWhiteSpace(watcher) && watcher.Trim().Length >= 2)
        {
            return Refuse(WatcherRefusal);
        }

        var match = ObservableKinds.FirstOrDefault(item => Key(item) == key);
        if (match is null)
        {
            return Refuse(key.Length < 3 ? CatalogRefusal : OpinionRefusal);
        }

        return new AcquisitionEventResult(true, match, Observations[match], string.Empty, 0);
    }

    private static AcquisitionEventResult Refuse(string error) =>
        new(false, string.Empty, string.Empty, error, 0);

    private static bool IsDelivery(string key) => key is
        "SENT" or "DELIVERED" or "EMAILED" or "EMAILSENT" or "WHATSAPPSENT" or "MESSAGESENT" or "DELIVERY";

    private static string Key(string? value) =>
        (value ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant();
}
