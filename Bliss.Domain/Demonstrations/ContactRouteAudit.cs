namespace Bliss.Domain.Demonstrations;

public sealed record StoredRoute(
    string? BusinessName,
    bool Suppressed,
    string? Freshness,
    string? EvidenceSourceUrl,
    int RoadCount,
    bool PreviewPrepared);

public sealed record RouteRow(
    string BusinessName,
    string Route,
    string Adapter,
    string Transmission,
    string Notice);

public sealed record RouteReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    bool CensusClaimed,
    int Stored,
    int Suppressed,
    int Stale,
    int MissingEvidence,
    int NoRoad,
    int Preview,
    int Withheld,
    IReadOnlyList<RouteRow> Routes);

public sealed record StoredTransmissionAudit(string? IdempotencyKey, string? Transmission);

public sealed record TransmissionDecision(
    bool Accepted,
    bool Duplicate,
    bool Written,
    string IdempotencyKey,
    string Adapter,
    string Transmission,
    string Notice,
    bool GreenMeansSend,
    string Delivery);

/// <summary>
/// Reads the stored contact route. A public address is not permission to
/// send. An explicit transmission request is recorded and not transmitted.
/// </summary>
public static class ContactRouteAudit
{
    public const string Notice =
        "The contact route reads stored evidence, freshness, suppression, and the preview adapter. A public address is not permission to send. A missing person is not invented. This is not a census. Transmission stays NOT_SENT. Green does not send.";

    public const string BlankName =
        "A business name is required. None was invented.";

    public const string SuppressedRoute = "SUPPRESSED";
    public const string StaleRoute = "STALE";
    public const string MissingEvidenceRoute = "MISSING_EVIDENCE";
    public const string NoRoadRoute = "NO_ROAD";
    public const string PreviewRoute = "PREVIEW";
    public const string WithheldRoute = "WITHHELD";
    public const string NoAdapter = "none";

    public const string BlankAuthorization =
        "Transmission requires explicit words. Nothing is sent.";

    public const string BlankKey =
        "An idempotency key is required. Nothing is sent.";

    public const string DuplicateRequest =
        "That transmission request is already recorded. Nothing was sent.";

    public const string RecordedRequest =
        "The transmission request was recorded. No adapter transmitted it. Delivery remains NOT_SENT.";

    public const string AddressWithheld =
        "The recorded words included an address. They are not repeated. Nothing was sent.";

    public static RouteReading Read(IReadOnlyList<StoredRoute>? routes)
    {
        if (routes is null)
        {
            throw new InvalidOperationException("A contact route reading needs the stored prospects. None is invented.");
        }

        var rows = new List<RouteRow>();
        var blank = 0;
        foreach (var item in routes)
        {
            var name = (item.BusinessName ?? string.Empty).Trim();
            if (name.Length < 3)
            {
                blank++;
                continue;
            }

            rows.Add(Classify(name, item));
        }

        rows.Sort((left, right) => string.Compare(left.BusinessName, right.BusinessName, StringComparison.Ordinal));
        return new RouteReading(
            Notice,
            false,
            DeliveryPolicy.Transmission,
            false,
            rows.Count,
            rows.Count(row => row.Route == SuppressedRoute),
            rows.Count(row => row.Route == StaleRoute),
            rows.Count(row => row.Route == MissingEvidenceRoute) + blank,
            rows.Count(row => row.Route == NoRoadRoute),
            rows.Count(row => row.Route == PreviewRoute),
            rows.Count(row => row.Route == WithheldRoute),
            rows);
    }

    public static TransmissionDecision Authorize(
        string? authorization,
        string? idempotencyKey,
        IReadOnlyList<StoredTransmissionAudit>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("A transmission audit needs the stored audits. None is invented.");
        }

        var words = (authorization ?? string.Empty).Trim();
        if (words.Length == 0)
        {
            return Refuse(BlankAuthorization);
        }

        var key = (idempotencyKey ?? string.Empty).Trim();
        if (!KeyIsUsable(key))
        {
            return Refuse(BlankKey);
        }

        foreach (var item in existing)
        {
            if (string.Equals((item.IdempotencyKey ?? string.Empty).Trim(), key, StringComparison.Ordinal))
            {
                return new TransmissionDecision(
                    true,
                    true,
                    false,
                    key,
                    NoAdapter,
                    DeliveryPolicy.Transmission,
                    DuplicateRequest,
                    false,
                    DeliveryPolicy.Transmission);
            }
        }

        return new TransmissionDecision(
            true,
            false,
            true,
            key,
            NoAdapter,
            DeliveryPolicy.Transmission,
            RecordedRequest,
            false,
            DeliveryPolicy.Transmission);
    }

    public static string DisplayAuthorization(string? authorization)
    {
        var text = (authorization ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return BlankAuthorization;
        }

        if (text.Contains('@') || text.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            return AddressWithheld;
        }

        return text.Length <= 160 ? text : text.Substring(0, 160);
    }

    private static RouteRow Classify(string name, StoredRoute item)
    {
        if (item.Suppressed)
        {
            return Row(name, SuppressedRoute, NoAdapter, "Suppression comes before the adapter. Nothing is sent.");
        }

        if (string.Equals((item.Freshness ?? string.Empty).Trim(), "STALE", StringComparison.Ordinal))
        {
            return Row(name, StaleRoute, NoAdapter, "Stale evidence is not used. Nothing is sent.");
        }

        if (!OpportunityScreen.IsPublicSource(item.EvidenceSourceUrl ?? string.Empty))
        {
            return Row(name, MissingEvidenceRoute, NoAdapter, "A public evidence URL is required before a person is used. None was invented.");
        }

        if (item.RoadCount < 1)
        {
            return Row(name, NoRoadRoute, NoAdapter, "A route needs a stored contact road. Alpha will not invent one.");
        }

        if (item.PreviewPrepared)
        {
            return Row(name, PreviewRoute, DeliveryPolicy.PreviewAdapter, "The preview adapter prepared a copy. Transmission remains NOT_SENT.");
        }

        return Row(name, WithheldRoute, NoAdapter, "Evidence is stored. Transmission was not authorized. Nothing is sent.");
    }

    private static RouteRow Row(string name, string route, string adapter, string notice) =>
        new(name, route, adapter, DeliveryPolicy.Transmission, name + ": " + notice);

    private static TransmissionDecision Refuse(string notice) =>
        new(false, false, false, string.Empty, NoAdapter, DeliveryPolicy.Transmission, notice, false, DeliveryPolicy.Transmission);

    private static bool KeyIsUsable(string key)
    {
        if (key.Length < 8 || key.Length > 80)
        {
            return false;
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                return false;
            }
        }

        return true;
    }
}
