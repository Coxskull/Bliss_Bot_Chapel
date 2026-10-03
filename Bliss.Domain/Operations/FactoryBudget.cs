using System.Globalization;

namespace Bliss.Domain.Operations;

public sealed record StoredBudget(
    string Scope,
    decimal? CeilingAmount,
    string? CeilingCurrency,
    decimal? RecordedSpend);

public sealed record BudgetPlace(
    string Scope,
    string DisplayName,
    string Notice);

public sealed record BudgetReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    IReadOnlyList<BudgetPlace> Scopes);

public sealed record BudgetDecision(
    string Scope,
    decimal CeilingAmount,
    string CeilingCurrency,
    decimal? RecordedSpend,
    string Reason,
    bool Degraded,
    string Notice);

/// <summary>
/// Factory budgets are operational ceilings. A reached ceiling degrades that
/// scope only. A ceiling is not an Economics price and it purchases nothing.
/// </summary>
public static class FactoryBudget
{
    public const string Daily = "DAILY";
    public const string Monthly = "MONTHLY";
    public const string Provider = "PROVIDER";
    public const string Prospect = "PROSPECT";

    public const string Notice =
        "Factory budgets are operational ceilings on the same operations console. A missing ceiling stays unrecorded. A reached ceiling degrades only that scope. This is not an Economics price. Nothing is purchased. Green does not send. Delivery remains NOT_SENT.";

    public static readonly string[] Scopes = [Daily, Monthly, Provider, Prospect];

    public static BudgetReading Read(IEnumerable<StoredBudget>? rows)
    {
        var stored = rows?.ToList() ?? [];
        if (stored.Count != Scopes.Length
            || Scopes.Any(scope => stored.Count(row => row.Scope == scope) != 1))
        {
            throw new InvalidOperationException(
                "A budget reading needs the four stored scopes. None is invented.");
        }

        var places = Scopes.Select(scope =>
        {
            var row = stored.Single(item => item.Scope == scope);
            return new BudgetPlace(scope, Display(scope), Describe(scope, row.CeilingAmount, row.CeilingCurrency, row.RecordedSpend));
        }).ToList();
        return new BudgetReading(Notice, false, "NOT_SENT", places);
    }

    public static BudgetDecision Apply(
        string? scope,
        decimal? ceilingAmount,
        string? ceilingCurrency,
        decimal? recordedSpend,
        string? reason)
    {
        var name = (scope ?? string.Empty).Trim().ToUpperInvariant();
        if (!Scopes.Contains(name, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The budget scope is not daily, monthly, provider, or prospect.");
        }

        var why = (reason ?? string.Empty).Trim();
        if (why.Length is 0 or > 300)
        {
            throw new InvalidOperationException(
                "A reason of 1 to 300 characters is required for the audit.");
        }

        var ceiling = CheckedMoney(ceilingAmount, ceilingCurrency, "A factory ceiling needs both an amount and a currency. None is invented.");
        if (recordedSpend is < 0)
        {
            throw new InvalidOperationException(
                "Recorded spend cannot be negative. None is invented.");
        }

        var degraded = recordedSpend is not null && recordedSpend.Value >= ceiling.Amount;
        return new BudgetDecision(
            name,
            ceiling.Amount,
            ceiling.Currency,
            recordedSpend,
            why,
            degraded,
            Describe(name, ceiling.Amount, ceiling.Currency, recordedSpend));
    }

    public static string Describe(string scope, decimal? ceiling, string? currency, decimal? spend)
    {
        var name = Display(scope);
        if (ceiling is null)
        {
            return name + " has no ceiling recorded. None was invented. Nothing is purchased. Green does not send. Delivery remains NOT_SENT.";
        }

        var limit = "The operational ceiling for " + name + " is " + Format(ceiling.Value) + " " + currency + ".";
        if (spend is null)
        {
            return limit + " Recorded spend is not on file. None was invented. The ceiling is not compared. Nothing is purchased. This is not an Economics price. Green does not send. Delivery remains NOT_SENT.";
        }

        var observed = " Recorded spend is " + Format(spend.Value) + " " + currency + ".";
        var place = spend.Value >= ceiling.Value
            ? " The ceiling is reached. This scope degrades and further spend on it is refused. Other scopes are unchanged."
            : " The ceiling is open. Other scopes are unchanged.";
        return limit + observed + place + " Nothing is purchased. This is not an Economics price. Green does not send. Delivery remains NOT_SENT.";
    }

    public static string Display(string scope) => scope switch
    {
        Daily => "Daily",
        Monthly => "Monthly",
        Provider => "Provider",
        Prospect => "Prospect",
        _ => scope
    };

    public static string Format(decimal amount) =>
        amount.ToString("0.############################", CultureInfo.InvariantCulture);

    private readonly record struct Money(decimal Amount, string Currency);

    private static Money CheckedMoney(decimal? amount, string? currency, string missingMessage)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
        if (amount is null && code is null)
        {
            throw new InvalidOperationException(missingMessage);
        }

        if (amount is null != code is null)
        {
            throw new InvalidOperationException(missingMessage);
        }

        if (amount is < 0 or 0)
        {
            throw new InvalidOperationException(
                "A factory ceiling must be greater than zero. None is invented.");
        }

        if (code is null || code.Length != 3 || code.Any(character => character is < 'A' or > 'Z'))
        {
            throw new InvalidOperationException(
                "A currency is a three-letter code. None is invented.");
        }

        return new Money(amount ?? 0m, code);
    }
}
