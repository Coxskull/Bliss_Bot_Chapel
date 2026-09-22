using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerCampaignReadinessHandshakeResult(
    Guid CampaignReadinessHandshakeVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    string Status,
    Guid QaReviewReportVersionId,
    Guid QaAcceptDecisionId,
    Guid ApprovedCreativePackageVersionId,
    string CreativePackageDocumentSha256,
    Guid CreativePackageDecisionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    string SelectedCreativeAssetSha256,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid CampaignPlacementId,
    Guid CampaignPlacementRunId,
    string RulesFindingsJson,
    string Rationale,
    bool DisclaimerAcknowledged,
    bool? SyntheticMarkerAcknowledged,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrent,
    bool IsReplay);

public sealed record WeddingPlannerCampaignReadinessDecisionResult(
    Guid CampaignReadinessDecisionId,
    Guid CampaignReadinessHandshakeVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string Decision,
    string Rationale,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerCampaignReadinessHandshakeResult Version,
    bool IsReplay);

public sealed record WeddingPlannerCampaignReadinessEligibilityResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    bool HasCurrentQaPointer,
    Guid? CurrentAcceptedQaReviewReportVersionId,
    string? CurrentQaStatus,
    bool IsCleanAccepted,
    bool HasCleanAcceptDecision,
    Guid? CurrentApprovedCreativePackageVersionId,
    bool PackageReady,
    bool HasSyntheticUpstream,
    Guid? CurrentCampaignReadinessHandshakeVersionId,
    string NoReservationDisclosure,
    IReadOnlyList<CampaignReadinessMatchCandidate> Candidates);

public sealed record CampaignReadinessMatchCandidate(
    Guid BlissMatchId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    string MatchStatus,
    decimal? OverallScore,
    string OpportunityStatus,
    string OpportunityName,
    IReadOnlyList<CampaignReadinessCampaignCandidate> Campaigns,
    IReadOnlyList<CampaignReadinessContentCandidate> ContentItems);

public sealed record CampaignReadinessCampaignCandidate(
    Guid CampaignId,
    string Name,
    string Status,
    Guid? AdvertiserOpportunityId,
    bool OpportunityCompatible);

public sealed record CampaignReadinessContentCandidate(
    Guid ContentItemId,
    string Title,
    string ContentType,
    IReadOnlyList<CampaignReadinessSlotCandidate> Slots);

public sealed record CampaignReadinessSlotCandidate(
    Guid AdInventorySlotId,
    string SlotType,
    bool IsAvailable,
    string AvailabilityNote);

/// <summary>
/// Deterministic Phase 8 campaign-readiness handshake orchestration.
/// Exactly 0 AI roles/profiles/calls/runs. Operator/admin commit/revoke only.
/// </summary>
public sealed class WeddingPlannerCampaignReadinessOrchestrationService
{
    private readonly BlissDbContext _db;
    private readonly WeddingPlannerService _planner;
    private readonly CampaignPlacementService _placements;
    private readonly IWeddingPlannerCampaignReadinessHostEnvironment _hostEnvironment;

    public WeddingPlannerCampaignReadinessOrchestrationService(
        BlissDbContext db,
        WeddingPlannerService planner,
        CampaignPlacementService placements,
        IWeddingPlannerCampaignReadinessHostEnvironment hostEnvironment)
    {
        _db = db;
        _planner = planner;
        _placements = placements;
        _hostEnvironment = hostEnvironment;
    }

    public bool IsDevelopmentHost => _hostEnvironment.IsDevelopmentHost;

    public async Task<WeddingPlannerCampaignReadinessEligibilityResult> GetEligibilityAsync(
        Guid workspaceId,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadWorkspaceForReadAsync(
            workspaceId, canReadAcrossWorkspaces, boundAdvertiserId, cancellationToken);

        WeddingPlannerQaReviewReportVersion? qa = null;
        if (workspace.CurrentAcceptedQaReviewReportVersionId is Guid qaId)
        {
            qa = await _db.WeddingPlannerQaReviewReportVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == qaId, cancellationToken);
        }

        var cleanAccepted = qa is not null
            && string.Equals(qa.Status, WeddingPlannerQaReviewReportStatuses.Accepted, StringComparison.Ordinal);
        var latestDecision = qa is null
            ? null
            : await _db.WeddingPlannerQaReviewDecisions
                .AsNoTracking()
                .Where(x => x.QaReviewReportVersionId == qa.Id)
                .OrderByDescending(x => x.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);
        var hasCleanAccept = latestDecision is not null
            && string.Equals(latestDecision.Decision, WeddingPlannerQaReviewDecisions.Accept, StringComparison.Ordinal);

        WeddingPlannerCreativePackageVersion? package = null;
        if (workspace.CurrentApprovedCreativePackageVersionId is Guid packageId)
        {
            package = await _db.WeddingPlannerCreativePackageVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == packageId, cancellationToken);
        }

        var packageReady = package is not null
            && qa is not null
            && package.Id == qa.ApprovedCreativePackageVersionId
            && string.Equals(package.Status, WeddingPlannerCreativePackageStatuses.Approved, StringComparison.Ordinal);

        var synthetic = WeddingPlannerCampaignReadinessValidation.DetectSyntheticUpstream(
            package?.DocumentJson,
            qa?.DocumentJson);

        var candidates = await LoadCandidatesAsync(workspace.AdvertiserId, cancellationToken);

        return new WeddingPlannerCampaignReadinessEligibilityResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentAcceptedQaReviewReportVersionId.HasValue,
            workspace.CurrentAcceptedQaReviewReportVersionId,
            qa?.Status,
            cleanAccepted,
            hasCleanAccept,
            workspace.CurrentApprovedCreativePackageVersionId,
            packageReady,
            synthetic,
            workspace.CurrentCampaignReadinessHandshakeVersionId,
            WeddingPlannerCampaignReadinessDisclosures.NoReservation,
            candidates);
    }

    public async Task<IReadOnlyList<WeddingPlannerCampaignReadinessHandshakeResult>> ListHandshakesAsync(
        Guid workspaceId,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await LoadWorkspaceForReadAsync(
            workspaceId, canReadAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var rows = await _db.WeddingPlannerCampaignReadinessHandshakeVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);
        return rows.Select(x => ToHandshakeResult(
            x,
            workspace.CurrentCampaignReadinessHandshakeVersionId == x.Id,
            false)).ToList();
    }

    public async Task<WeddingPlannerCampaignReadinessHandshakeResult> GetHandshakeAsync(
        Guid handshakeId,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerCampaignReadinessHandshakeVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshakeId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Campaign readiness handshake was not found.");

        await EnsureHandshakeReadAccessAsync(
            version.AdvertiserId, canReadAcrossWorkspaces, boundAdvertiserId, cancellationToken);

        var workspace = await _db.WeddingPlannerWorkspaces
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);

        return ToHandshakeResult(
            version,
            workspace.CurrentCampaignReadinessHandshakeVersionId == version.Id,
            false);
    }

    public async Task<IReadOnlyList<WeddingPlannerCampaignReadinessDecisionResult>> ListDecisionsAsync(
        Guid handshakeId,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await GetHandshakeAsync(
            handshakeId, canReadAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var decisions = await _db.WeddingPlannerCampaignReadinessDecisions
            .AsNoTracking()
            .Where(x => x.CampaignReadinessHandshakeVersionId == handshakeId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
        return decisions.Select(d => new WeddingPlannerCampaignReadinessDecisionResult(
            d.Id,
            d.CampaignReadinessHandshakeVersionId,
            d.AdvertiserId,
            d.WorkspaceId,
            d.Decision,
            d.Rationale,
            d.ActorType,
            d.ActorLabel,
            d.SourceSystem,
            d.IdempotencyKey,
            d.OccurredAt,
            version,
            false)).ToList();
    }

    public async Task<WeddingPlannerCampaignReadinessHandshakeResult> CommitAsync(
        Guid workspaceId,
        Guid blissMatchId,
        Guid campaignId,
        Guid contentItemId,
        Guid adInventorySlotId,
        string rationale,
        bool disclaimerAcknowledged,
        bool? syntheticMarkerAcknowledged,
        JsonNode? rawBody,
        string sourceSystem,
        string idempotencyKey,
        bool canCommit,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (!canCommit)
        {
            throw new WeddingPlannerForbiddenException(
                "Only operator or admin may commit campaign-readiness handshakes.");
        }

        if (rawBody is not null)
        {
            WeddingPlannerCampaignReadinessValidation.RejectForbiddenCommitFields(rawBody);
        }

        WeddingPlannerCampaignReadinessValidation.ValidateCommitAcknowledgements(
            disclaimerAcknowledged, rationale);
        var normalizedRationale = WeddingPlannerCampaignReadinessValidation.NormalizeRationale(rationale);
        var (source, key, placementKey, markKey) =
            WeddingPlannerCampaignReadinessValidation.NormalizeCommitKeys(sourceSystem, idempotencyKey);

        // Idempotent replay pre-check outside the write transaction.
        var existing = await _db.WeddingPlannerCampaignReadinessHandshakeVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            EnsureCommitReplayMatches(
                existing,
                workspaceId,
                blissMatchId,
                campaignId,
                contentItemId,
                adInventorySlotId,
                normalizedRationale,
                disclaimerAcknowledged,
                syntheticMarkerAcknowledged);
            await EnsureHandshakeReadAccessAsync(
                existing.AdvertiserId, canReadAcrossWorkspaces, boundAdvertiserId, cancellationToken);
            var workspaceReplay = await _db.WeddingPlannerWorkspaces
                .AsNoTracking()
                .SingleAsync(x => x.Id == existing.WorkspaceId, cancellationToken);
            // Exact replay writes nothing (no audit).
            return ToHandshakeResult(
                existing,
                workspaceReplay.CurrentCampaignReadinessHandshakeVersionId == existing.Id,
                true);
        }

        if (await _db.WeddingPlannerCampaignReadinessDecisions.AsNoTracking()
                .AnyAsync(x => x.SourceSystem == source && x.IdempotencyKey == markKey, cancellationToken))
        {
            WeddingPlannerCampaignReadinessValidation.ThrowIdempotencyConflict(
                "derived MARK key already exists.");
        }

        if (await _db.CampaignPlacementRuns.AsNoTracking()
                .AnyAsync(x => x.SourceSystem == source && x.IdempotencyKey == placementKey, cancellationToken))
        {
            WeddingPlannerCampaignReadinessValidation.ThrowIdempotencyConflict(
                "derived PLACEMENT key already exists.");
        }

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken)
            : null;

        try
        {
            var workspace = await _db.WeddingPlannerWorkspaces
                .SingleOrDefaultAsync(x => x.Id == workspaceId, cancellationToken)
                ?? throw new WeddingPlannerNotFoundException("Wedding Planner workspace was not found.");

            if (!canReadAcrossWorkspaces
                && boundAdvertiserId is Guid bound
                && bound != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Wedding Planner workspace was not found.");
            }

            if (workspace.CurrentCampaignReadinessHandshakeVersionId is not null)
            {
                throw new InvalidOperationException(
                    "Current campaign-readiness handshake pointer is set; revoke or clear first.");
            }

            var evaluation = await EvaluateCommitContextAsync(
                workspace,
                blissMatchId,
                campaignId,
                contentItemId,
                adInventorySlotId,
                syntheticMarkerAcknowledged,
                cancellationToken);

            if (!string.Equals(
                    evaluation.Rules.OverallSeverity,
                    WeddingPlannerCampaignReadinessFindingSeverities.Pass,
                    StringComparison.Ordinal))
            {
                var blocked = string.Join(
                    ", ",
                    evaluation.Rules.Findings
                        .Where(f => f.Severity == WeddingPlannerCampaignReadinessFindingSeverities.Block)
                        .Select(f => f.Code));
                throw new InvalidOperationException(
                    $"campaign-readiness-rules.v1 overallSeverity is BLOCK ({blocked}).");
            }

            var placementResult = await _placements.BindCoreInternalAsync(
                new CampaignPlacementCommand(
                    source,
                    placementKey,
                    actorLabel,
                    blissMatchId,
                    campaignId,
                    contentItemId,
                    adInventorySlotId),
                cancellationToken);

            var nextVersion = await NextVersionNumberAsync(workspace.Id, cancellationToken);
            var qaDocumentSha = WeddingPlannerCampaignReadinessValidation.Sha256Hex(
                evaluation.QaReport.DocumentJson);
            var documentJson = WeddingPlannerCampaignReadinessValidation.BuildCanonicalHandshakeDocument(
                evaluation.Rules,
                evaluation.QaReport.Id,
                qaDocumentSha,
                evaluation.LatestQaDecision.Id,
                evaluation.Package.Id,
                evaluation.PackageSha,
                evaluation.LatestCreativeApprove.Id,
                evaluation.QaReport.SelectedVariantId,
                evaluation.SelectedAsset.Id,
                evaluation.SelectedAsset.Sha256,
                evaluation.SelectedAsset.ByteSize,
                evaluation.SelectedAsset.Width,
                evaluation.SelectedAsset.Height,
                evaluation.Package.ApprovedConceptPackageVersionId,
                evaluation.Package.SelectedConceptId,
                evaluation.Package.ApprovedBrandDnaVersionId,
                evaluation.Package.ApprovedBrandDnaVersionNumber,
                evaluation.Package.ApprovedColorProfileVersionId,
                evaluation.Package.ApprovedColorProfileVersionNumber,
                evaluation.Package.ApprovedResearchReportVersionId,
                evaluation.Package.ApprovedResearchReportVersionNumber,
                evaluation.Match.Id,
                evaluation.Match.CreatorId,
                evaluation.Match.AdvertiserOpportunityId,
                evaluation.Match.RuleVersionId,
                evaluation.Campaign.Id,
                evaluation.Content.Id,
                evaluation.Slot.Id,
                placementResult.CampaignPlacementId,
                placementResult.RunId,
                evaluation.QaReport.Status,
                evaluation.Package.Status,
                evaluation.Match.Status,
                evaluation.Match.OverallScore,
                evaluation.Opportunity.Status,
                evaluation.Campaign.Status,
                evaluation.Content.Title,
                evaluation.Content.ContentType,
                evaluation.Slot.SlotType,
                evaluation.Slot.StartSecond,
                evaluation.Slot.DurationSeconds,
                evaluation.Slot.IsAvailable,
                evaluation.CreatorName,
                evaluation.Opportunity.Name,
                normalizedRationale,
                true,
                evaluation.HasSyntheticUpstream ? true : syntheticMarkerAcknowledged,
                evaluation.HasSyntheticUpstream);

            var summary =
                $"{WeddingPlannerSchemaVersions.CampaignReadinessHandshakeV1}; " +
                $"match={evaluation.Match.Id:N}; campaign={evaluation.Campaign.Id:N}; " +
                $"placement={placementResult.CampaignPlacementId:N}; rules=PASS";

            var handshake = new WeddingPlannerCampaignReadinessHandshakeVersion
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                VersionNumber = nextVersion,
                SchemaVersion = WeddingPlannerSchemaVersions.CampaignReadinessHandshakeV1,
                DocumentJson = documentJson,
                Summary = summary.Length > 2000 ? summary[..2000] : summary,
                Status = WeddingPlannerCampaignReadinessHandshakeStatuses.CampaignReady,
                QaReviewReportVersionId = evaluation.QaReport.Id,
                QaReviewReportDocumentSha256 = qaDocumentSha,
                QaAcceptDecisionId = evaluation.LatestQaDecision.Id,
                ApprovedCreativePackageVersionId = evaluation.Package.Id,
                CreativePackageDocumentSha256 = evaluation.PackageSha,
                CreativePackageDecisionId = evaluation.LatestCreativeApprove.Id,
                SelectedVariantId = evaluation.QaReport.SelectedVariantId,
                SelectedCreativeAssetId = evaluation.SelectedAsset.Id,
                SelectedCreativeAssetSha256 = evaluation.SelectedAsset.Sha256.ToLowerInvariant(),
                SelectedCreativeAssetByteSize = evaluation.SelectedAsset.ByteSize,
                SelectedCreativeAssetWidth = evaluation.SelectedAsset.Width,
                SelectedCreativeAssetHeight = evaluation.SelectedAsset.Height,
                ApprovedConceptPackageVersionId = evaluation.Package.ApprovedConceptPackageVersionId,
                SelectedConceptId = evaluation.Package.SelectedConceptId,
                ApprovedBrandDnaVersionId = evaluation.Package.ApprovedBrandDnaVersionId,
                ApprovedBrandDnaVersionNumber = evaluation.Package.ApprovedBrandDnaVersionNumber,
                ApprovedColorProfileVersionId = evaluation.Package.ApprovedColorProfileVersionId,
                ApprovedColorProfileVersionNumber = evaluation.Package.ApprovedColorProfileVersionNumber,
                ApprovedResearchReportVersionId = evaluation.Package.ApprovedResearchReportVersionId,
                ApprovedResearchReportVersionNumber = evaluation.Package.ApprovedResearchReportVersionNumber,
                BlissMatchId = evaluation.Match.Id,
                CreatorId = evaluation.Match.CreatorId,
                AdvertiserOpportunityId = evaluation.Match.AdvertiserOpportunityId,
                RuleVersionId = evaluation.Match.RuleVersionId,
                MatchStatusSnapshot = evaluation.Match.Status,
                MatchOverallScoreSnapshot = evaluation.Match.OverallScore,
                OpportunityStatusSnapshot = evaluation.Opportunity.Status,
                CampaignId = evaluation.Campaign.Id,
                ContentItemId = evaluation.Content.Id,
                AdInventorySlotId = evaluation.Slot.Id,
                CampaignPlacementId = placementResult.CampaignPlacementId,
                CampaignPlacementRunId = placementResult.RunId,
                RulesFindingsJson = evaluation.Rules.DocumentJson,
                Rationale = normalizedRationale,
                DisclaimerAcknowledged = true,
                SyntheticMarkerAcknowledged = evaluation.HasSyntheticUpstream ? true : syntheticMarkerAcknowledged,
                SourceSystem = source,
                IdempotencyKey = key,
                ActorType = actorType,
                ActorLabel = actorLabel,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerCampaignReadinessHandshakeVersions.Add(handshake);

            var markDecision = new WeddingPlannerCampaignReadinessDecision
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                CampaignReadinessHandshakeVersionId = handshake.Id,
                Decision = WeddingPlannerCampaignReadinessDecisions.MarkCampaignReady,
                Rationale = normalizedRationale,
                ActorType = actorType,
                ActorLabel = actorLabel,
                SourceSystem = source,
                IdempotencyKey = markKey,
                OccurredAt = DateTime.UtcNow
            };
            _db.WeddingPlannerCampaignReadinessDecisions.Add(markDecision);

            workspace.CurrentCampaignReadinessHandshakeVersionId = handshake.Id;
            workspace.UpdatedAt = DateTime.UtcNow;

            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId, workspace.Id, null, null,
                WeddingPlannerAuditActions.CampaignReadinessRulesFindingsRecorded,
                actorType, actorLabel, WeddingPlannerOutcomes.Succeeded, requestId,
                $"Campaign readiness rules PASS for handshake {handshake.Id}.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId, workspace.Id, null, null,
                WeddingPlannerAuditActions.CampaignReadinessPlacementCreated,
                actorType, actorLabel, WeddingPlannerOutcomes.Created, requestId,
                $"Planned placement {placementResult.CampaignPlacementId} via Phase 8 path.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId, workspace.Id, null, null,
                WeddingPlannerAuditActions.CampaignReadinessMarkDecisionRecorded,
                actorType, actorLabel, WeddingPlannerOutcomes.Created, requestId,
                $"MARK_CAMPAIGN_READY decision {markDecision.Id} recorded.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId, workspace.Id, null, null,
                WeddingPlannerAuditActions.CampaignReadinessPointerSet,
                actorType, actorLabel, WeddingPlannerOutcomes.Created, requestId,
                $"CurrentCampaignReadinessHandshakeVersionId set to {handshake.Id}.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId, workspace.Id, null, null,
                WeddingPlannerAuditActions.CampaignReadinessHandshakeCommitted,
                actorType, actorLabel, WeddingPlannerOutcomes.Created, requestId,
                $"Campaign readiness handshake {handshake.Id} committed.");

            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return ToHandshakeResult(handshake, true, false);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<WeddingPlannerCampaignReadinessDecisionResult> RevokeAsync(
        Guid handshakeId,
        string decision,
        string rationale,
        JsonNode? rawBody,
        string sourceSystem,
        string idempotencyKey,
        bool canRevoke,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (!canRevoke)
        {
            throw new WeddingPlannerForbiddenException(
                "Only operator or admin may revoke campaign-readiness handshakes.");
        }

        if (rawBody is not null)
        {
            WeddingPlannerCampaignReadinessValidation.RejectForbiddenRevokeFields(rawBody);
        }

        WeddingPlannerCampaignReadinessValidation.ValidateRevokeDecision(decision, rationale);
        var normalizedRationale = WeddingPlannerCampaignReadinessValidation.NormalizeRationale(rationale);
        var source = WeddingPlannerCampaignReadinessValidation.Required(sourceSystem, nameof(sourceSystem), 64)
            .ToUpperInvariant();
        var key = WeddingPlannerCampaignReadinessValidation.Required(idempotencyKey, nameof(idempotencyKey), 128);

        // Idempotent replay pre-check outside the write transaction.
        var existingDecision = await _db.WeddingPlannerCampaignReadinessDecisions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existingDecision is not null)
        {
            EnsureRevokeReplayMatches(existingDecision, handshakeId, normalizedRationale);
            var existingVersion = await GetHandshakeAsync(
                existingDecision.CampaignReadinessHandshakeVersionId,
                canReadAcrossWorkspaces,
                boundAdvertiserId,
                cancellationToken);
            return new WeddingPlannerCampaignReadinessDecisionResult(
                existingDecision.Id,
                existingDecision.CampaignReadinessHandshakeVersionId,
                existingDecision.AdvertiserId,
                existingDecision.WorkspaceId,
                existingDecision.Decision,
                existingDecision.Rationale,
                existingDecision.ActorType,
                existingDecision.ActorLabel,
                existingDecision.SourceSystem,
                existingDecision.IdempotencyKey,
                existingDecision.OccurredAt,
                existingVersion,
                true);
        }

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken)
            : null;

        try
        {
            var version = await _db.WeddingPlannerCampaignReadinessHandshakeVersions
                .SingleOrDefaultAsync(x => x.Id == handshakeId, cancellationToken)
                ?? throw new WeddingPlannerNotFoundException("Campaign readiness handshake was not found.");

            await EnsureHandshakeReadAccessAsync(
                version.AdvertiserId, canReadAcrossWorkspaces, boundAdvertiserId, cancellationToken);

            if (!string.Equals(
                    version.Status,
                    WeddingPlannerCampaignReadinessHandshakeStatuses.CampaignReady,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only CAMPAIGN_READY handshakes can be revoked.");
            }

            version.Status = WeddingPlannerCampaignReadinessHandshakeStatuses.Revoked;
            var revokeRow = new WeddingPlannerCampaignReadinessDecision
            {
                Id = Guid.NewGuid(),
                AdvertiserId = version.AdvertiserId,
                WorkspaceId = version.WorkspaceId,
                CampaignReadinessHandshakeVersionId = version.Id,
                Decision = WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
                Rationale = normalizedRationale,
                ActorType = actorType,
                ActorLabel = actorLabel,
                SourceSystem = source,
                IdempotencyKey = key,
                OccurredAt = DateTime.UtcNow
            };
            _db.WeddingPlannerCampaignReadinessDecisions.Add(revokeRow);

            var workspaceEntity = await _db.WeddingPlannerWorkspaces
                .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);
            var clearedPointer = false;
            if (workspaceEntity.CurrentCampaignReadinessHandshakeVersionId == version.Id)
            {
                workspaceEntity.CurrentCampaignReadinessHandshakeVersionId = null;
                workspaceEntity.UpdatedAt = DateTime.UtcNow;
                clearedPointer = true;
            }

            _planner.AddAuditForOrchestration(
                version.AdvertiserId, version.WorkspaceId, null, null,
                WeddingPlannerAuditActions.CampaignReadinessRevoked,
                actorType, actorLabel, WeddingPlannerOutcomes.Rejected, requestId,
                $"Campaign readiness handshake {version.Id} revoked; placement/run unchanged.");
            if (clearedPointer)
            {
                _planner.AddAuditForOrchestration(
                    version.AdvertiserId, version.WorkspaceId, null, null,
                    WeddingPlannerAuditActions.CampaignReadinessPointerCleared,
                    actorType, actorLabel, WeddingPlannerOutcomes.Rejected, requestId,
                    "CurrentCampaignReadinessHandshakeVersionId cleared on revoke.");
            }

            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new WeddingPlannerCampaignReadinessDecisionResult(
                revokeRow.Id,
                version.Id,
                version.AdvertiserId,
                version.WorkspaceId,
                revokeRow.Decision,
                revokeRow.Rationale,
                revokeRow.ActorType,
                revokeRow.ActorLabel,
                revokeRow.SourceSystem,
                revokeRow.IdempotencyKey,
                revokeRow.OccurredAt,
                ToHandshakeResult(version, false, false),
                false);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            _db.ChangeTracker.Clear();
            throw;
        }
    }

    private static void EnsureCommitReplayMatches(
        WeddingPlannerCampaignReadinessHandshakeVersion existing,
        Guid workspaceId,
        Guid blissMatchId,
        Guid campaignId,
        Guid contentItemId,
        Guid adInventorySlotId,
        string normalizedRationale,
        bool disclaimerAcknowledged,
        bool? syntheticMarkerAcknowledged)
    {
        if (existing.WorkspaceId != workspaceId
            || existing.BlissMatchId != blissMatchId
            || existing.CampaignId != campaignId
            || existing.ContentItemId != contentItemId
            || existing.AdInventorySlotId != adInventorySlotId
            || !string.Equals(existing.Rationale, normalizedRationale, StringComparison.Ordinal)
            || existing.DisclaimerAcknowledged != disclaimerAcknowledged
            || !WeddingPlannerCampaignReadinessValidation.SyntheticAckMatches(
                existing.SyntheticMarkerAcknowledged,
                syntheticMarkerAcknowledged))
        {
            WeddingPlannerCampaignReadinessValidation.ThrowIdempotencyConflict(
                "existing handshake source/key does not match request workspace and selection.");
        }
    }

    private static void EnsureRevokeReplayMatches(
        WeddingPlannerCampaignReadinessDecision existingDecision,
        Guid handshakeId,
        string normalizedRationale)
    {
        if (string.Equals(
                existingDecision.Decision,
                WeddingPlannerCampaignReadinessDecisions.MarkCampaignReady,
                StringComparison.Ordinal))
        {
            WeddingPlannerCampaignReadinessValidation.ThrowIdempotencyConflict(
                "key collides with MARK_CAMPAIGN_READY decision.");
        }

        if (existingDecision.CampaignReadinessHandshakeVersionId != handshakeId
            || !string.Equals(
                existingDecision.Decision,
                WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
                StringComparison.Ordinal)
            || !string.Equals(existingDecision.Rationale, normalizedRationale, StringComparison.Ordinal))
        {
            WeddingPlannerCampaignReadinessValidation.ThrowIdempotencyConflict(
                "existing decision source/key does not match revoke request.");
        }
    }

    private async Task<CommitEvaluation> EvaluateCommitContextAsync(
        WeddingPlannerWorkspace workspace,
        Guid blissMatchId,
        Guid campaignId,
        Guid contentItemId,
        Guid adInventorySlotId,
        bool? syntheticMarkerAcknowledged,
        CancellationToken cancellationToken)
    {
        WeddingPlannerQaReviewReportVersion? qa = null;
        if (workspace.CurrentAcceptedQaReviewReportVersionId is Guid qaId)
        {
            qa = await _db.WeddingPlannerQaReviewReportVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == qaId, cancellationToken);
        }

        var latestQaDecision = qa is null
            ? null
            : await _db.WeddingPlannerQaReviewDecisions
                .AsNoTracking()
                .Where(x => x.QaReviewReportVersionId == qa.Id)
                .OrderByDescending(x => x.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

        WeddingPlannerCreativePackageVersion? package = null;
        if (workspace.CurrentApprovedCreativePackageVersionId is Guid packageId)
        {
            package = await _db.WeddingPlannerCreativePackageVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == packageId, cancellationToken);
        }

        var packageSha = package is null
            ? null
            : WeddingPlannerCampaignReadinessValidation.Sha256Hex(package.DocumentJson);

        WeddingPlannerCreativePackageDecision? latestApprove = null;
        WeddingPlannerCreativeAsset? selectedAsset = null;
        var assetsForVariant = 0;
        SelectedVariantQaSnapshot? selectedVariantSnapshot = null;
        WeddingPlannerConceptPackageVersion? concept = null;
        var selectedConceptPresent = false;
        WeddingPlannerBrandDnaVersion? dna = null;
        WeddingPlannerColorProfileVersion? color = null;
        WeddingPlannerResearchReportVersion? research = null;

        if (package is not null && qa is not null)
        {
            latestApprove = await _db.WeddingPlannerCreativePackageDecisions
                .AsNoTracking()
                .Where(x => x.CreativePackageVersionId == package.Id
                            && x.Decision == WeddingPlannerCreativePackageDecisions.Approve)
                .OrderByDescending(x => x.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            selectedVariantSnapshot = WeddingPlannerQaRulesEngine.ExtractSelectedVariant(
                package.DocumentJson, qa.SelectedVariantId);

            assetsForVariant = await _db.WeddingPlannerCreativeAssets
                .AsNoTracking()
                .CountAsync(
                    x => x.CreativePackageVersionId == package.Id && x.VariantId == qa.SelectedVariantId,
                    cancellationToken);

            selectedAsset = await _db.WeddingPlannerCreativeAssets
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == qa.SelectedCreativeAssetId, cancellationToken);

            concept = await _db.WeddingPlannerConceptPackageVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == package.ApprovedConceptPackageVersionId, cancellationToken);
            selectedConceptPresent = WeddingPlannerCampaignReadinessRulesEngine.TrySelectedConceptPresentInPackage(
                concept?.DocumentJson,
                package.SelectedConceptId);
            dna = await _db.WeddingPlannerBrandDnaVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == package.ApprovedBrandDnaVersionId, cancellationToken);
            color = await _db.WeddingPlannerColorProfileVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == package.ApprovedColorProfileVersionId, cancellationToken);
            research = await _db.WeddingPlannerResearchReportVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == package.ApprovedResearchReportVersionId, cancellationToken);
        }

        var match = await _db.BlissMatches
            .AsNoTracking()
            .Include(x => x.Creator)
            .Include(x => x.AdvertiserOpportunity)
            .ThenInclude(x => x.AdvertiserProgram)
            .SingleOrDefaultAsync(x => x.Id == blissMatchId, cancellationToken);
        var opportunity = match?.AdvertiserOpportunity;
        Guid? programAdvertiserId = opportunity?.AdvertiserProgram?.AdvertiserId;
        var creatorName = match?.Creator?.Name;

        var campaign = await _db.Campaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == campaignId, cancellationToken);
        var content = await _db.ContentItems
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == contentItemId, cancellationToken);
        var slot = await _db.AdInventorySlots
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == adInventorySlotId, cancellationToken);

        var hasSynthetic = WeddingPlannerCampaignReadinessValidation.DetectSyntheticUpstream(
            package?.DocumentJson, qa?.DocumentJson);

        var rulesContext = new WeddingPlannerCampaignReadinessRulesContext(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentAcceptedQaReviewReportVersionId,
            workspace.CurrentApprovedCreativePackageVersionId,
            qa,
            latestQaDecision,
            package,
            packageSha,
            latestApprove,
            selectedVariantSnapshot,
            selectedAsset,
            assetsForVariant,
            concept,
            selectedConceptPresent,
            dna,
            color,
            research,
            match,
            opportunity,
            programAdvertiserId,
            campaign,
            content,
            slot,
            hasSynthetic,
            IsDevelopmentHost,
            syntheticMarkerAcknowledged);

        var rules = WeddingPlannerCampaignReadinessRulesEngine.Evaluate(rulesContext);

        if (qa is null
            || latestQaDecision is null
            || package is null
            || packageSha is null
            || latestApprove is null
            || selectedAsset is null
            || match is null
            || opportunity is null
            || campaign is null
            || content is null
            || slot is null)
        {
            var blocked = string.Join(
                ", ",
                rules.Findings
                    .Where(f => f.Severity == WeddingPlannerCampaignReadinessFindingSeverities.Block)
                    .Select(f => f.Code));
            throw new InvalidOperationException(
                $"campaign-readiness-rules.v1 overallSeverity is BLOCK ({blocked}).");
        }

        return new CommitEvaluation(
            rules,
            qa,
            latestQaDecision,
            package,
            packageSha,
            latestApprove,
            selectedAsset,
            match,
            opportunity,
            campaign,
            content,
            slot,
            hasSynthetic,
            creatorName);
    }

    private async Task<IReadOnlyList<CampaignReadinessMatchCandidate>> LoadCandidatesAsync(
        Guid advertiserId,
        CancellationToken cancellationToken)
    {
        var matches = await _db.BlissMatches
            .AsNoTracking()
            .Include(x => x.AdvertiserOpportunity)
            .ThenInclude(x => x.AdvertiserProgram)
            .Where(x => x.Status == EntityStatuses.Approved
                        && x.AdvertiserOpportunity.Status == EntityStatuses.Active
                        && x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == advertiserId)
            .ToListAsync(cancellationToken);

        var result = new List<CampaignReadinessMatchCandidate>();
        foreach (var match in matches)
        {
            var campaigns = await _db.Campaigns
                .AsNoTracking()
                .Where(x => x.Status == EntityStatuses.Draft
                            && (x.AdvertiserOpportunityId == null
                                || x.AdvertiserOpportunityId == match.AdvertiserOpportunityId))
                .OrderBy(x => x.Name)
                .Select(x => new CampaignReadinessCampaignCandidate(
                    x.Id,
                    x.Name,
                    x.Status,
                    x.AdvertiserOpportunityId,
                    x.AdvertiserOpportunityId == null
                        || x.AdvertiserOpportunityId == match.AdvertiserOpportunityId))
                .ToListAsync(cancellationToken);

            var contentItems = await _db.ContentItems
                .AsNoTracking()
                .Where(x => x.CreatorId == match.CreatorId)
                .OrderBy(x => x.Title)
                .ToListAsync(cancellationToken);

            var contentCandidates = new List<CampaignReadinessContentCandidate>();
            foreach (var content in contentItems)
            {
                var slots = await _db.AdInventorySlots
                    .AsNoTracking()
                    .Where(x => x.ContentItemId == content.Id)
                    .OrderBy(x => x.SlotType)
                    .Select(x => new CampaignReadinessSlotCandidate(
                        x.Id,
                        x.SlotType,
                        x.IsAvailable,
                        "IsAvailable is informational only; slot availability is not reserved."))
                    .ToListAsync(cancellationToken);
                contentCandidates.Add(new CampaignReadinessContentCandidate(
                    content.Id,
                    content.Title,
                    content.ContentType,
                    slots));
            }

            result.Add(new CampaignReadinessMatchCandidate(
                match.Id,
                match.CreatorId,
                match.AdvertiserOpportunityId,
                match.Status,
                match.OverallScore,
                match.AdvertiserOpportunity.Status,
                match.AdvertiserOpportunity.Name,
                campaigns,
                contentCandidates));
        }

        return result;
    }

    private async Task<WeddingPlannerWorkspace> LoadWorkspaceForReadAsync(
        Guid workspaceId,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken)
    {
        var workspace = await _db.WeddingPlannerWorkspaces
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspaceId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Wedding Planner workspace was not found.");

        await EnsureHandshakeReadAccessAsync(
            workspace.AdvertiserId, canReadAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        return workspace;
    }

    private Task EnsureHandshakeReadAccessAsync(
        Guid advertiserId,
        bool canReadAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken)
    {
        if (canReadAcrossWorkspaces)
        {
            return Task.CompletedTask;
        }

        if (boundAdvertiserId is Guid bound && bound == advertiserId)
        {
            return Task.CompletedTask;
        }

        throw new WeddingPlannerNotFoundException("Campaign readiness handshake was not found.");
    }

    private async Task<int> NextVersionNumberAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var max = await _db.WeddingPlannerCampaignReadinessHandshakeVersions
            .Where(x => x.WorkspaceId == workspaceId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    private static WeddingPlannerCampaignReadinessHandshakeResult ToHandshakeResult(
        WeddingPlannerCampaignReadinessHandshakeVersion version,
        bool isCurrent,
        bool isReplay) =>
        new(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.DocumentJson,
            version.Summary,
            version.Status,
            version.QaReviewReportVersionId,
            version.QaAcceptDecisionId,
            version.ApprovedCreativePackageVersionId,
            version.CreativePackageDocumentSha256,
            version.CreativePackageDecisionId,
            version.SelectedVariantId,
            version.SelectedCreativeAssetId,
            version.SelectedCreativeAssetSha256,
            version.BlissMatchId,
            version.CampaignId,
            version.ContentItemId,
            version.AdInventorySlotId,
            version.CampaignPlacementId,
            version.CampaignPlacementRunId,
            version.RulesFindingsJson,
            version.Rationale,
            version.DisclaimerAcknowledged,
            version.SyntheticMarkerAcknowledged,
            version.SourceSystem,
            version.IdempotencyKey,
            version.ActorType,
            version.ActorLabel,
            version.CreatedAt,
            isCurrent,
            isReplay);

    private sealed record CommitEvaluation(
        CanonicalCampaignReadinessRulesFindings Rules,
        WeddingPlannerQaReviewReportVersion QaReport,
        WeddingPlannerQaReviewDecision LatestQaDecision,
        WeddingPlannerCreativePackageVersion Package,
        string PackageSha,
        WeddingPlannerCreativePackageDecision LatestCreativeApprove,
        WeddingPlannerCreativeAsset SelectedAsset,
        BlissMatch Match,
        AdvertiserOpportunity Opportunity,
        Campaign Campaign,
        ContentItem Content,
        AdInventorySlot Slot,
        bool HasSyntheticUpstream,
        string? CreatorName);
}
