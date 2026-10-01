using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Demonstrations;

[ApiController]
[Route("api/demonstrations")]
public sealed class DemonstrationsController(ProspectDemonstrationService demonstrations) : ControllerBase
{
    [HttpGet("library")]
    [AllowAnonymous]
    public ActionResult Library()
    {
        var library = demonstrations.Library();
        var fuel = MediaFuelGauge.From(library.Clips.Select(x => (x.Status, x.QuotaCredit)));
        return Ok(new
        {
            fuel.DailyTarget,
            fuel.Submitted,
            fuel.QualifiedUnique,
            fuel.Duplicates,
            fuel.Rejected,
            fuel.Pending,
            fuel.ReplacementRequired,
            fuel.DailyRemaining,
            fuel.WeeklyTarget,
            fuel.WeeklyQualified,
            fuel.WeeklyRemaining,
            fuel.FuelStatus,
            clips = library.Clips.OrderByDescending(x => x.AddedAt).Select(ClipDto),
            demonstrations = library.Demonstrations.Select(SummaryDto)
        });
    }

    [HttpGet("buying-roles")]
    [AllowAnonymous]
    public ActionResult BuyingRoles([FromQuery] string niche) =>
        Ok(new { niche, roles = BuyingRoleCatalog.RolesFor(niche) });

    [HttpPost("source-media")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(40_000_000)]
    public async Task<ActionResult> Upload(
        IFormFile file,
        [FromForm] string market,
        [FromForm] string country,
        [FromForm] string? culture,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { error = "The upload was empty." });
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var clip = await demonstrations.SaveUploadAsync(
            file.FileName,
            memory.ToArray(),
            market,
            country,
            culture ?? string.Empty,
            "Uploaded source slice.",
            cancellationToken);
        return Ok(ClipDto(clip));
    }

    [HttpPost("source-media/studio-slice")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> StudioSlice(
        [FromBody] StudioSliceRequest request,
        CancellationToken cancellationToken)
    {
        var seconds = request.Seconds is >= 8 and <= 20 ? request.Seconds : 15;
        var clip = await demonstrations.CreateStudioSliceAsync(
            string.IsNullOrWhiteSpace(request.Market) ? ReferenceProspect.Market : request.Market,
            string.IsNullOrWhiteSpace(request.Country) ? ReferenceProspect.Country : request.Country,
            seconds,
            cancellationToken);
        return Ok(ClipDto(clip));
    }

    [HttpPost("abc-pharmacy/produce")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Produce(
        [FromBody] ProduceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var demonstration = await demonstrations.ProduceReferenceAsync(
                $"{Request.Scheme}://{Request.Host}",
                request.SourceClipId,
                request.ConceptCount ?? ReferenceProspect.Concepts.Length,
                cancellationToken);
            return Ok(DetailDto(demonstration));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    public ActionResult Get(string slug)
    {
        var demonstration = demonstrations.Find(slug);
        return demonstration is null ? NotFound() : Ok(DetailDto(demonstration));
    }

    [HttpGet("{slug}/concepts/{conceptId}/video")]
    [AllowAnonymous]
    public ActionResult Video(string slug, string conceptId)
    {
        var path = demonstrations.RenderFile(slug, conceptId, qr: false);
        return path is null ? NotFound() : PhysicalFile(path, "video/mp4", enableRangeProcessing: true);
    }

    [HttpGet("{slug}/concepts/{conceptId}/qr")]
    [AllowAnonymous]
    public ActionResult Qr(string slug, string conceptId)
    {
        var path = demonstrations.RenderFile(slug, conceptId, qr: true);
        return path is null ? NotFound() : PhysicalFile(path, "image/png");
    }

    [HttpPost("{slug}/messages")]
    [AllowAnonymous]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult Message(string slug, [FromBody] VisitorMessageRequest request)
    {
        if (demonstrations.Find(slug) is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 1000)
        {
            return BadRequest(new { error = "Write a message of 1 to 1000 characters." });
        }

        var reply = demonstrations.Converse(slug, request.Text);
        var current = demonstrations.Find(slug)!;
        return Ok(new
        {
            reply = reply.Text,
            signal = reply.Signal,
            humanEscalation = current.HumanEscalation,
            messages = current.Messages.Select(MessageDto)
        });
    }

    private static object ClipDto(SourceClipRecord clip) => new
    {
        clip.Id,
        clip.OriginalFileName,
        clip.Market,
        clip.Country,
        clip.Culture,
        clip.DurationSeconds,
        clip.Status,
        clip.QuotaCredit,
        clip.Provenance,
        clip.Sha256,
        clip.AddedAt
    };

    private static object SummaryDto(DemonstrationRecord demonstration) => new
    {
        demonstration.Slug,
        demonstration.BusinessName,
        demonstration.Market,
        conceptCount = demonstration.Concepts.Count,
        pageUrl = "/demonstrations/" + demonstration.Slug,
        outreachUrl = "/outreach/" + demonstration.Slug
    };

    private static object DetailDto(DemonstrationRecord demonstration) => new
    {
        demonstration.Slug,
        demonstration.BusinessName,
        demonstration.Niche,
        demonstration.Market,
        demonstration.Country,
        demonstration.Language,
        buyingRoles = BuyingRoleCatalog.RolesFor(demonstration.Niche),
        demonstration.DecisionMakerStatus,
        demonstration.ContactTier,
        demonstration.ContactRoute,
        demonstration.Disclosure,
        demonstration.Illustrative,
        demonstration.HumanEscalation,
        demonstration.LastSignal,
        delivery = "NOT_SENT",
        subject = $"See how {demonstration.BusinessName} could reach more customers in {demonstration.Market}",
        concepts = demonstration.Concepts.Select(concept => new
        {
            concept.Id,
            concept.Name,
            concept.Headline,
            concept.Subhead,
            concept.Detail,
            concept.CallToAction,
            concept.QrDestination,
            concept.SourceClipId,
            videoUrl = $"/api/demonstrations/{demonstration.Slug}/concepts/{concept.Id}/video",
            qrUrl = $"/api/demonstrations/{demonstration.Slug}/concepts/{concept.Id}/qr"
        }),
        messages = demonstration.Messages.Select(MessageDto)
    };

    private static object MessageDto(ChatRecord message) => new
    {
        message.Role,
        message.Text,
        message.Signal,
        message.At
    };
}

public sealed record StudioSliceRequest(string? Market, string? Country, int Seconds);
public sealed record ProduceRequest(Guid? SourceClipId, int? ConceptCount);
public sealed record VisitorMessageRequest(string Text);
