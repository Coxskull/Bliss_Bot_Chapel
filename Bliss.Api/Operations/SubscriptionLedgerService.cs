using Bliss.Domain.Entities;
using Bliss.Domain.Operations;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed record RegisterServiceView(
    string ServiceKey,
    string DisplayName,
    string Provider,
    string? Classification,
    string ClassificationLine,
    string Status,
    string AccountOwner,
    string Plan,
    decimal? MonthlyAmount,
    string? Currency,
    string CostLine,
    string Notice);

public sealed record BudgetScopeView(
    string Scope,
    string DisplayName,
    decimal? CeilingAmount,
    string? CeilingCurrency,
    decimal? RecordedSpend,
    bool Degraded,
    string Notice);

public sealed record LedgerAuditView(
    Guid Id,
    string Action,
    string ServiceKey,
    string? Classification,
    string Status,
    decimal? MonthlyAmount,
    string? Currency,
    string Reason,
    string Notice,
    DateTime RecordedAt);

public sealed record BudgetAuditView(
    Guid Id,
    string Scope,
    decimal CeilingAmount,
    string CeilingCurrency,
    decimal? RecordedSpend,
    string Reason,
    string Notice,
    DateTime RecordedAt);

public sealed record LedgerBoard(
    string Notice,
    string BudgetNotice,
    string Delivery,
    bool GreenMeansSend,
    IReadOnlyList<RegisterServiceView> Services,
    IReadOnlyList<BudgetScopeView> Budgets,
    IReadOnlyList<LedgerAuditView> Audits,
    IReadOnlyList<BudgetAuditView> BudgetAudits);

public sealed class SubscriptionLedgerService(BlissDbContext database)
{
    public async Task<LedgerBoard> ReadAsync(CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        var services = await database.SubscriptionRegisterRows.AsNoTracking().ToListAsync(cancellationToken);
        var budgets = await database.FactoryBudgetStates.AsNoTracking().ToListAsync(cancellationToken);
        var audits = await database.SubscriptionLedgerAudits.AsNoTracking()
            .OrderByDescending(item => item.RecordedAt)
            .Take(20)
            .ToListAsync(cancellationToken);
        var budgetAudits = await database.FactoryBudgetAudits.AsNoTracking()
            .OrderByDescending(item => item.RecordedAt)
            .Take(20)
            .ToListAsync(cancellationToken);
        return Present(services, budgets, audits, budgetAudits);
    }

    public async Task<LedgerBoard> RecordCostAsync(
        string? serviceKey,
        decimal? amount,
        string? currency,
        string? source,
        CancellationToken cancellationToken)
    {
        var decision = SubscriptionRegister.RecordCost(serviceKey, amount, currency, source);
        await EnsureAsync(cancellationToken);
        var row = await database.SubscriptionRegisterRows.SingleAsync(
            item => item.ServiceKey == decision.ServiceKey,
            cancellationToken);
        var now = DateTime.UtcNow;
        row.MonthlyAmount = decision.Amount;
        row.Currency = decision.Currency;
        row.Notice = decision.Notice;
        row.UpdatedAt = now;
        database.SubscriptionLedgerAudits.Add(new SubscriptionLedgerAudit
        {
            Id = Guid.NewGuid(),
            Action = "COST",
            ServiceKey = decision.ServiceKey,
            Classification = row.Classification,
            Status = row.Status,
            MonthlyAmount = decision.Amount,
            Currency = decision.Currency,
            Reason = decision.Source,
            Notice = decision.Notice,
            RecordedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        return await ReadAsync(cancellationToken);
    }

    public async Task<LedgerBoard> ReviewAsync(GapProposal? proposal, CancellationToken cancellationToken)
    {
        var decision = SubscriptionRegister.Review(proposal);
        await EnsureAsync(cancellationToken);
        database.SubscriptionLedgerAudits.Add(new SubscriptionLedgerAudit
        {
            Id = Guid.NewGuid(),
            Action = "REVIEW",
            ServiceKey = decision.ServiceName,
            Classification = decision.Classification,
            Status = decision.Status,
            MonthlyAmount = decision.MonthlyAmount,
            Currency = decision.MonthlyCurrency,
            EstimatedAmount = decision.EstimatedAmount,
            EstimatedCurrency = decision.EstimatedCurrency,
            EngineeringContract = decision.EngineeringContract,
            Provider = decision.Provider,
            Capability = decision.Capability,
            UsageCharges = decision.UsageCharges,
            Alternatives = decision.Alternatives,
            BuildAlternative = decision.BuildAlternative,
            WhyAlphaIsInsufficient = decision.WhyAlphaIsInsufficient,
            RequiredDate = decision.RequiredDate,
            RequiredOrOptional = decision.RequiredOrOptional,
            Reason = decision.Reason,
            Notice = decision.Notice,
            RecordedAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return await ReadAsync(cancellationToken);
    }

    public async Task<LedgerBoard> ApplyBudgetAsync(
        string? scope,
        decimal? ceilingAmount,
        string? ceilingCurrency,
        decimal? recordedSpend,
        string? reason,
        CancellationToken cancellationToken)
    {
        var decision = FactoryBudget.Apply(scope, ceilingAmount, ceilingCurrency, recordedSpend, reason);
        await EnsureAsync(cancellationToken);
        var row = await database.FactoryBudgetStates.SingleAsync(item => item.Scope == decision.Scope, cancellationToken);
        var now = DateTime.UtcNow;
        row.CeilingAmount = decision.CeilingAmount;
        row.CeilingCurrency = decision.CeilingCurrency;
        row.RecordedSpend = decision.RecordedSpend;
        row.Notice = decision.Notice;
        row.UpdatedAt = now;
        database.FactoryBudgetAudits.Add(new FactoryBudgetAudit
        {
            Id = Guid.NewGuid(),
            Scope = decision.Scope,
            CeilingAmount = decision.CeilingAmount,
            CeilingCurrency = decision.CeilingCurrency,
            RecordedSpend = decision.RecordedSpend,
            Reason = decision.Reason,
            Notice = decision.Notice,
            RecordedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        return await ReadAsync(cancellationToken);
    }

    private async Task EnsureAsync(CancellationToken cancellationToken)
    {
        var services = await database.SubscriptionRegisterRows.Select(item => item.ServiceKey).ToListAsync(cancellationToken);
        var scopes = await database.FactoryBudgetStates.Select(item => item.Scope).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var known in SubscriptionRegister.Known)
        {
            if (services.Contains(known.Key, StringComparer.Ordinal))
            {
                continue;
            }

            database.SubscriptionRegisterRows.Add(new SubscriptionRegisterRow
            {
                Id = Guid.NewGuid(),
                ServiceKey = known.Key,
                DisplayName = known.DisplayName,
                Provider = known.Provider,
                Capability = known.Capability,
                Classification = known.Classification,
                Status = known.Status,
                AccountOwner = known.AccountOwner,
                Plan = known.Plan,
                Notice = SubscriptionRegister.CostLine(null, null) + " Green does not send. Delivery remains NOT_SENT.",
                UpdatedAt = now
            });
        }

        foreach (var scope in FactoryBudget.Scopes)
        {
            if (scopes.Contains(scope, StringComparer.Ordinal))
            {
                continue;
            }

            database.FactoryBudgetStates.Add(new FactoryBudgetState
            {
                Id = Guid.NewGuid(),
                Scope = scope,
                Notice = FactoryBudget.Describe(scope, null, null, null),
                UpdatedAt = now
            });
        }

        if (database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static LedgerBoard Present(
        IReadOnlyList<SubscriptionRegisterRow> services,
        IReadOnlyList<FactoryBudgetState> budgets,
        IReadOnlyList<SubscriptionLedgerAudit> audits,
        IReadOnlyList<FactoryBudgetAudit> budgetAudits)
    {
        var reading = SubscriptionRegister.Read(services.Select(item =>
            new StoredService(item.ServiceKey, item.Classification, item.MonthlyAmount, item.Currency, item.Status)));
        var budgetReading = FactoryBudget.Read(budgets.Select(item =>
            new StoredBudget(item.Scope, item.CeilingAmount, item.CeilingCurrency, item.RecordedSpend)));
        var ordered = reading.Services.Select(place =>
        {
            var row = services.Single(item => item.ServiceKey == place.Key);
            return new RegisterServiceView(
                row.ServiceKey,
                place.DisplayName,
                row.Provider,
                row.Classification,
                place.ClassificationLine,
                place.Status,
                row.AccountOwner,
                row.Plan,
                row.MonthlyAmount,
                row.Currency,
                place.CostLine,
                row.Notice);
        }).ToList();
        var scopes = budgetReading.Scopes.Select(place =>
        {
            var row = budgets.Single(item => item.Scope == place.Scope);
            var degraded = row.CeilingAmount is not null
                && row.RecordedSpend is not null
                && row.RecordedSpend.Value >= row.CeilingAmount.Value;
            return new BudgetScopeView(
                row.Scope,
                place.DisplayName,
                row.CeilingAmount,
                row.CeilingCurrency,
                row.RecordedSpend,
                degraded,
                place.Notice);
        }).ToList();
        var history = audits.Select(item => new LedgerAuditView(
            item.Id,
            item.Action,
            item.ServiceKey,
            item.Classification,
            item.Status,
            item.MonthlyAmount,
            item.Currency,
            item.Reason,
            item.Notice,
            item.RecordedAt)).ToList();
        var budgetHistory = budgetAudits.Select(item => new BudgetAuditView(
            item.Id,
            item.Scope,
            item.CeilingAmount,
            item.CeilingCurrency,
            item.RecordedSpend,
            item.Reason,
            item.Notice,
            item.RecordedAt)).ToList();
        return new LedgerBoard(
            reading.Notice,
            budgetReading.Notice,
            "NOT_SENT",
            false,
            ordered,
            scopes,
            history,
            budgetHistory);
    }
}
