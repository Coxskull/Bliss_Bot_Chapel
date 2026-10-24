using Bliss.Api.Security;
using Bliss.Api.Runtime;
using Bliss.Domain.Scouting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/scout-compatibility")]
public sealed class ScoutCompatibilityPreviewController(
    IHostEnvironment host,
    BlissAuthenticationOptions authentication) : ControllerBase
{
    [HttpPost("preview")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult<ScoutCompatibilityFinding> Preview([FromBody] ScoutReviewPair? request)
    {
        if (!host.IsStaging() || !authentication.Enabled || User.Identity?.IsAuthenticated != true)
            return StatusCode(403, new { error = "SCOUT_STAGING_AUTH_REQUIRED", delivery = "NOT_SENT" });

        if (request is null)
            return BadRequest(new { error = "SCOUT_REVIEW_PAYLOAD_REQUIRED", delivery = "NOT_SENT" });

        try
        {
            return Ok(ScoutCompatibilityReview.Evaluate(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT" });
        }
    }
}