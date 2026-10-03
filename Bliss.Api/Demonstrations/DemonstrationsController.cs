using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Demonstrations;

[ApiController]
[Route("api/demonstrations")]
public sealed class DemonstrationsController(
    ProspectDemonstrationService demonstrations,
    EconomicsAcceptedPriceReader economics,
    EconomicsNegotiationGate negotiation,
    BlissRematchGate rematch,
    WeddingPlannerWakeGate planner,
    BlissDbContext database) : ControllerBase
{
    [HttpPost("factory-batch")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult RunFactoryBatch()
    {
        var batch = demonstrations.RunFactoryBatch();
        return Ok(BatchDto(batch));
    }

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
            memory = demonstrations.MemoryStore,
            clips = library.Clips.OrderByDescending(x => x.AddedAt).Select(ClipDto),
            demonstrations = library.Demonstrations.Select(SummaryDto)
        });
    }

    [HttpPost("flow")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult RegulateFlow([FromBody] FlowRequest? request)
    {
        try
        {
            var library = demonstrations.Library();
            var board = FlowControl.Regulate(
                library.Demonstrations.Select(item => new FlowProspect(item.Slug, item.BusinessName, item.Suppressed)),
                request?.Capacity);
            return Ok(new
            {
                board.Preserved,
                board.Released,
                board.Held,
                board.Withheld,
                board.Discarded,
                board.Capacity,
                board.Notice,
                board.CapacityNotice,
                board.GreenMeansSend,
                board.Delivery,
                assignments = board.Assignments.Select(item => new
                {
                    item.Slug,
                    item.BusinessName,
                    item.Lane,
                    item.Label
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", discarded = 0, greenMeansSend = false });
        }
    }

    [HttpGet("balance")]
    [AllowAnonymous]
    public async Task<ActionResult> Balance(CancellationToken cancellationToken)
    {
        var advertisers = await database.Advertisers.AsNoTracking()
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        var creators = await database.Creators.AsNoTracking()
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        var board = MarketplaceBalance.Read(advertisers, creators);
        return Ok(new
        {
            board.Notice,
            board.Counts,
            board.Pressure,
            board.Revenue,
            board.Inventory,
            board.Skipped,
            board.AdvertiserCount,
            board.CreatorCount,
            board.CensusClaimed,
            board.GreenMeansSend,
            board.Delivery,
            advertisers = board.Advertisers,
            creators = board.Creators
        });
    }

    [HttpGet("creative-inventory")]
    [AllowAnonymous]
    public async Task<ActionResult> CreativeInventoryBoard(CancellationToken cancellationToken)
    {
        var slots = await database.AdInventorySlots.AsNoTracking()
            .Select(item => new StoredSlot(item.ContentItem.Title, item.SlotType))
            .ToListAsync(cancellationToken);
        var board = CreativeInventory.ReadStored(slots);
        return Ok(InventoryBody(board));
    }

    [HttpPost("creative-inventory")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult ReadCreativeInventory([FromBody] CreativeInventoryRequest? request)
    {
        try
        {
            var decision = CreativeInventory.Decide(request?.Pair, request?.CreatorApproved, request?.Stack == true);
            return Ok(new
            {
                decision.Notice,
                decision.Result,
                decision.Pair,
                decision.Accepted,
                decision.StackRefused,
                decision.GreenMeansSend,
                decision.Delivery,
                decision.SlotsChanged
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false, slotsChanged = false });
        }
    }

    [HttpGet("rotation")]
    [AllowAnonymous]
    public async Task<ActionResult> RotationPreview(CancellationToken cancellationToken)
    {
        var names = await database.Advertisers.AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
        var slotCount = await database.AdInventorySlots.CountAsync(cancellationToken);
        var board = RotationAbundance.Preview(names, slotCount);
        return Ok(RotationBody(board));
    }

    [HttpPost("rotation")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> ReadRotation([FromBody] RotationRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var names = await database.Advertisers.AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
            var slotCount = await database.AdInventorySlots.CountAsync(cancellationToken);
            var board = RotationAbundance.Read(request?.Pair, names, request?.CreatorApproved, slotCount);
            return Ok(RotationBody(board));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false, slotsChanged = false });
        }
    }

    [HttpGet("scale-proof")]
    [AllowAnonymous]
    public ActionResult ScaleProofReport()
    {
        var report = ScaleProof.MeasureAll();
        return Ok(new
        {
            report.Passed,
            report.Notice,
            report.Cost,
            report.FactoryTarget,
            report.AiCalls,
            report.ProductionChanged,
            report.Delivery,
            rungs = report.Rungs.Select(item => new
            {
                item.Target,
                item.Measured,
                item.Passed,
                item.Claimed,
                item.ElapsedMilliseconds,
                item.Failure
            })
        });
    }

    [HttpGet("conversation-laboratory")]
    [AllowAnonymous]
    public ActionResult ConversationLaboratoryReport()
    {
        var report = ConversationLaboratory.Run();
        return Ok(new
        {
            report.ScenarioCount,
            report.PassedCount,
            report.Passed,
            report.Notice,
            report.Voice,
            delivery = "NOT_SENT",
            aiCalls = 0,
            scenarios = report.Results.Select(item => new
            {
                item.Id,
                item.Persona,
                item.Prompt,
                item.Reply,
                item.Signal,
                item.Passed,
                item.Missing
            })
        });
    }

    [HttpGet("{slug}/grooming")]
    [AllowAnonymous]
    public ActionResult Grooming(string slug)
    {
        var demonstration = demonstrations.Find(slug);
        if (demonstration is null)
        {
            return NotFound(new { error = "Prospect not found.", delivery = "NOT_SENT", productionChanged = false });
        }

        var report = GroomingReport.Read((demonstration.Events ?? []).Select(item => item.Kind));
        return Ok(new
        {
            report.EventCount,
            counts = report.Counts.Select(item => new { item.Kind, item.Count }),
            report.Friction,
            report.Cost,
            report.Experiments,
            report.NextAction,
            report.Notice,
            report.AiCalls,
            report.ProductionChanged,
            report.Delivery
        });
    }

    [HttpPost("{slug}/grooming/research")]
    [AllowAnonymous]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult StageResearch(string slug, [FromBody] GroomingResearchRequest? request)
    {
        var demonstration = demonstrations.Find(slug);
        if (demonstration is null)
        {
            return NotFound(new { error = "Prospect not found.", delivery = "NOT_SENT", productionChanged = false });
        }

        try
        {
            var reading = GroomingReport.Stage(
                (demonstration.Events ?? []).Select(item => item.Kind),
                request?.Excerpt);
            return Ok(new
            {
                reading.Staged,
                reading.Notice,
                reading.Provenance,
                reading.Hypothesis,
                reading.AiCalls,
                reading.ProductionChanged,
                reading.Delivery
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", productionChanged = false });
        }
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

    [HttpPost("{slug}/contact-roads")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult RecordContactRoad(string slug, [FromBody] ContactRoadRequest request)
    {
        try
        {
            var body = request ?? new ContactRoadRequest(null, null, null);
            var prospect = demonstrations.RecordContactRoad(slug, body.Kind ?? string.Empty, body.Value ?? string.Empty, body.SourceUrl ?? string.Empty);
            return Ok(DetailDto(prospect));
        }
        catch (ContactRoadRejectedException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                accepted = false,
                outreachEligible = false,
                reasons = ex.Result.Reasons
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{slug}/delivery")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult DecideDelivery(string slug, [FromBody] DeliveryRequest request)
    {
        try
        {
            var body = request ?? new DeliveryRequest(null, null, null, null);
            var prospect = demonstrations.DecideDelivery(slug, body.RoadId, body.Policy, body.Adapter, body.Authorization);
            return Ok(DetailDto(prospect));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, outreachEligible = false, delivery = "NOT_SENT" });
        }
    }

    [HttpPost("{slug}/rematch")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Rematch(string slug, [FromBody] RematchRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            if (demonstrations.Find(slug) is null)
            {
                return NotFound(new { error = "Prospect not found.", delivery = "NOT_SENT" });
            }

            var decision = await rematch.SelectAsync(request?.OpportunityId, request?.CurrentCreatorId, cancellationToken);
            var prospect = demonstrations.RememberRematch(slug, decision);
            return Ok(DetailDto(prospect));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT" });
        }
    }

    [HttpPost("{slug}/wedding-planner")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> WakeWeddingPlanner(
        string slug,
        [FromBody] WeddingPlannerWakeRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var prospect = demonstrations.Find(slug);
            if (prospect is null)
            {
                return NotFound(new { error = "Prospect not found.", delivery = "NOT_SENT" });
            }

            var decision = await planner.DecideAsync(prospect, request?.AdvertiserId, cancellationToken);
            var current = demonstrations.RememberWake(slug, decision);
            return Ok(DetailDto(current));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT" });
        }
    }

    [HttpPost("{slug}/progression")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult ReadProgression(string slug)
    {
        try
        {
            var prospect = demonstrations.RememberProgression(slug);
            return Ok(DetailDto(prospect));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false, erased = false });
        }
    }

    [HttpPost("{slug}/suppression")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult Suppress(string slug, [FromBody] SuppressionRequest request)
    {
        try
        {
            var prospect = demonstrations.Suppress(slug, request?.Reason ?? string.Empty);
            return Ok(DetailDto(prospect));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, outreachEligible = false });
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

    [HttpPost("{slug}/events")]
    [AllowAnonymous]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public ActionResult RecordEvent(string slug, [FromBody] AcquisitionEventRequest request)
    {
        if (demonstrations.Find(slug) is null)
        {
            return NotFound();
        }

        try
        {
            var current = demonstrations.RecordEvent(slug, request?.Kind ?? string.Empty, request?.Watcher);
            return Ok(new
            {
                kind = current.Events[^1].Kind,
                observation = current.Events[^1].Observation,
                aiCalls = current.Events[^1].AiCalls,
                delivery = "NOT_SENT",
                events = current.Events.Select(EventDto)
            });
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

    [HttpGet("{slug}/concepts/{conceptId}/source")]
    [AllowAnonymous]
    public ActionResult Source(string slug, string conceptId)
    {
        var path = demonstrations.SourceFile(slug, conceptId);
        return path is null ? NotFound() : PhysicalFile(path, "video/mp4", enableRangeProcessing: true);
    }

    [HttpGet("{slug}/concepts/{conceptId}/qr")]
    [AllowAnonymous]
    public ActionResult Qr(string slug, string conceptId)
    {
        var path = demonstrations.RenderFile(slug, conceptId, qr: true);
        return path is null ? NotFound() : PhysicalFile(path, "image/png");
    }

    [HttpPost("{slug}/economics-price")]
    [AllowAnonymous]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> ReadEconomicsPrice(string slug, [FromBody] EconomicsPriceRequest request, CancellationToken cancellationToken)
    {
        if (demonstrations.Find(slug) is null)
        {
            return NotFound();
        }

        if (!Guid.TryParse(request?.QuoteId, out var quoteId) || quoteId == Guid.Empty)
        {
            return BadRequest(new { error = EconomicsPriceSpeech.MissingQuote });
        }

        var price = await economics.ReadAcceptedAsync(quoteId, cancellationToken);
        var spoken = EconomicsPriceSpeech.Speak(price?.Amount, price?.CurrencyCode);
        if (price is null || spoken is null)
        {
            return BadRequest(new { error = EconomicsPriceSpeech.MissingResult });
        }

        demonstrations.RememberEconomicsQuote(slug, quoteId);
        return Ok(new
        {
            quoteId,
            amount = price.Amount,
            currency = price.CurrencyCode,
            spoken,
            voice = DemonstrationConversation.Voice,
            delivery = "NOT_SENT"
        });
    }

    [HttpPost("{slug}/messages")]
    [AllowAnonymous]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Message(string slug, [FromBody] VisitorMessageRequest request, CancellationToken cancellationToken)
    {
        var prospect = demonstrations.Find(slug);
        if (prospect is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length > 1000)
        {
            return BadRequest(new { error = "Write a message of 1 to 1000 characters." });
        }

        AcceptedEconomicsPrice? price = null;
        PreparedNegotiation? prepared = null;
        if (NegotiationEnvelope.IsNegotiation(request.Text))
        {
            prepared = await negotiation.ResolveAsync(prospect.EconomicsQuoteId, request.Text, cancellationToken);
        }
        else if (Guid.TryParse(prospect.EconomicsQuoteId, out var quoteId))
        {
            price = await economics.ReadAcceptedAsync(quoteId, cancellationToken);
        }

        var reply = demonstrations.Converse(slug, request.Text, price, prepared);
        var current = demonstrations.Find(slug)!;
        return Ok(new
        {
            voice = DemonstrationConversation.Voice,
            reply = reply.Text,
            signal = reply.Signal,
            gear = reply.Gear,
            humanEscalation = current.HumanEscalation,
            messages = current.Messages.Select(MessageDto)
        });
    }

    private static object RotationBody(RotationBoard board) => new
    {
        board.Notice,
        board.Period,
        board.Counts,
        board.Abundance,
        board.Result,
        board.TheoreticalSlots,
        board.PlacedAdvertisers,
        board.OpenSlots,
        board.SlotCount,
        board.Accepted,
        board.GreenMeansSend,
        board.Delivery,
        board.SlotsChanged,
        board.Skipped,
        advertisers = board.Advertisers
    };

    private static object InventoryBody(StoredInventory board) => new
    {
        board.Notice,
        board.Stored,
        board.Rotation,
        board.SlotCount,
        board.GreenMeansSend,
        board.Delivery,
        board.SlotsChanged,
        contents = board.Contents.Select(item => new { item.Title, item.Line })
    };

    private static object BatchDto(FactoryBatchRecord batch) => new
    {
        batch.Id,
        batch.StartedAt,
        batch.FinishedAt,
        batch.ElapsedMilliseconds,
        batch.Status,
        batch.ProspectCount,
        batch.PreservedCount,
        batch.SuppressedCount,
        batch.DemonstrationCount,
        batch.ConceptCount,
        batch.QualifiedClipCount,
        batch.ChecksPassed,
        batch.ExceptionCount,
        batch.AiCalls,
        batch.Cost,
        delivery = "NOT_SENT",
        batch.Exceptions
    };

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
            roadCount = demonstration.ContactRoads.Count,
            suppressed = demonstration.Suppressed,
            outreachEligible = DeliveryEligibility.Open(demonstration),
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
        demonstration.Suppressed,
        demonstration.SuppressionReason,
        outreachEligible = DeliveryEligibility.Open(demonstration),
        contactRoads = (demonstration.ContactRoads ?? []).Select(road => new
        {
            road.Id,
            road.Kind,
            road.Value,
            road.SourceUrl,
            road.State,
            outreachEligible = road.OutreachEligible && !demonstration.Suppressed,
            permission = ContactRoads.NotPermission,
            road.RecordedAt
        }),
        deliveryDecisions = (demonstration.DeliveryDecisions ?? []).Select(item => new
        {
            item.Id,
            item.RoadId,
            item.Policy,
            item.Adapter,
            item.Eligibility,
            item.Transmission,
            item.Notice,
            item.PreparedCopy,
            item.DecidedAt
        }),
        deliveryNotice = (demonstration.DeliveryDecisions ?? []).LastOrDefault()?.Notice ?? string.Empty,
        rematchNotice = demonstration.BlissRematchNotice,
        rematchStatus = demonstration.BlissRematchStatus,
        rematchCreatorName = demonstration.BlissRematchCreatorName,
        rematchScore = demonstration.BlissRematchScore,
        plannerStatus = demonstration.WeddingPlannerStatus,
        plannerNotice = demonstration.WeddingPlannerNotice,
        progressionSignal = demonstration.ProgressionSignal,
        progressionNotice = demonstration.ProgressionNotice,
        progressionNextAction = demonstration.ProgressionNextAction,
        greenMeansSend = false,
        erased = false,
        progressions = (demonstration.Progressions ?? []).Select(item => new
        {
            item.Id,
            item.Signal,
            item.Notice,
            item.NextAction,
            item.RecordedAt
        }),
        plannerWorkspaceId = demonstration.WeddingPlannerWorkspaceId,
        plannerSessionId = demonstration.WeddingPlannerSessionId,
        transmission = DeliveryPolicy.Transmission,
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
            concept.RecipeVersion,
            concept.QaStatus,
            concept.AccentHex,
            servedPicture = string.IsNullOrWhiteSpace(concept.VideoFileName) ? "PLAYER" : "FILE",
            playerNotice = concept.RecipeVersion == RecipeRuntime.Version ? RecipeRuntime.PlayerNotice : null,
            videoUrl = string.IsNullOrWhiteSpace(concept.VideoFileName)
                ? null
                : $"/api/demonstrations/{demonstration.Slug}/concepts/{concept.Id}/video",
            sourceUrl = $"/api/demonstrations/{demonstration.Slug}/concepts/{concept.Id}/source",
            qrUrl = $"/api/demonstrations/{demonstration.Slug}/concepts/{concept.Id}/qr"
        }),
        messages = demonstration.Messages.Select(MessageDto),
        events = (demonstration.Events ?? []).Select(EventDto)
        };
    }

    private static object EventDto(AcquisitionEventRecord item) => new
    {
        item.Id,
        item.Kind,
        item.Observation,
        item.OccurredAt,
        item.AiCalls,
        delivery = "NOT_SENT"
    };

    private static object MessageDto(ChatRecord message) => new
    {
        message.Role,
        message.Text,
        message.Signal,
        message.Gear,
        message.At
    };
}

public sealed record FlowRequest(int? Capacity);

public sealed record CreativeInventoryRequest(int? Pair, bool? CreatorApproved, bool? Stack);

public sealed record RotationRequest(int? Pair, bool? CreatorApproved);
public sealed record GroomingResearchRequest(string? Excerpt);
public sealed record DiscoverRequest(string? Niche, string? Market, string? BusinessName, string? PublicSourceUrl);
public sealed record AcquisitionEventRequest(string? Kind, string? Watcher);
public sealed record ContactRoadRequest(string? Kind, string? Value, string? SourceUrl);
public sealed record DeliveryRequest(string? RoadId, string? Policy, string? Adapter, string? Authorization);
public sealed record RematchRequest(Guid? OpportunityId, Guid? CurrentCreatorId);
public sealed record WeddingPlannerWakeRequest(Guid? AdvertiserId);

internal static class DeliveryEligibility
{
    public static bool Open(DemonstrationRecord demonstration) =>
        !demonstration.Suppressed
        && (demonstration.ContactRoads ?? []).Any(road => road.OutreachEligible);
}
public sealed record SuppressionRequest(string? Reason);
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
public sealed record EconomicsPriceRequest(string? QuoteId);
