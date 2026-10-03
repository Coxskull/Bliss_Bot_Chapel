using System.Globalization;
using System.Text.RegularExpressions;

namespace Bliss.Domain.Demonstrations;

public sealed record AcceptedEconomicsPrice(string Amount, string CurrencyCode);

public static partial class EconomicsPriceSpeech
{
    public const string MissingResult =
        "Economics has no accepted result for that quote. Ask Alpha will not state a number.";

    public const string MissingQuote =
        "An Economics quote id is required. Ask Alpha will not invent a number.";

    public static string? Speak(string? amount, string? currency)
    {
        var number = (amount ?? string.Empty).Trim();
        var code = (currency ?? string.Empty).Trim().ToUpperInvariant();
        if (!AmountPattern().IsMatch(number) || !CurrencyPattern().IsMatch(code))
        {
            return null;
        }

        return number + " " + code;
    }

    public static string FormatAmount(decimal amount) =>
        amount.ToString("0.############################", CultureInfo.InvariantCulture);

    public static string PricingReply(string? amount, string? currency)
    {
        var spoken = Speak(amount, currency);
        if (spoken is null)
        {
            return "I can discuss the shape of a podcast placement, and I cannot invent a price. "
                + "Custom pricing and a binding quote stay with a human. If you share an approximate budget, timing, and market, I will record them for that handoff.";
        }

        return "Economics accepted " + spoken + ". That is the only number I can say. I cannot invent a price. "
            + "This is not a new quote and it is not a win.";
    }

    [GeneratedRegex(@"^\d+(\.\d{1,2})?$")]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
