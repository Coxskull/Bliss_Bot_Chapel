using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/acceptance")]
public sealed class EconomicsPhaseAcceptanceController(EconomicsPhaseAcceptanceService acceptance) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await acceptance.ReadAsync(cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Accept([FromBody] EconomicsPhaseAcceptanceRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await acceptance.AcceptAsync(
                request?.Phase,
                request?.Reprice ?? false,
                request?.Settle ?? false,
                cancellationToken);
            var board = await acceptance.ReadAsync(cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                delivery = "NOT_SENT",
                greenMeansSend = false,
                repricingAuthorized = false,
                settlementAuthorized = false,
                recommendationRewritten = false,
                historyLine = EconomicsPhaseAcceptance.Unrecorded
            });
        }
    }

    private static object Body(AcceptanceBoard board, AcceptanceWrite? write) => new
    {
        notice = write?.Acceptance.Notice ?? board.Acceptance?.Notice ?? "Economics Phase 9 awaits the owner acceptance. A pricing rule is not changed. An empty history stays unrecorded. Delivery remains NOT_SENT.",
        delivery = "NOT_SENT",
        greenMeansSend = false,
        accepted = write is not null || board.Acceptance is not null,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        phase = EconomicsPhaseAcceptance.Phase,
        phaseKey = EconomicsPhaseAcceptance.PhaseKey,
        placementCount = write?.PlacementCount ?? board.PlacementCount,
        campaignCount = write?.CampaignCount ?? board.CampaignCount,
        historyLine = write?.HistoryLine ?? board.HistoryLine,
        recommendationMatches = write?.RecommendationMatches ?? board.RecommendationMatches,
        repricingAuthorized = false,
        settlementAuthorized = false,
        recommendationRewritten = false
    };
}

public sealed record EconomicsPhaseAcceptanceRequest(int? Phase, bool? Reprice, bool? Settle);
