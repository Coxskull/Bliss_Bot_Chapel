namespace Bliss.Domain.Demonstrations;

public sealed record ProgressionDecision(
    string Signal,
    string Notice,
    string NextAction,
    bool GreenMeansSend,
    bool Erased,
    string Delivery);

/// <summary>
/// Green moves to the next authorized action. Yellow keeps the record.
/// Red stops the prohibited action and does not erase the business.
/// </summary>
public static class ProspectProgression
{
    public const string Green = "GREEN";
    public const string Yellow = "YELLOW";
    public const string Red = "RED";

    public static ProgressionDecision Decide(
        string? businessName,
        string? prospectState,
        bool suppressed,
        string? suppressionReason,
        bool roadStored,
        bool previewEligible)
    {
        var name = (businessName ?? string.Empty).Trim();
        if (name.Length < 3)
        {
            throw new InvalidOperationException("A business name is required. None is invented.");
        }

        if (suppressed)
        {
            var reason = (suppressionReason ?? string.Empty).Trim();
            var kept = reason.Length == 0
                ? " The stored suppression is kept. None is invented."
                : " The reason is kept: " + reason + ".";
            return Finish(
                Red,
                name + " is red. Red stops the prohibited action. The prospect record is preserved." + kept,
                "The next authorized action is to keep the suppression. Nothing is sent.");
        }

        if (string.Equals((prospectState ?? string.Empty).Trim(), "PRESERVED", StringComparison.Ordinal))
        {
            return Finish(
                Yellow,
                name + " is yellow. Yellow keeps the record. The demonstration stays withheld until the road is rechecked.",
                "The next authorized action is to recheck the road. Nothing is sent.");
        }

        if (previewEligible)
        {
            return Finish(
                Green,
                name + " is green. Green moves to the approved queue. The authorized action prepares the preview.",
                "The next authorized action is the preview queue. Nothing is sent.");
        }

        if (roadStored)
        {
            return Finish(
                Green,
                name + " is green. Green moves to the policy check. A public road is not permission to send.",
                "The next authorized action is the eligibility check. Nothing is sent.");
        }

        return Finish(
            Green,
            name + " is green. Green moves to road finding.",
            "The next authorized action is to find a public road. Nothing is sent.");
    }

    private static ProgressionDecision Finish(string signal, string notice, string next) =>
        new(signal, notice + " Green does not send. Delivery remains NOT_SENT.", next, false, false, "NOT_SENT");
}
