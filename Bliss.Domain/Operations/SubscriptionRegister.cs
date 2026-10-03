using System.Globalization;

namespace Bliss.Domain.Operations;

public sealed record KnownService(
    string Key,
    string DisplayName,
    string Provider,
    string Capability,
    string? Classification,
    string Status,
    string AccountOwner,
    string Plan);

public sealed record StoredService(
    string Key,
    string? Classification,
    decimal? MonthlyAmount,
    string? Currency,
    string Status);

public sealed record ServicePlace(
    string Key,
    string DisplayName,
    string ClassificationLine,
    string CostLine,
    string Status);

public sealed record RegisterReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    IReadOnlyList<ServicePlace> Services);

public sealed record CostDecision(
    string ServiceKey,
    decimal Amount,
    string Currency,
    string Source,
    string Notice);

public sealed record GapProposal(
    string? ServiceName,
    string? Provider,
    string? Capability,
    string? Classification,
    string? EngineeringContract,
    decimal? MonthlyAmount,
    string? MonthlyCurrency,
    string? UsageCharges,
    string? Alternatives,
    string? BuildAlternative,
    string? WhyAlphaIsInsufficient,
    decimal? EstimatedAmount,
    string? EstimatedCurrency,
    string? RequiredDate,
    string? RequiredOrOptional);

public sealed record ReviewDecision(
    string ServiceName,
    string Provider,
    string Capability,
    string Classification,
    string Status,
    decimal? MonthlyAmount,
    string? MonthlyCurrency,
    decimal? EstimatedAmount,
    string? EstimatedCurrency,
    string EngineeringContract,
    string UsageCharges,
    string Alternatives,
    string BuildAlternative,
    string WhyAlphaIsInsufficient,
    string RequiredDate,
    string RequiredOrOptional,
    string Reason,
    string Notice);

/// <summary>
/// The subscription register persists known services and operator-supplied costs.
/// A missing cost stays unrecorded. A review is not a purchase.
/// </summary>
public static class SubscriptionRegister
{
    public const string Notice =
        "The subscription register records stored rows. A missing cost stays unrecorded. None was invented. A review is not a purchase. Nothing is purchased. Green does not send. Delivery remains NOT_SENT.";

    public const string Unrecorded = "UNRECORDED";
    public const string NewPaid = "NEW_PAID_SUBSCRIPTION";
    public const string RejectedDuplicate = "DUPLICATIVE_REJECTED";
    public const string NotProposed = "NOT_PROPOSED";
    public const string Rejected = "REJECTED";

    public static readonly string[] Classes =
    [
        "FREE_SELF_HOSTED",
        "EXISTING_ALPHA_SUBSCRIPTION",
        "USAGE_BASED",
        "FREE_OR_DEVELOPMENT_TIER",
        NewPaid,
        RejectedDuplicate
    ];

    public static readonly KnownService[] Known =
    [
        new("BLISS_CHAPEL", "Bliss Bot Chapel", "self-hosted", "Matching, operations, Wedding Planner Phase 1, and Economics", "FREE_SELF_HOSTED", "OBSERVED", "n/a", "n/a"),
        new("POSTGRESQL", "PostgreSQL", "operator-hosted", "Authoritative business data", null, "OBSERVED", Unrecorded, Unrecorded),
        new("IDENTITY", "OpenID Connect", "not selected", "Operator and advertiser authentication outside Development", null, "OBSERVED", Unrecorded, Unrecorded),
        new("N8N", "n8n", "n8n", "Inactive orchestration templates. Not business authority", null, "UNACTIVATED", Unrecorded, Unrecorded),
        new("GOHIGHLEVEL", "GoHighLevel", "GoHighLevel", "Named in a prior document. Not coupled to this repository", null, "OBSERVED", Unrecorded, Unrecorded),
        new("CONSUMER_CHAT", "Personal consumer chat", "consumer chat product", "None for production Alpha", RejectedDuplicate, Rejected, "personal", "consumer"),
        new("FFMPEG", "FFmpeg", "self-hosted", "Routine video compositing. Not a subscription", "FREE_SELF_HOSTED", NotProposed, "n/a", "n/a"),
        new("QR", "In-process QR generation", "self-hosted", "Real QR codes. No per-code subscription", "FREE_SELF_HOSTED", NotProposed, "n/a", "n/a"),
        new("ENRICHMENT", "Contact enrichment", "none selected", "Not purchased", null, NotProposed, "n/a", "none"),
        new("AI_PROVIDERS", "Production model providers", "none locked", "Not called by accepted Bliss pricing authority", null, NotProposed, Unrecorded, "none")
    ];

    public static RegisterReading Read(IEnumerable<StoredService>? rows)
    {
        var stored = rows?.ToList() ?? [];
        if (stored.Count != Known.Length
            || Known.Any(item => stored.Count(row => row.Key == item.Key) != 1))
        {
            throw new InvalidOperationException(
                "A ledger reading needs the stored register. None is invented.");
        }

        var places = Known.Select(item =>
        {
            var row = stored.Single(candidate => candidate.Key == item.Key);
            var status = string.IsNullOrWhiteSpace(row.Status)
                ? "Status is not recorded. None was invented."
                : row.Status.Trim();
            return new ServicePlace(
                item.Key,
                item.DisplayName,
                ClassLine(row.Classification),
                CostLine(row.MonthlyAmount, row.Currency),
                status);
        }).ToList();

        return new RegisterReading(Notice, false, "NOT_SENT", places);
    }

    public static CostDecision RecordCost(
        string? serviceKey,
        decimal? amount,
        string? currency,
        string? source)
    {
        var key = (serviceKey ?? string.Empty).Trim().ToUpperInvariant();
        if (Known.All(item => item.Key != key))
        {
            throw new InvalidOperationException(
                "The cost must name a stored service. None is invented. Nothing was purchased.");
        }

        var money = RequiredMoney(amount, currency, "A recorded cost needs both an amount and a currency. None is invented.");
        var billing = (source ?? string.Empty).Trim();
        if (billing.Length is 0 or > 300)
        {
            throw new InvalidOperationException(
                "A recorded cost needs a billing source of 1 to 300 characters. None is invented.");
        }

        return new CostDecision(
            key,
            money.Amount,
            money.Currency,
            billing,
            CostLine(money.Amount, money.Currency)
                + " Green does not send. Delivery remains NOT_SENT.");
    }

    public static ReviewDecision Review(GapProposal? proposal)
    {
        var review = proposal ?? new GapProposal(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var classification = (review.Classification ?? string.Empty).Trim().ToUpperInvariant();
        if (!Classes.Contains(classification, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The classification is not one of the register classes. None was invented. Nothing was purchased.");
        }

        if (classification is not (NewPaid or RejectedDuplicate))
        {
            throw new InvalidOperationException(
                "Only a new paid subscription or a rejected duplicate can be reviewed. None was invented. Nothing was purchased.");
        }

        var service = (review.ServiceName ?? string.Empty).Trim();
        if (service.Length is 0 or > 120)
        {
            throw new InvalidOperationException(
                "A review needs a service name of 1 to 120 characters. Nothing was purchased.");
        }

        if (classification == RejectedDuplicate)
        {
            var rejectedMoney = OptionalMoney(review.MonthlyAmount, review.MonthlyCurrency);
            var rejectedEstimate = OptionalMoney(review.EstimatedAmount, review.EstimatedCurrency);
            return new ReviewDecision(
                service,
                Clean(review.Provider, 120),
                Clean(review.Capability, 300),
                classification,
                Rejected,
                rejectedMoney.Amount,
                rejectedMoney.Currency,
                rejectedEstimate.Amount,
                rejectedEstimate.Currency,
                Clean(review.EngineeringContract, 200),
                Clean(review.UsageCharges, 300),
                Clean(review.Alternatives, 300),
                Clean(review.BuildAlternative, 300),
                Clean(review.WhyAlphaIsInsufficient, 300),
                Clean(review.RequiredDate, 80),
                Clean(review.RequiredOrOptional, 16),
                "Rejected. Do not buy it.",
                "The proposal is rejected. Do not buy it. Nothing was purchased. Green does not send. Delivery remains NOT_SENT.");
        }

        var provider = RequiredText(review.Provider, 120);
        var capability = RequiredText(review.Capability, 300);
        var contract = RequiredText(review.EngineeringContract, 200);
        var usage = RequiredText(review.UsageCharges, 300);
        var alternatives = RequiredText(review.Alternatives, 300);
        var build = RequiredText(review.BuildAlternative, 300);
        var why = RequiredText(review.WhyAlphaIsInsufficient, 300);
        var date = RequiredText(review.RequiredDate, 80);
        var required = (review.RequiredOrOptional ?? string.Empty).Trim().ToUpperInvariant();
        if (required is not ("REQUIRED" or "OPTIONAL"))
        {
            throw new InvalidOperationException(IncompleteReview);
        }

        var monthly = RequiredMoney(
            review.MonthlyAmount,
            review.MonthlyCurrency,
            IncompleteReview);
        var estimated = RequiredMoney(
            review.EstimatedAmount,
            review.EstimatedCurrency,
            IncompleteReview);

        return new ReviewDecision(
            service,
            provider,
            capability,
            classification,
            NotProposed,
            monthly.Amount,
            monthly.Currency,
            estimated.Amount,
            estimated.Currency,
            contract,
            usage,
            alternatives,
            build,
            why,
            date,
            required,
            contract,
            "The capability-gap review is complete. The proposal stays NOT_PROPOSED. A review is not a purchase. Nothing was bought. Green does not send. Delivery remains NOT_SENT.");
    }

    public static string ClassLine(string? classification) =>
        string.IsNullOrWhiteSpace(classification)
            ? "Classification is not recorded. None was invented."
            : classification.Trim();

    public static string CostLine(decimal? amount, string? currency) =>
        amount is null
            ? "Cost is not recorded. None was invented."
            : "Recorded cost: " + Format(amount.Value) + " " + currency
                + ". This is the amount the operator supplied. It is not an Economics price. Nothing was purchased.";

    public static string Format(decimal amount) =>
        amount.ToString("0.############################", CultureInfo.InvariantCulture);

    private const string IncompleteReview =
        "A new paid subscription needs a complete capability-gap review. None was invented. Nothing was purchased.";

    private static string RequiredText(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length is 0 || text.Length > max)
        {
            throw new InvalidOperationException(IncompleteReview);
        }

        return text;
    }

    private static string Clean(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private readonly record struct Money(decimal? Amount, string? Currency);

    private readonly record struct Priced(decimal Amount, string Currency);

    private static Priced RequiredMoney(decimal? amount, string? currency, string missingMessage)
    {
        if (amount is null && string.IsNullOrWhiteSpace(currency))
        {
            throw new InvalidOperationException(missingMessage);
        }

        var priced = CheckedMoney(amount, currency);
        return new Priced(priced.Amount!.Value, priced.Currency!);
    }

    private static Money OptionalMoney(decimal? amount, string? currency)
    {
        if (amount is null && string.IsNullOrWhiteSpace(currency))
        {
            return new Money(null, null);
        }

        return CheckedMoney(amount, currency);
    }

    private static Money CheckedMoney(decimal? amount, string? currency)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
        if (amount is null != code is null)
        {
            throw new InvalidOperationException(
                "A price needs both an amount and a currency. None is invented.");
        }

        if (amount is < 0 or 0)
        {
            throw new InvalidOperationException(
                "A price must be greater than zero. None is invented.");
        }

        if (code is null || code.Length != 3 || code.Any(character => character is < 'A' or > 'Z'))
        {
            throw new InvalidOperationException(
                "A currency is a three-letter code. None is invented.");
        }

        return new Money(amount.Value, code);
    }
}
