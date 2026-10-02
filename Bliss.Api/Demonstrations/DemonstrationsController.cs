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

    [HttpGet("catalog")]
    [AllowAnonymous]
    public ActionResult Catalog() =>
        Ok(new
        {
            niches = BuyingRoleCatalog.Niches.OrderBy(x => x),
            markets = InitialMarkets.All.Select(x => new { city = x.City, country = x.Country, language = x.Language })
        });

    [HttpPost("discover")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult Discover([FromBody] DiscoverRequest request)
    {
        try
        {
            var prospect = demonstrations.Discover(
                request.Niche ?? string.Empty,
                request.Market ?? string.Empty,
                request.BusinessName ?? string.Empty,
                request.PublicSourceUrl ?? string.Empty);
            return Ok(DetailDto(prospect));
        }
        catch (DiscoveryRejectedException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                score = ex.Result.Score,
                passesInitialScreen = false,
                reasons = ex.Result.Reasons
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{slug}/decision-maker")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult RecordDecisionMaker(string slug, [FromBody] DecisionMakerRequest request)
    {
        try
        {
            var now = DateTime.UtcNow;
            var body = request ?? new DecisionMakerRequest(null, null, null, null, null, null, null, null, null);
            var prospect = demonstrations.RecordDecisionMaker(slug, new DecisionMakerInput(
                body.PersonName,
                body.Role,
                body.EvidenceKind,
                body.EvidenceUrl,
                body.CorroboratingKind,
                body.CorroboratingUrl,
                body.ContactKind,
                body.ContactValue,
                body.ContactSourceUrl,
                now,
                now));
            return Ok(DetailDto(prospect));
        }
        catch (DecisionMakerRejectedException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                confidence = "UNVERIFIED",
                accepted = false,
                reasons = ex.Result.Reasons
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{slug}/produce")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> ProduceDiscovered(
        string slug,
        [FromBody] ProduceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var demonstration = await demonstrations.ProduceDiscoveredAsync(
                slug,
                $"{Request.Scheme}://{Request.Host}",
                request.SourceClipId,
                cancellationToken);
            return Ok(DetailDto(demonstration));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

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

    private static object SummaryDto(DemonstrationRecord demonstration)
    {
        var presentation = DecisionMakerEvidence.Present(
            demonstration.DecisionMakerStatus,
            demonstration.DecisionMakerName,
            demonstration.LastVerifiedAt,
            DateTime.UtcNow);
        return new
        {
            demonstration.Slug,
            demonstration.BusinessName,
            demonstration.Niche,
            demonstration.Market,
            demonstration.OpportunityScore,
            demonstration.ProspectState,
            demonstration.DecisionMakerStatus,
            demonstration.DecisionMakerName,
            demonstration.ContactTier,
            freshness = presentation.Freshness,
            personalizationAllowed = presentation.PersonalizationAllowed,
            conceptCount = demonstration.Concepts.Count,
            pageUrl = "/demonstrations/" + demonstration.Slug,
            outreachUrl = "/outreach/" + demonstration.Slug
        };
    }

    private static object DetailDto(DemonstrationRecord demonstration)
    {
        var presentation = DecisionMakerEvidence.Present(
            demonstration.DecisionMakerStatus,
            demonstration.DecisionMakerName,
            demonstration.LastVerifiedAt,
            DateTime.UtcNow);
        return new
        {
        demonstration.Slug,
        demonstration.BusinessName,
        demonstration.Niche,
        demonstration.Market,
        demonstration.Country,
        demonstration.Language,
        buyingRoles = BuyingRoleCatalog.Niches.Contains(demonstration.Niche, StringComparer.OrdinalIgnoreCase)
            ? BuyingRoleCatalog.RolesFor(demonstration.Niche)
            : Array.Empty<string>(),
        demonstration.DecisionMakerStatus,
        demonstration.DecisionMakerName,
        demonstration.DecisionMakerRole,
        freshness = presentation.Freshness,
        personalizationAllowed = presentation.PersonalizationAllowed,
        demonstration.EvidenceKind,
        demonstration.EvidenceSourceUrl,
        demonstration.CorroboratingSourceUrl,
        demonstration.ContactTier,
        demonstration.ContactRoute,
        demonstration.ContactType,
        demonstration.ContactValue,
        demonstration.ContactSourceUrl,
        demonstration.ContactVerification,
        demonstration.Disclosure,
        demonstration.PublicSourceUrl,
        demonstration.OpportunityScore,
        demonstration.ProspectState,
        demonstration.BusinessIdentity,
        demonstration.Illustrative,
        demonstration.HumanEscalation,
        demonstration.LastSignal,
        delivery = "NOT_SENT",
        subject = demonstration.ProspectState == "PRESERVED"
            ? $"{demonstration.BusinessName} is preserved. No demonstration was manufactured and nothing was sent."
            : presentation.PersonalizationAllowed
            ? $"For {demonstration.DecisionMakerName}: see how {demonstration.BusinessName} could reach more customers in {demonstration.Market}"
            : $"See how {demonstration.BusinessName} could reach more customers in {demonstration.Market}",
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
    }

    private static object MessageDto(ChatRecord message) => new
    {
        message.Role,
        message.Text,
        message.Signal,
        message.At
    };
}

public sealed record DiscoverRequest(string? Niche, string? Market, string? BusinessName, string? PublicSourceUrl);
public sealed record DecisionMakerRequest(
    string? PersonName,
    string? Role,
    string? EvidenceKind,
    string? EvidenceUrl,
    string? CorroboratingKind,
    string? CorroboratingUrl,
    string? ContactKind,
    string? ContactValue,
    string? ContactSourceUrl);
public sealed record StudioSliceRequest(string? Market, string? Country, int Seconds);
public sealed record ProduceRequest(Guid? SourceClipId, int? ConceptCount);
public sealed record VisitorMessageRequest(string Text);
