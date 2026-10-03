using System.Globalization;
using System.Text.RegularExpressions;

namespace Bliss.Domain.Demonstrations;

public enum NegotiationKind
{
    NotNegotiation,
    NoApprovedQuote,
    WillNotSplit,
    Unreadable,
    Explain,
    BelowFloor,
    AboveEnvelope,
    CurrencyMismatch,
    Inside
}

public sealed record NegotiationLine(decimal Floor, decimal High, string Currency);

public sealed record NegotiationDecision(
    NegotiationKind Kind,
    string Reply,
    bool HumanEscalation,
    decimal? Proposal = null);

public sealed record PreparedNegotiation(string Reply, bool HumanEscalation, string Gear);

/// <summary>
/// Decides whether a visitor proposal sits inside the Economics envelope.
/// The floor and the high are copied from the approved quote line.
/// This type does not write a quote and does not invent a discount.
/// </summary>
public static partial class NegotiationEnvelope
{
    public static bool IsNegotiation(string? message)
    {
        var normalized = (message ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return false;
        }

        if (ContainsAny(normalized, "human", "person", "call me", "meeting", "representative", "speak to"))
        {
            return false;
        }

        return ContainsAny(
            normalized,
            "negotiate",
            "discount",
            "cheaper",
            "counter",
            "lower",
            "take ",
            "we offer",
            "we can pay",
            "offer ");
    }

    public static NegotiationDecision Decide(string? message, NegotiationLine? line, bool multipleLines = false)
    {
        if (!IsNegotiation(message))
        {
            return new NegotiationDecision(NegotiationKind.NotNegotiation, string.Empty, false);
        }

        if (multipleLines)
        {
            return new NegotiationDecision(
                NegotiationKind.WillNotSplit,
                Finish("Ask Alpha will not split it. I cannot invent a price. This is not a win."),
                true);
        }

        if (line is null)
        {
            return new NegotiationDecision(
                NegotiationKind.NoApprovedQuote,
                Finish("Ask Alpha will not negotiate without an approved Economics quote. I cannot invent a price."),
                false);
        }

        var text = message!.Trim();
        if (CurrencyMismatch(text, line.Currency))
        {
            return new NegotiationDecision(
                NegotiationKind.CurrencyMismatch,
                Finish($"The proposal must use {line.Currency}. Ask Alpha will not invent a price."),
                false);
        }

        var amounts = Amounts(text);
        var floor = EconomicsPriceSpeech.FormatAmount(line.Floor);
        var high = EconomicsPriceSpeech.FormatAmount(line.High);
        var currency = line.Currency;
        if (amounts.Count > 1)
        {
            return new NegotiationDecision(
                NegotiationKind.Unreadable,
                Finish(
                    "Ask Alpha cannot read more than one amount. "
                    + $"The creator floor is {floor} {currency}. The envelope high is {high} {currency}. "
                    + "A proposal inside that range can be recorded as a draft. It is not accepted. I cannot invent a price."),
                false);
        }

        if (amounts.Count == 0)
        {
            return new NegotiationDecision(
                NegotiationKind.Explain,
                Finish(
                    $"The creator floor is {floor} {currency}. The envelope high is {high} {currency}. "
                    + "A proposal inside that range can be recorded as a draft. It is not accepted. I cannot invent a price."),
                false);
        }

        var proposal = amounts[0];
        if (proposal < line.Floor)
        {
            return new NegotiationDecision(
                NegotiationKind.BelowFloor,
                Finish(
                    "The proposal is below the creator floor. Ask Alpha will not break it. "
                    + $"The creator floor is {floor} {currency}. I cannot invent a price. This is not a win."),
                true);
        }

        if (proposal > line.High)
        {
            return new NegotiationDecision(
                NegotiationKind.AboveEnvelope,
                Finish(
                    "The proposal is outside the Economics envelope. Ask Alpha will not raise the price. "
                    + $"The envelope high is {high} {currency}. I cannot invent a price. This is not a win."),
                true);
        }

        return new NegotiationDecision(NegotiationKind.Inside, string.Empty, false, proposal);
    }

    public static string InsideReply(string ledgerAmount, string currency) =>
        Finish(
            "The proposal is inside the Economics envelope. "
            + $"Economics recorded a draft of {ledgerAmount} {currency}. It is not accepted. "
            + "I cannot invent a price. This is not a win.");

    public static PreparedNegotiation Present(NegotiationDecision decision, string? ledgerAmount = null, string? currency = null)
    {
        var reply = decision.Kind == NegotiationKind.Inside
            ? InsideReply(ledgerAmount ?? string.Empty, currency ?? string.Empty)
            : decision.Reply;
        var gear = decision.HumanEscalation ? "handoff" : "integrity";
        return new PreparedNegotiation(reply, decision.HumanEscalation, gear);
    }

    private static string Finish(string reply) => reply + " Delivery remains NOT_SENT.";

    private static bool CurrencyMismatch(string message, string currency)
    {
        foreach (Match match in AmountCurrencyPattern().Matches(message))
        {
            var code = match.Groups[1].Value.ToUpperInvariant();
            if (!string.Equals(code, currency, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<decimal> Amounts(string message)
    {
        var amounts = new List<decimal>();
        foreach (Match match in AmountPattern().Matches(message))
        {
            if (decimal.TryParse(match.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                amounts.Add(amount);
            }
        }

        return amounts;
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(text.Contains);

    [GeneratedRegex(@"\d+(?:\.\d{1,2})?", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"\d+(?:\.\d{1,2})?\s*([A-Za-z]{3})\b", RegexOptions.CultureInvariant)]
    private static partial Regex AmountCurrencyPattern();
}
