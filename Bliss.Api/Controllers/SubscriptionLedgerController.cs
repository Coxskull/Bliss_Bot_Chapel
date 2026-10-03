using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/ledger")]
public sealed class SubscriptionLedgerController(SubscriptionLedgerService ledger) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await ledger.ReadAsync(cancellationToken);
        return Ok(Body(board));
    }

    [HttpPost("cost")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Cost([FromBody] LedgerCostRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var board = await ledger.RecordCostAsync(
                request?.ServiceKey,
                request?.Amount,
                request?.Currency,
                request?.Source,
                cancellationToken);
            return Ok(Body(board));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false });
        }
    }

    [HttpPost("review")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Review([FromBody] LedgerReviewRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var board = await ledger.ReviewAsync(
                new GapProposal(
                    request?.ServiceName,
                    request?.Provider,
                    request?.Capability,
                    request?.Classification,
                    request?.EngineeringContract,
                    request?.MonthlyAmount,
                    request?.MonthlyCurrency,
                    request?.UsageCharges,
                    request?.Alternatives,
                    request?.BuildAlternative,
                    request?.WhyAlphaIsInsufficient,
                    request?.EstimatedAmount,
                    request?.EstimatedCurrency,
                    request?.RequiredDate,
                    request?.RequiredOrOptional),
                cancellationToken);
            return Ok(Body(board));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false });
        }
    }

    [HttpPost("budget")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Budget([FromBody] LedgerBudgetRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var board = await ledger.ApplyBudgetAsync(
                request?.Scope,
                request?.CeilingAmount,
                request?.CeilingCurrency,
                request?.RecordedSpend,
                request?.Reason,
                cancellationToken);
            return Ok(Body(board));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false });
        }
    }

    private static object Body(LedgerBoard board) => new
    {
        board.Notice,
        board.BudgetNotice,
        board.Delivery,
        board.GreenMeansSend,
        services = board.Services.Select(item => new
        {
            item.ServiceKey,
            item.DisplayName,
            item.Provider,
            item.Classification,
            item.ClassificationLine,
            item.Status,
            item.AccountOwner,
            item.Plan,
            item.MonthlyAmount,
            item.Currency,
            item.CostLine,
            item.Notice
        }),
        budgets = board.Budgets.Select(item => new
        {
            item.Scope,
            item.DisplayName,
            item.CeilingAmount,
            item.CeilingCurrency,
            item.RecordedSpend,
            item.Degraded,
            item.Notice
        }),
        audits = board.Audits.Select(item => new
        {
            item.Id,
            item.Action,
            item.ServiceKey,
            item.Classification,
            item.Status,
            item.MonthlyAmount,
            item.Currency,
            item.Reason,
            item.Notice,
            item.RecordedAt
        }),
        budgetAudits = board.BudgetAudits.Select(item => new
        {
            item.Id,
            item.Scope,
            item.CeilingAmount,
            item.CeilingCurrency,
            item.RecordedSpend,
            item.Reason,
            item.Notice,
            item.RecordedAt
        })
    };
}

public sealed record LedgerCostRequest(string? ServiceKey, decimal? Amount, string? Currency, string? Source);

public sealed record LedgerReviewRequest(
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

public sealed record LedgerBudgetRequest(
    string? Scope,
    decimal? CeilingAmount,
    string? CeilingCurrency,
    decimal? RecordedSpend,
    string? Reason);
