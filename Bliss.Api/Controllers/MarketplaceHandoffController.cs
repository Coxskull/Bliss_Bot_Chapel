using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/handoff")]
public sealed class MarketplaceHandoffController(MarketplaceHandoffService handoffs) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read([FromQuery] string? tenant, CancellationToken cancellationToken)
    {
        var board = await handoffs.ReadAsync(tenant, cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Hand([FromBody] HandoffRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await handoffs.HandAsync(request?.Tenant, request?.CreatorId ?? Guid.Empty, cancellationToken);
            var board = await handoffs.ReadAsync(request?.Tenant, cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                delivery = "NOT_SENT",
                greenMeansSend = false,
                winClaimed = false,
                evaluatorInvoked = false
            });
        }
    }

    private static object Body(HandoffBoard board, HandoffWrite? write)
    {
        var reading = write?.Reading;
        var row = write?.Row;
        return new
        {
            notice = row?.Notice ?? reading?.Notice ?? MarketplaceHandoff.Notice,
            delivery = "NOT_SENT",
            greenMeansSend = false,
            winClaimed = false,
            evaluator = "DeterministicRuleEvaluator",
            tenant = board.Tenant,
            ruleName = board.Rule?.Name ?? string.Empty,
            duplicate = write?.Duplicate ?? false,
            written = write?.Written ?? false,
            evaluatorInvoked = row?.EvaluatorInvoked ?? reading?.EvaluatorInvoked ?? false,
            matchStatus = row?.MatchStatus ?? reading?.MatchStatus ?? string.Empty,
            overallScore = row?.OverallScore ?? reading?.OverallScore,
            result = row is null && reading is null
                ? null
                : new
                {
                    businessName = row?.BusinessName ?? reading?.BusinessName,
                    creatorName = row?.CreatorName ?? reading?.CreatorName,
                    sourceUrl = row?.SourceUrl ?? reading?.SourceUrl,
                    matchStatus = row?.MatchStatus ?? reading?.MatchStatus,
                    overallScore = row?.OverallScore ?? reading?.OverallScore,
                    evaluatorInvoked = row?.EvaluatorInvoked ?? reading?.EvaluatorInvoked ?? false,
                    winClaimed = false,
                    delivery = "NOT_SENT",
                    notice = row?.Notice ?? reading?.Notice
                },
            advertisers = board.Advertisers.Select(item =>
            {
                var preserved = string.Equals(item.ProspectState, "PRESERVED", StringComparison.Ordinal);
                var qualified = item.Score == 100 && !preserved && !item.Suppressed
                    && !string.IsNullOrWhiteSpace(item.PublicSourceUrl);
                return new
                {
                    tenantKey = item.TenantKey,
                    businessName = item.BusinessName,
                    sourceUrl = item.PublicSourceUrl,
                    score = item.Score,
                    state = item.ProspectState,
                    qualified,
                    reason = qualified
                        ? "Qualified. A public source is stored."
                        : MarketplaceHandoff.WithheldNotice
                };
            }),
            creators = board.Creators
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => new
                {
                    id = item.Id,
                    name = item.Name,
                    countryCode = item.CountryCode,
                    primaryLanguage = item.PrimaryLanguage
                }),
            handoffs = board.Handoffs.Select(item => new
            {
                item.TenantKey,
                item.BusinessName,
                item.SourceUrl,
                item.CreatorId,
                item.CreatorName,
                item.RuleName,
                item.MatchStatus,
                item.OverallScore,
                item.EvaluatorInvoked,
                item.WinClaimed,
                item.Delivery
            })
        };
    }
}

public sealed record HandoffRequest(string? Tenant, Guid CreatorId);
