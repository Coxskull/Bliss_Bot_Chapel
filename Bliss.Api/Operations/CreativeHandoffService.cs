using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Bliss.Domain.Common;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Api.Operations;

public sealed record DecideCandidateApproval(string Stage, string Decision, Guid? ReviewId, Guid? AdInventorySlotId,
    int ExpectedVersion, string Rationale, bool DimensionsVerified, bool FormatVerified, bool CreatorSafeAreaVerified, string IdempotencyKey);
public sealed record PlanCreativeHandoff(Guid CandidateId, Guid ReviewId, Guid AdvertiserApprovalId, Guid AlphaQaApprovalId,
    Guid InventoryFitId, Guid BlissMatchId, Guid CampaignId, Guid AdInventorySlotId, Guid QuoteLineItemId, string IdempotencyKey);
public sealed record CreateHandoffCampaign(Guid CampaignId, Guid BlissMatchId, string Name);

public sealed class CreativeHandoffService(BlissDbContext db, AcademyProductionService production, CampaignPlacementService placements)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);
    private static string Hash(object value) => ControlledAiService.Hash(Serialize(value));
    private static string Text(string? value, int max)
    { var text = (value ?? "").Trim(); if (text.Length is 0 || text.Length > max) throw new InvalidOperationException($"Required text must be at most {max} characters."); return text; }
    private static bool RetryConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation }) return true;
        return false;
    }
    private async Task<(AcademyProductionCandidate Candidate, AcademyProductionBrief Brief)> Candidate(Guid workspace, Guid id, CancellationToken ct)
    {
        var candidate = await db.AcademyProductionCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id &&
            db.AcademyProductionBriefs.Any(b => b.Id == x.BriefId && b.WorkspaceId == workspace), ct)
            ?? throw new WeddingPlannerNotFoundException("Creative candidate was not found.");
        return (candidate, await production.BriefAsync(workspace, candidate.BriefId, ct));
    }
    public async Task<List<CandidateApprovalDecision>> Decisions(Guid workspace, Guid candidate, CancellationToken ct)
    {
        await Candidate(workspace, candidate, ct);
        return await db.CandidateApprovalDecisions.AsNoTracking().Where(x => x.CandidateId == candidate).OrderBy(x => x.CreatedAt).ToListAsync(ct);
    }
    public Task<List<CreativeCampaignHandoff>> Handoffs(Guid workspace, CancellationToken ct) => db.CreativeCampaignHandoffs.AsNoTracking()
        .Where(x => x.WorkspaceId == workspace).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public async Task<(Campaign Campaign, bool Replay)> CreateCampaign(WeddingPlannerWorkspaceResult workspace, CreateHandoffCampaign request, string label, CancellationToken ct)
    {
        if (request.CampaignId == Guid.Empty) throw new InvalidOperationException("A stable campaign request UUID is required.");
        var name = Text(request.Name, 200);
        var match = await db.BlissMatches.AsNoTracking().Include(x => x.AdvertiserOpportunity).SingleOrDefaultAsync(x => x.Id == request.BlissMatchId
            && x.Status == EntityStatuses.Approved && x.AdvertiserOpportunity.Status == EntityStatuses.Active
            && x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == workspace.AdvertiserId, ct)
            ?? throw new WeddingPlannerNotFoundException("This advertiser's approved match was not found.");
        var existing = await db.Campaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CampaignId, ct);
        if (existing is not null)
        {
            if (existing.AdvertiserOpportunityId != match.AdvertiserOpportunityId) throw new WeddingPlannerNotFoundException("Campaign was not found.");
            if (existing.Name != name || existing.Status != EntityStatuses.Draft) throw new WeddingPlannerConflictException("This campaign UUID belongs to different planning intent.");
            return (existing, true);
        }
        var campaign = new Campaign { Id = request.CampaignId, AdvertiserOpportunityId = match.AdvertiserOpportunityId, Name = name, Status = EntityStatuses.Draft, AuthorityStatus = match.AdvertiserOpportunity.AuthorityStatus, CreatedAt = DateTime.UtcNow };
        db.Add(campaign); db.Add(Audit(workspace, "HANDOFF_CAMPAIGN_DRAFT_CREATED", label, campaign.Id));
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) when (RetryConflict(ex)) { throw new WeddingPlannerConflictException("Campaign creation raced another request. Retry the same UUID."); }
        return (campaign, false);
    }
    private async Task<AcademyProductionReview> Reviewed(AcademyProductionCandidate candidate, AcademyProductionBrief brief, Guid? reviewId, CancellationToken ct)
    {
        var brand = await db.WeddingPlannerBrandDna.AsNoTracking().Where(x => x.WorkspaceId == brief.WorkspaceId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (brand?.Id != brief.BrandDnaId || brand.Status != "CONFIRMED") throw new WeddingPlannerConflictException("The brief no longer represents the latest confirmed Brand DNA.");
        if (candidate.Version != await db.AcademyProductionCandidates.Where(x => x.BriefId == brief.Id).MaxAsync(x => x.Version, ct))
            throw new WeddingPlannerConflictException("Approve and plan the latest candidate revision.");
        var review = await db.AcademyProductionReviews.AsNoTracking().Where(x => x.CandidateId == candidate.Id).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (review is null || review.Id != reviewId || review.Decision != "PASS" || review.CriticalDefect
            || new[] { review.Customization, review.QualityParity, review.Originality, review.GeographicAuthenticity }.Any(x => x != "PASS"))
            throw new InvalidOperationException("The exact latest instructor assessment must PASS all four gates without a critical defect.");
        return review;
    }
    private static bool Overlay(string type) => type is InventorySlotTypes.LowerThird or InventorySlotTypes.PerimeterOverlay or InventorySlotTypes.CornerOverlay or InventorySlotTypes.RotatingOverlay;
    public async Task<object> Options(WeddingPlannerWorkspaceResult workspace, CancellationToken ct)
    {
        var matches = await db.BlissMatches.AsNoTracking().Where(x => x.Status == EntityStatuses.Approved && x.AdvertiserOpportunity.Status == EntityStatuses.Active
            && x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == workspace.AdvertiserId)
            .Select(x => new { x.Id, x.CreatorId, x.AdvertiserOpportunityId, creatorName = x.Creator.Name, opportunityName = x.AdvertiserOpportunity.Name }).ToListAsync(ct);
        var creators = matches.Select(x => x.CreatorId).ToList(); var opportunities = matches.Select(x => x.AdvertiserOpportunityId).ToList();
        var slots = await db.AdInventorySlots.AsNoTracking().Where(x => x.IsAvailable && creators.Contains(x.ContentItem.CreatorId))
            .Select(x => new { x.Id, x.ContentItemId, x.ContentItem.CreatorId, x.SlotType, x.DurationSeconds, contentTitle = x.ContentItem.Title }).ToListAsync(ct);
        var campaigns = await db.Campaigns.AsNoTracking().Where(x => x.Status == EntityStatuses.Draft && x.AdvertiserOpportunityId.HasValue && opportunities.Contains(x.AdvertiserOpportunityId.Value))
            .Select(x => new { x.Id, x.Name, x.AdvertiserOpportunityId }).ToListAsync(ct);
        var lines = await db.QuoteLineItems.AsNoTracking().Where(x => x.Quantity == 1 && x.QuoteVersion.Quote.Status == QuoteStatuses.Accepted
            && x.QuoteVersion.VersionNumber == x.QuoteVersion.Quote.CurrentVersionNumber && x.QuoteVersion.Quote.AdvertiserOpportunityId.HasValue
            && opportunities.Contains(x.QuoteVersion.Quote.AdvertiserOpportunityId.Value))
            .Select(x => new { x.Id, x.QuoteVersionId, x.Description, x.Quantity, x.LineAmount, x.CurrencyCode,
                x.RateRecommendation.BlissMatchId, x.RateRecommendation.AdInventorySlotId, x.QuoteVersion.Quote.AdvertiserOpportunityId }).ToListAsync(ct);
        return new { matches, slots = slots.Where(x => Overlay(x.SlotType)).ToList(), campaigns, quoteLines = lines, delivery = "NOT_SENT" };
    }
    public async Task<(CandidateApprovalDecision Row, bool Replay)> Decide(WeddingPlannerWorkspaceResult workspace, Guid candidateId,
        DecideCandidateApproval request, string subject, string label, bool separatePrincipals, CancellationToken ct)
    {
        var (candidate, brief) = await Candidate(workspace.WorkspaceId, candidateId, ct);
        var stage = Text(request.Stage, 128).ToUpperInvariant(); var decision = Text(request.Decision, 128).ToUpperInvariant();
        if (stage is not ("ADVERTISER" or "ALPHA_QA" or "INVENTORY_FIT") || request.ExpectedVersion < 0
            || (stage == "INVENTORY_FIT") != request.AdInventorySlotId.HasValue) throw new InvalidOperationException("Choose a valid approval stage and its inventory scope.");
        var rationale = Text(request.Rationale, 2000); var key = Text(request.IdempotencyKey, 128); CreativeApproval.RequireKey(key);
        var scope = stage == "INVENTORY_FIT" ? $"FIT:{request.AdInventorySlotId}" : stage;
        var fingerprint = Hash(new { stage, decision, request.ReviewId, request.AdInventorySlotId, request.ExpectedVersion, rationale,
            request.DimensionsVerified, request.FormatVerified, request.CreatorSafeAreaVerified, subject });
        var title = $"Candidate {candidate.Version}: {candidate.Label}";
        var semantics = CreativeApproval.Decide(workspace.WorkspaceId, title[..Math.Min(title.Length, 120)], decision,
            stage == "ADVERTISER" ? "ADVERTISER" : "OPERATOR", key, []);
        async Task<CandidateApprovalDecision?> Replay() => await db.CandidateApprovalDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.IdempotencyKey == key, ct);
        var prior = await Replay(); if (prior is not null) { Same(prior.RequestSha256, fingerprint); return (prior, true); }
        var latest = await Latest(candidateId, scope, ct);
        if ((latest?.Version ?? 0) != request.ExpectedVersion)
        {
            var winner = await Replay(); if (winner is not null) { Same(winner.RequestSha256, fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Approval history changed. Refresh before deciding.");
        }
        var width = 0; var height = 0;
        if (decision == "APPROVE")
        {
            await Reviewed(candidate, brief, request.ReviewId, ct);
            if (separatePrincipals && stage is "ADVERTISER" or "ALPHA_QA")
            {
                var other = await Latest(candidateId, stage == "ADVERTISER" ? "ALPHA_QA" : "ADVERTISER", ct);
                if (other?.Decision == "APPROVE" && other.ActorSubject == subject)
                    throw new WeddingPlannerForbiddenException("Advertiser approval and Alpha QA require separate authenticated principals.");
            }
            if (stage == "INVENTORY_FIT")
            {
                var slot = await db.AdInventorySlots.AsNoTracking().Include(x => x.ContentItem).SingleOrDefaultAsync(x => x.Id == request.AdInventorySlotId, ct)
                    ?? throw new WeddingPlannerNotFoundException("Inventory slot was not found.");
                if (!slot.IsAvailable || !Overlay(slot.SlotType)) throw new InvalidOperationException("This PNG candidate requires available image-overlay inventory.");
                if (!await db.BlissMatches.AnyAsync(x => x.CreatorId == slot.ContentItem.CreatorId && x.Status == EntityStatuses.Approved
                    && x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == workspace.AdvertiserId, ct)) throw new WeddingPlannerNotFoundException("Compatible approved creator inventory was not found.");
                CandidatePng.Validate(candidate.ImageBytes);
                width = (int)BinaryPrimitives.ReadUInt32BigEndian(candidate.ImageBytes.AsSpan(16, 4)); height = (int)BinaryPrimitives.ReadUInt32BigEndian(candidate.ImageBytes.AsSpan(20, 4));
                if (width <= 1 || height <= 1 || !request.DimensionsVerified || !request.FormatVerified || !request.CreatorSafeAreaVerified)
                    throw new InvalidOperationException("Document dimensions, format, and creator-safe areas for a real candidate, not a pixel test fixture.");
            }
        }
        else if (request.ReviewId.HasValue && !await db.AcademyProductionReviews.AnyAsync(x => x.Id == request.ReviewId && x.CandidateId == candidateId, ct))
            throw new WeddingPlannerNotFoundException("Instructor assessment was not found.");
        if (decision == "WITHHOLD" && stage == "INVENTORY_FIT" && !await db.AdInventorySlots.AnyAsync(x => x.Id == request.AdInventorySlotId
            && db.BlissMatches.Any(m => m.CreatorId == x.ContentItem.CreatorId && m.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == workspace.AdvertiserId), ct))
            throw new WeddingPlannerNotFoundException("This advertiser's inventory scope was not found.");
        var row = new CandidateApprovalDecision { Id = Guid.NewGuid(), CandidateId = candidateId, ReviewId = request.ReviewId,
            AdInventorySlotId = request.AdInventorySlotId, Stage = stage, ScopeKey = scope, Version = request.ExpectedVersion + 1,
            Decision = decision, Status = semantics.Status, Rationale = rationale, DimensionsVerified = request.DimensionsVerified,
            FormatVerified = request.FormatVerified, CreatorSafeAreaVerified = request.CreatorSafeAreaVerified, ImageWidth = width, ImageHeight = height,
            ActorSubject = Text(subject, 256), ActorLabel = label, IdempotencyKey = key, RequestSha256 = fingerprint, CreatedAt = DateTime.UtcNow };
        var audit = Audit(workspace, "CANDIDATE_APPROVAL_RECORDED", label, row.Id);
        db.Add(row); db.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) when (RetryConflict(ex))
        {
            db.Entry(row).State = EntityState.Detached; db.Entry(audit).State = EntityState.Detached;
            var winner = await Replay(); if (winner is not null) { Same(winner.RequestSha256, fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Approval history changed concurrently. Refresh before deciding.");
        }
        return (row, false);
    }
    private Task<CandidateApprovalDecision?> Latest(Guid candidate, string scope, CancellationToken ct) => db.CandidateApprovalDecisions.AsNoTracking()
        .Where(x => x.CandidateId == candidate && x.ScopeKey == scope).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
    private static void Same(string stored, string current) { if (stored != current) throw new WeddingPlannerConflictException("This key belongs to different creative input. Use a new key."); }
    private static WeddingPlannerAuditEvent Audit(WeddingPlannerWorkspaceResult workspace, string action, string label, Guid rowId) => new() {
        Id = Guid.NewGuid(), AdvertiserId = workspace.AdvertiserId, WorkspaceId = workspace.WorkspaceId, Action = action,
        ActorType = "HUMAN", ActorLabel = label, Outcome = "RECORDED", Detail = $"{action}: {rowId}", OccurredAt = DateTime.UtcNow };
    public async Task<(CreativeCampaignHandoff Row, bool Replay)> Plan(WeddingPlannerWorkspaceResult workspace, PlanCreativeHandoff request,
        string subject, string label, bool separatePrincipals, CancellationToken ct)
    {
        var key = Text(request.IdempotencyKey, 128); var fingerprint = Hash(new { request, subject });
        async Task<CreativeCampaignHandoff?> Replay() => await db.CreativeCampaignHandoffs.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
        var previous = await Replay(); if (previous is not null) { Same(previous.RequestSha256, fingerprint); return (previous, true); }
        IDbContextTransaction? tx = null;
        try
        {
            if (db.Database.IsRelational()) tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var (candidate, brief) = await Candidate(workspace.WorkspaceId, request.CandidateId, ct);
            var review = await Reviewed(candidate, brief, request.ReviewId, ct);
            var advertiser = await Latest(candidate.Id, "ADVERTISER", ct); var qa = await Latest(candidate.Id, "ALPHA_QA", ct);
            var fit = await Latest(candidate.Id, $"FIT:{request.AdInventorySlotId}", ct);
            if (advertiser?.Id != request.AdvertiserApprovalId || qa?.Id != request.AlphaQaApprovalId || fit?.Id != request.InventoryFitId
                || new[] { advertiser, qa, fit }.Any(x => x?.Decision != "APPROVE" || x.ReviewId != review.Id))
                throw new InvalidOperationException("Exact latest advertiser approval, Alpha QA, and inventory fit are required for this assessment.");
            if (separatePrincipals && advertiser!.ActorSubject == qa!.ActorSubject) throw new WeddingPlannerForbiddenException("Advertiser approval and QA must remain independent.");
            var match = await db.BlissMatches.AsNoTracking().Include(x => x.AdvertiserOpportunity).ThenInclude(x => x.AdvertiserProgram)
                .SingleOrDefaultAsync(x => x.Id == request.BlissMatchId, ct) ?? throw new WeddingPlannerNotFoundException("Approved match was not found.");
            if (match.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId != workspace.AdvertiserId) throw new WeddingPlannerNotFoundException("Approved match was not found.");
            var slot = await db.AdInventorySlots.AsNoTracking().Include(x => x.ContentItem).SingleOrDefaultAsync(x => x.Id == request.AdInventorySlotId, ct)
                ?? throw new WeddingPlannerNotFoundException("Inventory slot was not found.");
            if (!slot.IsAvailable || !Overlay(slot.SlotType) || slot.ContentItem.CreatorId != match.CreatorId) throw new InvalidOperationException("Inventory is unavailable or incompatible with this creative and match.");
            var campaign = await db.Campaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.CampaignId && x.AdvertiserOpportunityId == match.AdvertiserOpportunityId, ct)
                ?? throw new WeddingPlannerNotFoundException("This advertiser's campaign was not found.");
            var line = await db.QuoteLineItems.AsNoTracking().Include(x => x.QuoteVersion).ThenInclude(x => x.Quote).Include(x => x.RateRecommendation)
                .SingleOrDefaultAsync(x => x.Id == request.QuoteLineItemId, ct) ?? throw new WeddingPlannerNotFoundException("Economics quote line was not found.");
            var quote = line.QuoteVersion.Quote; var version = line.QuoteVersion; var rate = line.RateRecommendation;
            if (quote.AdvertiserOpportunityId != match.AdvertiserOpportunityId) throw new WeddingPlannerNotFoundException("This advertiser's quote was not found.");
            if (quote.Status != QuoteStatuses.Accepted || quote.CurrentVersionNumber != version.VersionNumber || line.Quantity != 1
                || rate.BlissMatchId != match.Id || rate.AdvertiserOpportunityId != match.AdvertiserOpportunityId || rate.CreatorId != match.CreatorId
                || rate.ContentItemId != slot.ContentItemId || rate.AdInventorySlotId != slot.Id || line.CurrencyCode != version.CurrencyCode
                || !await db.QuoteApprovalDecisions.AnyAsync(x => x.QuoteVersionId == version.Id && x.Decision == QuoteApprovalDecisions.Approved, ct)
                || !await db.QuoteOutcomes.AnyAsync(x => x.QuoteVersionId == version.Id && x.Response == QuoteOutcomeResponses.Accepted, ct))
                throw new InvalidOperationException("An accepted current Economics quote for this exact match and inventory, quantity one, is required.");
            if (await db.CreativeCampaignHandoffs.AnyAsync(x => x.QuoteLineItemId == line.Id, ct)) throw new WeddingPlannerConflictException("This quote line already has a planned creative handoff.");
            var prepared = await placements.PrepareAsync(new CampaignPlacementCommand("CREATIVE_HANDOFF_V1", Hash(new { workspace.WorkspaceId, key }), label,
                match.Id, campaign.Id, slot.ContentItemId, slot.Id), ct);
            if (prepared.IsReplay) throw new WeddingPlannerConflictException("Existing placement intent lacks this handoff lineage. Reconcile before planning.");
            var row = new CreativeCampaignHandoff { Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, CandidateId = candidate.Id, ReviewId = review.Id,
                AdvertiserApprovalId = advertiser!.Id, AlphaQaApprovalId = qa!.Id, InventoryFitId = fit!.Id, QuoteLineItemId = line.Id,
                CampaignPlacementId = prepared.CampaignPlacementId, CampaignPlacementRunId = prepared.RunId, ActorLabel = label, IdempotencyKey = key,
                RequestSha256 = fingerprint, CreatedAt = DateTime.UtcNow,
                SnapshotJson = Serialize(new { candidate.Id, candidate.ImageSha256, brief.BrandDnaId, brief.ReferenceId, brief.ReferenceSha256, reviewId = review.Id,
                    advertiserApproval = advertiser.Id, advertiserSubject = advertiser.ActorSubject, qaApproval = qa.Id, qaSubject = qa.ActorSubject,
                    inventoryFit = fit.Id, fit.ImageWidth, fit.ImageHeight, slotId = slot.Id, slot.SlotType, slot.ContentItemId,
                    matchId = match.Id, match.RuleVersionId, match.OverallScore, match.Status, campaignId = campaign.Id,
                    quoteId = quote.Id, quoteVersionId = version.Id, quoteLineId = line.Id, line.Quantity, line.LineAmount, line.CurrencyCode, rateRecommendationId = rate.Id }) };
            db.Add(row); db.Add(Audit(workspace, "CREATIVE_HANDOFF_PLANNED", label, row.Id));
            await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return (row, false);
        }
        catch (Exception ex) when (RetryConflict(ex))
        {
            if (tx is not null) await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var winner = await Replay(); if (winner is not null) { Same(winner.RequestSha256, fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Creative planning changed concurrently. Refresh and retry the same key.");
        }
        finally { if (tx is not null) await tx.DisposeAsync(); }
    }
}
