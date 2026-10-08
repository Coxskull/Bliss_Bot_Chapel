using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.MissionControl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/mission-control")]
public sealed class MissionControlEvidenceController(MissionControlEvidenceService evidence) : ControllerBase
{
    [HttpGet("evidence")]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken) =>
        Ok(new
        {
            folder = EvidenceIdentity.SubmittedFolder,
            driveStatus = EvidenceIdentity.NotConnected,
            chatgptRetrieval = EvidenceIdentity.NotRun,
            packages = await evidence.ReadAsync(cancellationToken)
        });

    [HttpGet("evidence/{evidenceId}")]
    [AllowAnonymous]
    public async Task<ActionResult> ReadOne(string evidenceId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await evidence.ReadOneAsync(evidenceId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("evidence")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Issue([FromBody] EvidenceIssueRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await evidence.IssueAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("evidence/{evidenceId}/files")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Attach(
        string evidenceId,
        [FromBody] EvidenceFileRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = string.IsNullOrWhiteSpace(request?.ContentBase64)
                ? []
                : Convert.FromBase64String(request.ContentBase64);
            return Ok(await evidence.AttachAsync(evidenceId, request?.EvidenceType, bytes, cancellationToken));
        }
        catch (FormatException)
        {
            return Refuse(new InvalidOperationException("The evidence file was not readable. None was stored."));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("evidence/{evidenceId}/retest")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Retest(
        string evidenceId,
        [FromBody] EvidenceIssueRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await evidence.RetestAsync(evidenceId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("evidence/{evidenceId}/review")]
    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Review(
        string evidenceId,
        [FromBody] EvidenceReviewRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await evidence.ReviewAsync(evidenceId, request?.Reviewer, request?.ReviewerRole, request?.Result, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpGet("validation-catalog")]
    [AllowAnonymous]
    public ActionResult Catalog() =>
        Ok(new
        {
            status = EvidenceIdentity.NotRun,
            notice = "The catalog names existing approved tests. Selecting one does not run it and does not record a pass.",
            tests = EvidenceIdentity.Catalog
        });

    [HttpPost("validation-catalog/select")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult Select([FromBody] EvidenceSelectRequest? request) =>
        Ok(new
        {
            status = EvidenceIdentity.NotRun,
            test = evidence.Select(request?.Seed),
            notice = "Selection does not run the test and does not record a pass."
        });

    [HttpPost("validation-catalog/run")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Run([FromBody] EvidenceSelectRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await evidence.RunValidationAsync(request?.Seed, cancellationToken);
            return Ok(new
            {
                message = "Mission Control evidence ready: " + manifest.EvidenceId,
                manifest.EvidenceId,
                manifest.ClaimedResult,
                manifest.FinalReviewResult,
                manifest.DriveStatus,
                chatgptRetrieval = EvidenceIdentity.NotRun,
                manifest
            });
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    [HttpPost("retrieval-probe")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Probe(CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await evidence.PrepareRetrievalProbeAsync(cancellationToken);
            return Ok(new
            {
                message = "Mission Control evidence ready: " + manifest.EvidenceId,
                manifest.EvidenceId,
                manifest.DriveStatus,
                chatgptRetrieval = EvidenceIdentity.NotRun,
                manifest
            });
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex);
        }
    }

    private static ActionResult Refuse(InvalidOperationException ex) =>
        new BadRequestObjectResult(new
        {
            error = ex.Message,
            delivery = "NOT_SENT",
            campaignReady = false
        });
}

public sealed record EvidenceFileRequest(string? EvidenceType, string? ContentBase64);

public sealed record EvidenceReviewRequest(string? Reviewer, string? ReviewerRole, string? Result);

public sealed record EvidenceSelectRequest(int? Seed);
