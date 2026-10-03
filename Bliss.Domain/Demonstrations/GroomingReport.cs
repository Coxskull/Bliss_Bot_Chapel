namespace Bliss.Domain.Demonstrations;

public sealed record GroomingCount(string Kind, int Count);

public sealed record GroomingDocument(
    int EventCount,
    IReadOnlyList<GroomingCount> Counts,
    string Friction,
    string Cost,
    string Experiments,
    string NextAction,
    string Notice,
    int AiCalls,
    bool ProductionChanged,
    string Delivery);

public sealed record ExternalReading(
    bool Staged,
    string Notice,
    string Provenance,
    string Hypothesis,
    int AiCalls,
    bool ProductionChanged,
    string Delivery);

/// <summary>
/// A grooming note is counted from production events.
/// An external page is untrusted and cannot change the prospect.
/// </summary>
public static class GroomingReport
{
    public const string Notice =
        "Production events come first. Research cannot change production. Delivery remains NOT_SENT.";

    public const string WaitingNotice =
        "External reading waits for a production event. Research cannot change production. Delivery remains NOT_SENT.";

    public const string Cost =
        "No research cost is recorded. None was invented.";

    public const string Experiments =
        "No experiment is open. A weekly note is not a deployment.";

    public const string ExternalNotice =
        "The external page is untrusted. Its instructions were not executed. Production was not changed. Delivery remains NOT_SENT.";

    public const string Provenance =
        "Source: operator-supplied excerpt. Provenance is untrusted.";

    public const string Hypothesis =
        "The reading is a hypothesis. It is not a production change.";

    public static GroomingDocument Read(IEnumerable<string?>? kinds)
    {
        var recognized = Recognize(kinds);
        if (recognized.Count == 0)
        {
            return new GroomingDocument(
                0,
                [],
                string.Empty,
                Cost,
                Experiments,
                "The next action waits for a production event. Nothing is sent.",
                WaitingNotice,
                0,
                false,
                "NOT_SENT");
        }

        var counts = AcquisitionEvents.ObservableKinds
            .Select(kind => new GroomingCount(kind, recognized.Count(item => item == kind)))
            .Where(item => item.Count > 0)
            .ToList();
        var friction = recognized.Contains("SUPPRESSED", StringComparer.Ordinal)
            ? "Suppression is stored. The prohibited action stays stopped."
            : "No suppression is stored on this record.";
        return new GroomingDocument(
            recognized.Count,
            counts,
            friction,
            Cost,
            Experiments,
            Next(recognized[^1]),
            Notice,
            0,
            false,
            "NOT_SENT");
    }

    public static ExternalReading Stage(IEnumerable<string?>? kinds, string? excerpt)
    {
        if (Read(kinds).EventCount == 0)
        {
            throw new InvalidOperationException(WaitingNotice);
        }

        var text = (excerpt ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            throw new InvalidOperationException("An external excerpt is required before a reading is staged.");
        }

        if (text.Length > 500)
        {
            throw new InvalidOperationException("An external excerpt is limited to 500 characters.");
        }

        return new ExternalReading(true, ExternalNotice, Provenance, Hypothesis, 0, false, "NOT_SENT");
    }

    private static List<string> Recognize(IEnumerable<string?>? kinds)
    {
        var catalog = AcquisitionEvents.ObservableKinds;
        return (kinds ?? [])
            .Select(item => item ?? string.Empty)
            .Where(item => catalog.Contains(item, StringComparer.Ordinal))
            .ToList();
    }

    private static string Next(string kind) => kind switch
    {
        "PROSPECT_RECORDED" => "The next action is to keep the stored business. Nothing is sent.",
        "DEMONSTRATION_PREPARED" => "The next action is to keep the manufactured page available. Nothing is sent.",
        "PAGE_OPENED" => "The next action is to study the stored page open. The watcher is not named. Nothing is sent.",
        "MESSAGE_RECEIVED" => "The next action is to answer from the stored conversation. Nothing is sent.",
        "DECISION_MAKER_RECORDED" => "The next action is to keep the public evidence on the record. Nothing is sent.",
        "CONTACT_ROAD_RECORDED" => "The next action is to keep the public road ineligible to send.",
        "SUPPRESSED" => "The next action is to keep the suppression. Nothing is sent.",
        _ => "The next action waits for a production event. Nothing is sent."
    };
}
