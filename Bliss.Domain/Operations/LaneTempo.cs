using System.Globalization;

namespace Bliss.Domain.Operations;

public sealed record LaneTempoDecision(
    string Lane,
    string Tempo,
    decimal? CeilingAmount,
    string? CeilingCurrency,
    string Reason,
    string Notice);

/// <summary>
/// Spend and pause for one lane on the existing operations console.
/// A paused lane does not stop the others. Green is not a send.
/// </summary>
public static class LaneTempo
{
    public const string Full = "FULL";
    public const string Reduced = "REDUCED";
    public const string Stopped = "STOPPED";

    public const string BoardNotice =
        "One operations console. A paused lane does not stop the others. Green does not send. Delivery remains NOT_SENT.";

    public static readonly string[] Lanes =
    [
        "DISCOVERY",
        "VERIFICATION",
        "ROAD_FINDING",
        "POLICY",
        "OUTREACH",
        "CREATOR_DISCOVERY"
    ];

    public static LaneTempoDecision Apply(
        string? lane,
        string? tempo,
        decimal? ceilingAmount,
        string? ceilingCurrency,
        string? reason)
    {
        var laneName = (lane ?? string.Empty).Trim().ToUpperInvariant();
        var tempoName = (tempo ?? string.Empty).Trim().ToUpperInvariant();
        var why = (reason ?? string.Empty).Trim();
        if (!Lanes.Contains(laneName, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The lane is not one of the six operations lanes.");
        }

        if (tempoName is not (Full or Reduced or Stopped))
        {
            throw new InvalidOperationException("Tempo must be FULL, REDUCED, or STOPPED.");
        }

        if (why.Length is 0 or > 300)
        {
            throw new InvalidOperationException("A reason of 1 to 300 characters is required for the audit.");
        }

        var currency = string.IsNullOrWhiteSpace(ceilingCurrency)
            ? null
            : ceilingCurrency.Trim().ToUpperInvariant();
        if (ceilingAmount is null != currency is null)
        {
            throw new InvalidOperationException("A spend ceiling needs both an amount and a currency. None is invented.");
        }

        if (ceilingAmount is < 0 or 0)
        {
            throw new InvalidOperationException("A spend ceiling must be greater than zero. None is invented.");
        }

        if (currency is not null && (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z')))
        {
            throw new InvalidOperationException("A spend ceiling currency is a three-letter code.");
        }

        return new LaneTempoDecision(
            laneName,
            tempoName,
            ceilingAmount,
            currency,
            why,
            Describe(laneName, tempoName, ceilingAmount, currency));
    }

    public static string Describe(string lane, string tempo, decimal? amount, string? currency)
    {
        var name = Display(lane);
        var motion = tempo switch
        {
            Full => name + " is at full tempo. Green moves to the next authorized action.",
            Reduced => name + " is at reduced tempo. The lane keeps moving inside its recorded limit. Other lanes are unchanged.",
            _ => name + " is stopped. This lane stays paused. Other lanes keep moving. The prospect record is preserved."
        };
        var ceiling = amount is null
            ? " No spend ceiling is recorded. None was invented."
            : " The operational spend ceiling is " + Format(amount.Value) + " " + currency + ". This is not an Economics price.";
        return motion + " Green does not send." + ceiling + " Delivery remains NOT_SENT.";
    }

    public static string Display(string lane) => lane switch
    {
        "DISCOVERY" => "Discovery",
        "VERIFICATION" => "Verification",
        "ROAD_FINDING" => "Road finding",
        "POLICY" => "Policy",
        "OUTREACH" => "Outreach",
        "CREATOR_DISCOVERY" => "Creator discovery",
        _ => lane
    };

    public static string Format(decimal amount) =>
        amount.ToString("0.############################", CultureInfo.InvariantCulture);
}
