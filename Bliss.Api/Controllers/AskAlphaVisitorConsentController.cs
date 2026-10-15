using Bliss.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

// Stage I secure command boundary. Not an implementation of real consent.
// [Authorize] verifies the existing cookie-authenticated principal in hosting;
// the global /api unsafe-method middleware validates CSRF when OIDC is on.
// We intentionally do not inject DB/storage and do not write consent records.
[ApiController]
[Authorize]
[Route("api/ask-alpha/visitor-consent")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AskAlphaVisitorConsentController(
    BlissAuthenticationOptions authentication) : ControllerBase
{
    [HttpGet("readiness")]
    public ActionResult<AskAlphaVisitorConsentDecision> Readiness()
    {
        // Never leak session existence or transcript metadata.
        return Ok(new AskAlphaVisitorConsentDecision(
            "REAL_VISITOR_CONSENT_BACKEND_NOT_CONFIGURED"));
    }

    [HttpPost("opt-in")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public IActionResult OptIn([FromBody] AskAlphaVisitorConsentCommand request)
        => ReviewCommand(request, withdrawal: false);

    [HttpPost("withdraw")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public IActionResult Withdraw([FromBody] AskAlphaVisitorConsentCommand request)
        => ReviewCommand(request, withdrawal: true);

    private IActionResult ReviewCommand(
        AskAlphaVisitorConsentCommand request, bool withdrawal)
    {
        // No writes until independently proven real-visitor ownership and a
        // purpose/versioned, privacy-reviewed consent ledger are available.
        var result = AskAlphaVisitorConsentCommandGate.Inspect(
            User, authentication, request, withdrawal);
        return StatusCode(StatusCodes.Status409Conflict, result);
    }
}
