using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerMeasurementLearningJobResult(
    Guid MeasurementLearningJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid CampaignReadinessHandshakeVersionId,
    string HandshakeStatusSnapshot,
    bool HandshakeWasCurrentAtJobStart,
    Guid CampaignPlacementId,
    Guid CampaignPlacementRunId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid QaReviewReportVersionId,
    Guid ApprovedCreativePackageVersionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    DateTime ObservationStart,
    DateTime ObservationEnd,
    string SourceLabel,
    bool AttestationAcknowledged,
    long Impressions,
    long Clicks,
    long Conversions,
    decimal Spend,
    decimal? Revenue,
    string CurrencyCode,
    string? Notes,
    string InputJson,
    string InputSha256,
    string? MetricsJson,
    string? RulesFindingsJson,
    string? RulesOverallSeverity,
    Guid? PerformanceAnalysisAgentRunId,
    Guid? LearningSynthesisAgentRunId,
    Guid? OutputMeasurementLearningReportVersionId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool IsReplay);

public sealed record WeddingPlannerMeasurementLearningReportVersionResult(
    Guid MeasurementLearningReportVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingMeasurementLearningJobId,
    Guid ProducingAgentRunId,
    Guid PerformanceAnalysisAgentRunId,
    Guid LearningSynthesisAgentRunId,
    Guid CampaignReadinessHandshakeVersionId,
    string HandshakeStatusSnapshot,
    bool HandshakeWasCurrentAtJobStart,
    Guid CampaignPlacementId,
    Guid CampaignPlacementRunId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid QaReviewReportVersionId,
    Guid ApprovedCreativePackageVersionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    DateTime ObservationStart,
    DateTime ObservationEnd,
    string SourceLabel,
    string ObservationSourceSystem,
    long Impressions,
    long Clicks,
    long Conversions,
    decimal Spend,
    decimal? Revenue,
    string CurrencyCode,
    string MetricsJson,
    string RulesFindingsJson,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentAccepted,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerMeasurementLearningReportListResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentAcceptedMeasurementLearningReportVersionId,
    IReadOnlyList<WeddingPlannerMeasurementLearningReportVersionResult> Versions);

public sealed record WeddingPlannerMeasurementLearningRoleContributionResult(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid MeasurementLearningReportVersionId,
    Guid MeasurementLearningJobId,
    string LogicalRole,
    string ContributionSource,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerMeasurementLearningDecisionResult(
    Guid DecisionId,
    Guid MeasurementLearningReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string Rationale,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerMeasurementLearningReportVersionResult Version,
    bool IsReplay);

/// <summary>
/// Measurement / Learning saga: deterministic measurement-rules.v1 (BLOCK fails pre-AI),
/// exactly two AI profiles mapped to exactly three role contributions, immutable report.
/// Never mutates Phase 1–8 artifacts. Never invents delivery telemetry or causal attribution.
/// </summary>
public sealed class WeddingPlannerMeasurementLearningOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly BlissDbContext _db;
    private readonly WeddingPlannerService _planner;
    private readonly IWeddingPlannerAiProvider _ai;
    private readonly WeddingPlannerAiOptions _aiOptions;

    public WeddingPlannerMeasurementLearningOrchestrationService(
        BlissDbContext db,
        WeddingPlannerService planner,
        IWeddingPlannerAiProvider ai,
        IOptions<WeddingPlannerAiOptions> aiOptions)
    {
        _db = db;
        _planner = planner;
        _ai = ai;
        _aiOptions = aiOptions.Value;
    }

    public async Task<WeddingPlannerMeasurementLearningJobResult> CreateMeasurementLearningJobAsync(
        Guid workspaceId,
        Guid campaignReadinessHandshakeVersionId,
        DateTime observationStart,
        DateTime observationEnd,
        string sourceLabel,
        string observationSourceSystem,
        bool attestationAcknowledged,
        long impressions,
        long clicks,
        long conversions,
        decimal spend,
        decimal? revenue,
        string currencyCode,
        string? notes,
        JsonNode? rawBodyNode,
        string sourceSystem,
        string idempotencyKey,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        EnsureAiProviderAllowed();

        if (rawBodyNode is not null)
        {
            WeddingPlannerMeasurementLearningValidation.RejectForbiddenInputFields(rawBodyNode);
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64).ToUpperInvariant();
        var key = Required(
            idempotencyKey,
            nameof(idempotencyKey),
            WeddingPlannerMeasurementLearningIdempotency.MaxJobIdempotencyKeyLength);
        var normalizedObservationStart = observationStart.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(observationStart, DateTimeKind.Utc)
            : observationStart.ToUniversalTime();
        var normalizedObservationEnd = observationEnd.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(observationEnd, DateTimeKind.Utc)
            : observationEnd.ToUniversalTime();

        var existing = await _db.WeddingPlannerMeasurementLearningJobs
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkspaceId != workspace.Id || existing.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Measurement learning job was not found.");
            }

            EnsureJobReplayMatches(
                existing,
                campaignReadinessHandshakeVersionId,
                normalizedObservationStart,
                normalizedObservationEnd,
                sourceLabel,
                attestationAcknowledged,
                impressions,
                clicks,
                conversions,
                spend,
                revenue,
                currencyCode,
                notes);
            return ToJobResult(existing, true);
        }

        var handshake = await _db.WeddingPlannerCampaignReadinessHandshakeVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == campaignReadinessHandshakeVersionId, cancellationToken);
        if (handshake is null
            || handshake.WorkspaceId != workspace.Id
            || handshake.AdvertiserId != workspace.AdvertiserId)
        {
            throw new WeddingPlannerNotFoundException("Campaign readiness handshake was not found.");
        }

        if (!string.Equals(handshake.Status, WeddingPlannerCampaignReadinessHandshakeStatuses.CampaignReady, StringComparison.Ordinal)
            && !string.Equals(handshake.Status, WeddingPlannerCampaignReadinessHandshakeStatuses.Revoked, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Handshake Status must be CAMPAIGN_READY or REVOKED for measurement learning.");
        }

        var placement = await _db.CampaignPlacements
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.CampaignPlacementId, cancellationToken);
        var placementRun = await _db.CampaignPlacementRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.CampaignPlacementRunId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var handshakeWasCurrent = workspace.CurrentCampaignReadinessHandshakeVersionId == handshake.Id;
        var requireSynthetic = IsLocalAiProvider();
        var brief = WeddingPlannerMeasurementLearningValidation.CanonicalizeBrief(
            handshake.Id,
            normalizedObservationStart,
            normalizedObservationEnd,
            sourceLabel,
            observationSourceSystem,
            attestationAcknowledged,
            impressions,
            clicks,
            conversions,
            spend,
            revenue,
            currencyCode,
            notes,
            handshake.CampaignPlacementId,
            handshake.CampaignPlacementRunId,
            handshake.BlissMatchId,
            handshake.CampaignId,
            handshake.ContentItemId,
            handshake.AdInventorySlotId,
            handshake.QaReviewReportVersionId,
            handshake.ApprovedCreativePackageVersionId,
            handshake.SelectedVariantId,
            handshake.SelectedCreativeAssetId,
            handshake.ApprovedConceptPackageVersionId,
            handshake.SelectedConceptId,
            handshake.ApprovedBrandDnaVersionId,
            handshake.ApprovedBrandDnaVersionNumber,
            handshake.ApprovedColorProfileVersionId,
            handshake.ApprovedColorProfileVersionNumber,
            handshake.ApprovedResearchReportVersionId,
            handshake.ApprovedResearchReportVersionNumber,
            handshake.Status,
            handshakeWasCurrent,
            string.IsNullOrWhiteSpace(_aiOptions.Provider)
                ? WeddingPlannerAiProviderKinds.Local
                : _aiOptions.Provider.Trim(),
            _ai.WorkerKey,
            utcNow);

        var job = new WeddingPlannerMeasurementLearningJob
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            CampaignReadinessHandshakeVersionId = handshake.Id,
            HandshakeStatusSnapshot = handshake.Status,
            HandshakeWasCurrentAtJobStart = handshakeWasCurrent,
            CampaignPlacementId = handshake.CampaignPlacementId,
            CampaignPlacementRunId = handshake.CampaignPlacementRunId,
            BlissMatchId = handshake.BlissMatchId,
            CampaignId = handshake.CampaignId,
            ContentItemId = handshake.ContentItemId,
            AdInventorySlotId = handshake.AdInventorySlotId,
            QaReviewReportVersionId = handshake.QaReviewReportVersionId,
            ApprovedCreativePackageVersionId = handshake.ApprovedCreativePackageVersionId,
            SelectedVariantId = handshake.SelectedVariantId,
            SelectedCreativeAssetId = handshake.SelectedCreativeAssetId,
            ApprovedConceptPackageVersionId = handshake.ApprovedConceptPackageVersionId,
            SelectedConceptId = handshake.SelectedConceptId,
            ApprovedBrandDnaVersionId = handshake.ApprovedBrandDnaVersionId,
            ApprovedBrandDnaVersionNumber = handshake.ApprovedBrandDnaVersionNumber,
            ApprovedColorProfileVersionId = handshake.ApprovedColorProfileVersionId,
            ApprovedColorProfileVersionNumber = handshake.ApprovedColorProfileVersionNumber,
            ApprovedResearchReportVersionId = handshake.ApprovedResearchReportVersionId,
            ApprovedResearchReportVersionNumber = handshake.ApprovedResearchReportVersionNumber,
            ObservationStart = brief.ObservationStart,
            ObservationEnd = brief.ObservationEnd,
            SourceLabel = brief.SourceLabel,
            AttestationAcknowledged = true,
            Impressions = brief.Impressions,
            Clicks = brief.Clicks,
            Conversions = brief.Conversions,
            Spend = brief.Spend,
            Revenue = brief.Revenue,
            CurrencyCode = brief.CurrencyCode,
            Notes = brief.Notes,
            InputJson = brief.InputJson,
            InputSha256 = brief.InputSha256,
            MetricsJson = brief.Metrics.MetricsJson,
            Status = WeddingPlannerMeasurementLearningJobStatuses.Running,
            SourceSystem = source,
            IdempotencyKey = key,
            ActorType = actorType,
            ActorLabel = actorLabel,
            StartedAt = utcNow
        };
        _db.WeddingPlannerMeasurementLearningJobs.Add(job);
        _planner.AddAuditForOrchestration(
            workspace.AdvertiserId,
            workspace.Id,
            null,
            null,
            WeddingPlannerAuditActions.MeasurementLearningJobStarted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"Measurement learning job {job.Id} started.");
        await _db.SaveChangesAsync(cancellationToken);

        CanonicalMeasurementLearningRulesFindings rules;
        try
        {
            rules = await EvaluateRulesAsync(job, workspace, handshake, placement, placementRun, cancellationToken);
            job.RulesFindingsJson = rules.FindingsJson;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.MeasurementLearningRulesFindingsRecorded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                Truncate($"Measurement rules findings recorded for job {job.Id}: {rules.OverallSeverity}.", 2000));
            await _db.SaveChangesAsync(cancellationToken);

            if (!string.Equals(
                    rules.OverallSeverity,
                    WeddingPlannerMeasurementLearningFindingSeverities.Pass,
                    StringComparison.Ordinal))
            {
                var blocked = string.Join(
                    ", ",
                    rules.Findings
                        .Where(f => f.Severity == WeddingPlannerMeasurementLearningFindingSeverities.Block)
                        .Select(f => f.Code));
                throw new InvalidOperationException(
                    $"measurement-rules.v1 overallSeverity is BLOCK ({blocked}).");
            }
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }

        WeddingPlannerAgentRun performanceRun;
        WeddingPlannerAgentRun synthesisRun;
        try
        {
            performanceRun = await ExecuteStageAsync(
                job,
                workspace,
                brief,
                rules,
                WeddingPlannerMeasurementLearningWorkerProfiles.PerformanceAnalysisV1,
                requireSynthetic,
                actorType,
                actorLabel,
                requestId,
                cancellationToken);
            synthesisRun = await ExecuteStageAsync(
                job,
                workspace,
                brief,
                rules,
                WeddingPlannerMeasurementLearningWorkerProfiles.LearningSynthesisV1,
                requireSynthetic,
                actorType,
                actorLabel,
                requestId,
                cancellationToken);
        }
        catch (WeddingPlannerProviderException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }

        try
        {
            var performance = WeddingPlannerMeasurementLearningValidation.CanonicalizePerformanceAnalysisOutput(
                job.PerformanceAnalysisStageOutputJson,
                requireSynthetic);
            var synthesis = WeddingPlannerMeasurementLearningValidation.CanonicalizeLearningSynthesisOutput(
                job.LearningSynthesisStageOutputJson,
                requireSynthetic);
            var reportCanonical = WeddingPlannerMeasurementLearningValidation.MergeReport(
                brief,
                rules,
                performance,
                synthesis,
                performanceRun.Id,
                synthesisRun.Id,
                job.Id,
                requireSynthetic);

            var nextVersion = await _db.WeddingPlannerMeasurementLearningReportVersions
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => (int?)x.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            var report = new WeddingPlannerMeasurementLearningReportVersion
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                VersionNumber = nextVersion + 1,
                SchemaVersion = WeddingPlannerSchemaVersions.MeasurementLearningReportV1,
                DocumentJson = reportCanonical.DocumentJson,
                Summary = reportCanonical.Summary,
                ProducingMeasurementLearningJobId = job.Id,
                ProducingAgentRunId = synthesisRun.Id,
                PerformanceAnalysisAgentRunId = performanceRun.Id,
                LearningSynthesisAgentRunId = synthesisRun.Id,
                CampaignReadinessHandshakeVersionId = job.CampaignReadinessHandshakeVersionId,
                HandshakeStatusSnapshot = job.HandshakeStatusSnapshot,
                HandshakeWasCurrentAtJobStart = job.HandshakeWasCurrentAtJobStart,
                CampaignPlacementId = job.CampaignPlacementId,
                CampaignPlacementRunId = job.CampaignPlacementRunId,
                BlissMatchId = job.BlissMatchId,
                CampaignId = job.CampaignId,
                ContentItemId = job.ContentItemId,
                AdInventorySlotId = job.AdInventorySlotId,
                QaReviewReportVersionId = job.QaReviewReportVersionId,
                ApprovedCreativePackageVersionId = job.ApprovedCreativePackageVersionId,
                SelectedVariantId = job.SelectedVariantId,
                SelectedCreativeAssetId = job.SelectedCreativeAssetId,
                ApprovedConceptPackageVersionId = job.ApprovedConceptPackageVersionId,
                SelectedConceptId = job.SelectedConceptId,
                ApprovedBrandDnaVersionId = job.ApprovedBrandDnaVersionId,
                ApprovedBrandDnaVersionNumber = job.ApprovedBrandDnaVersionNumber,
                ApprovedColorProfileVersionId = job.ApprovedColorProfileVersionId,
                ApprovedColorProfileVersionNumber = job.ApprovedColorProfileVersionNumber,
                ApprovedResearchReportVersionId = job.ApprovedResearchReportVersionId,
                ApprovedResearchReportVersionNumber = job.ApprovedResearchReportVersionNumber,
                ObservationStart = job.ObservationStart,
                ObservationEnd = job.ObservationEnd,
                SourceLabel = job.SourceLabel,
                ObservationSourceSystem = brief.ObservationSourceSystem,
                Impressions = job.Impressions,
                Clicks = job.Clicks,
                Conversions = job.Conversions,
                Spend = job.Spend,
                Revenue = job.Revenue,
                CurrencyCode = job.CurrencyCode,
                MetricsJson = job.MetricsJson ?? brief.Metrics.MetricsJson,
                RulesFindingsJson = job.RulesFindingsJson ?? rules.FindingsJson,
                Status = WeddingPlannerMeasurementLearningReportStatuses.Proposed,
                SourceSystem = source,
                IdempotencyKey = WeddingPlannerMeasurementLearningIdempotency.ReportKey(key),
                ActorType = actorType,
                ActorLabel = actorLabel,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerMeasurementLearningReportVersions.Add(report);

            var nowComplete = DateTime.UtcNow;
            foreach (var contribution in reportCanonical.RoleContributions)
            {
                _db.WeddingPlannerMeasurementLearningRoleContributions.Add(
                    new WeddingPlannerMeasurementLearningRoleContribution
                    {
                        Id = Guid.NewGuid(),
                        AdvertiserId = workspace.AdvertiserId,
                        WorkspaceId = workspace.Id,
                        MeasurementLearningReportVersionId = report.Id,
                        MeasurementLearningJobId = job.Id,
                        LogicalRole = contribution.LogicalRole,
                        ContributionSource = contribution.ContributionSource,
                        ProducingAgentRunId = contribution.ProducingAgentRunId,
                        ContributionJson = contribution.ContributionJson,
                        CreatedAt = nowComplete
                    });
            }

            synthesisRun.OutputMeasurementLearningReportVersionId = report.Id;
            job.OutputMeasurementLearningReportVersionId = report.Id;
            job.Status = WeddingPlannerMeasurementLearningJobStatuses.Succeeded;
            job.CompletedAt = nowComplete;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.MeasurementLearningReportProposed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Proposed,
                requestId,
                $"Measurement learning report {report.Id} proposed.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.MeasurementLearningJobSucceeded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"Measurement learning job {job.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(job, false);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            AbandonPendingReportPersistence(job, synthesisRun);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<WeddingPlannerMeasurementLearningJobResult>> ListMeasurementLearningJobsAsync(
        Guid workspaceId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var items = await _db.WeddingPlannerMeasurementLearningJobs
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return items.Select(x => ToJobResult(x, false)).ToList();
    }

    public async Task<WeddingPlannerMeasurementLearningJobResult> GetMeasurementLearningJobAsync(
        Guid jobId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.WeddingPlannerMeasurementLearningJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == jobId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Measurement learning job was not found.");
        await _planner.RequireWorkspaceForOrchestrationAsync(
            job.WorkspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        return ToJobResult(job, false);
    }

    public async Task<WeddingPlannerMeasurementLearningReportListResult> ListMeasurementLearningReportsAsync(
        Guid workspaceId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var versions = await _db.WeddingPlannerMeasurementLearningReportVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);
        var results = new List<WeddingPlannerMeasurementLearningReportVersionResult>(versions.Count);
        foreach (var version in versions)
        {
            results.Add(await ToReportResultAsync(
                version,
                workspace.CurrentAcceptedMeasurementLearningReportVersionId == version.Id,
                false,
                cancellationToken));
        }

        return new WeddingPlannerMeasurementLearningReportListResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentAcceptedMeasurementLearningReportVersionId,
            results);
    }

    public async Task<WeddingPlannerMeasurementLearningReportVersionResult> GetMeasurementLearningReportAsync(
        Guid reportId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerMeasurementLearningReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Measurement learning report was not found.");
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        return await ToReportResultAsync(
            version,
            workspace.CurrentAcceptedMeasurementLearningReportVersionId == version.Id,
            false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WeddingPlannerMeasurementLearningRoleContributionResult>> ListContributionsAsync(
        Guid reportId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerMeasurementLearningReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Measurement learning report was not found.");
        await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var items = await _db.WeddingPlannerMeasurementLearningRoleContributions
            .AsNoTracking()
            .Where(x => x.MeasurementLearningReportVersionId == reportId)
            .ToListAsync(cancellationToken);
        return items
            .OrderBy(x => Array.IndexOf(WeddingPlannerMeasurementLearningLogicalRoles.AllInOrder.ToArray(), x.LogicalRole))
            .Select(x => new WeddingPlannerMeasurementLearningRoleContributionResult(
                x.Id,
                x.AdvertiserId,
                x.WorkspaceId,
                x.MeasurementLearningReportVersionId,
                x.MeasurementLearningJobId,
                x.LogicalRole,
                x.ContributionSource,
                x.ProducingAgentRunId,
                x.ContributionJson,
                x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListAgentRunsAsync(
        Guid reportId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerMeasurementLearningReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Measurement learning report was not found.");
        await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var runIds = new[] { version.PerformanceAnalysisAgentRunId, version.LearningSynthesisAgentRunId };
        var runs = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => runIds.Contains(x.Id))
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return runs.Select(r => ToAgentRunResult(r, false)).ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerMeasurementLearningDecisionResult>> ListDecisionsAsync(
        Guid reportId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerMeasurementLearningReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Measurement learning report was not found.");
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var decisions = await _db.WeddingPlannerMeasurementLearningDecisions
            .AsNoTracking()
            .Where(x => x.MeasurementLearningReportVersionId == reportId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
        var reportResult = await ToReportResultAsync(
            version,
            workspace.CurrentAcceptedMeasurementLearningReportVersionId == version.Id,
            false,
            cancellationToken);
        return decisions.Select(d => new WeddingPlannerMeasurementLearningDecisionResult(
            d.Id,
            d.MeasurementLearningReportVersionId,
            d.WorkspaceId,
            d.AdvertiserId,
            d.Decision,
            d.Rationale,
            d.ActorType,
            d.ActorLabel,
            d.SourceSystem,
            d.IdempotencyKey,
            d.OccurredAt,
            reportResult,
            false)).ToList();
    }

    public async Task<WeddingPlannerMeasurementLearningDecisionResult> DecideMeasurementLearningReportAsync(
        Guid reportId,
        string decision,
        string rationale,
        JsonNode? rawBodyNode,
        string sourceSystem,
        string idempotencyKey,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (rawBodyNode is not null)
        {
            WeddingPlannerMeasurementLearningValidation.RejectForbiddenDecisionFields(rawBodyNode);
        }

        var normalizedDecision = WeddingPlannerMeasurementLearningValidation.NormalizeDecision(decision);
        var normalizedRationale = Required(rationale, nameof(rationale), WeddingPlannerMeasurementLearningValidation.MaxRationaleLength);
        var source = Required(sourceSystem, nameof(sourceSystem), 64).ToUpperInvariant();
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);

        var existing = await _db.WeddingPlannerMeasurementLearningDecisions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            var existingReport = await _db.WeddingPlannerMeasurementLearningReportVersions
                .SingleAsync(x => x.Id == existing.MeasurementLearningReportVersionId, cancellationToken);
            if (existingReport.Id != reportId)
            {
                throw new WeddingPlannerNotFoundException("Measurement learning decision was not found.");
            }

            var workspaceReplay = await _planner.RequireWorkspaceForOrchestrationAsync(
                existing.WorkspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
            if (!string.Equals(existing.Decision, normalizedDecision, StringComparison.Ordinal)
                || !string.Equals(existing.Rationale, normalizedRationale, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Idempotency key is already bound to a different measurement-learning decision request.");
            }

            return new WeddingPlannerMeasurementLearningDecisionResult(
                existing.Id,
                existing.MeasurementLearningReportVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Decision,
                existing.Rationale,
                existing.ActorType,
                existing.ActorLabel,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                await ToReportResultAsync(
                    existingReport,
                    workspaceReplay.CurrentAcceptedMeasurementLearningReportVersionId == existingReport.Id,
                    true,
                    cancellationToken),
                true);
        }

        var version = await _db.WeddingPlannerMeasurementLearningReportVersions
            .SingleOrDefaultAsync(x => x.Id == reportId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Measurement learning report was not found.");
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);

        if (!string.Equals(version.Status, WeddingPlannerMeasurementLearningReportStatuses.Proposed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only PROPOSED measurement-learning reports may receive a terminal decision.");
        }

        var priorDecision = await _db.WeddingPlannerMeasurementLearningDecisions
            .AsNoTracking()
            .AnyAsync(x => x.MeasurementLearningReportVersionId == version.Id, cancellationToken);
        if (priorDecision)
        {
            throw new InvalidOperationException("Measurement-learning report already has a terminal decision.");
        }

        var now = DateTime.UtcNow;
        var row = new WeddingPlannerMeasurementLearningDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            MeasurementLearningReportVersionId = version.Id,
            Decision = normalizedDecision,
            Rationale = normalizedRationale,
            ActorType = actorType,
            ActorLabel = actorLabel,
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = now
        };
        _db.WeddingPlannerMeasurementLearningDecisions.Add(row);

        if (string.Equals(normalizedDecision, WeddingPlannerMeasurementLearningDecisions.Accept, StringComparison.Ordinal))
        {
            if (workspace.CurrentAcceptedMeasurementLearningReportVersionId is Guid previousId
                && previousId != version.Id)
            {
                var previous = await _db.WeddingPlannerMeasurementLearningReportVersions
                    .SingleOrDefaultAsync(x => x.Id == previousId, cancellationToken);
                if (previous is not null
                    && string.Equals(previous.Status, WeddingPlannerMeasurementLearningReportStatuses.Accepted, StringComparison.Ordinal))
                {
                    previous.Status = WeddingPlannerMeasurementLearningReportStatuses.Superseded;
                    _planner.AddAuditForOrchestration(
                        workspace.AdvertiserId,
                        workspace.Id,
                        null,
                        null,
                        WeddingPlannerAuditActions.MeasurementLearningReportSuperseded,
                        actorType,
                        actorLabel,
                        WeddingPlannerOutcomes.Superseded,
                        requestId,
                        $"Measurement learning report {previous.Id} superseded.");
                }
            }

            version.Status = WeddingPlannerMeasurementLearningReportStatuses.Accepted;
            workspace.CurrentAcceptedMeasurementLearningReportVersionId = version.Id;
            workspace.UpdatedAt = now;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.MeasurementLearningReportAccepted,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                $"Measurement learning report {version.Id} accepted.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.MeasurementLearningPointerSet,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"Measurement learning pointer set to {version.Id}.");
        }
        else
        {
            version.Status = WeddingPlannerMeasurementLearningReportStatuses.Rejected;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.MeasurementLearningReportRejected,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                $"Measurement learning report {version.Id} rejected.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new WeddingPlannerMeasurementLearningDecisionResult(
            row.Id,
            row.MeasurementLearningReportVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Decision,
            row.Rationale,
            row.ActorType,
            row.ActorLabel,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            await ToReportResultAsync(
                version,
                workspace.CurrentAcceptedMeasurementLearningReportVersionId == version.Id,
                false,
                cancellationToken),
            false);
    }

    private async Task<CanonicalMeasurementLearningRulesFindings> EvaluateRulesAsync(
        WeddingPlannerMeasurementLearningJob job,
        WeddingPlannerWorkspace workspace,
        WeddingPlannerCampaignReadinessHandshakeVersion handshake,
        CampaignPlacement? placement,
        CampaignPlacementRun? placementRun,
        CancellationToken cancellationToken)
    {
        var provenancePinsValid = await ValidateProvenancePinsAsync(handshake, workspace, cancellationToken);
        var observationWindowValid = WeddingPlannerMeasurementLearningValidation.IsValidObservationWindow(
            job.ObservationStart,
            job.ObservationEnd,
            DateTime.UtcNow);
        var noEventLevel = !ContainsEventLevelSignals(job.InputJson) && !ContainsEventLevelSignals(job.Notes);

        var context = new WeddingPlannerMeasurementLearningRulesContext(
            workspace.Id,
            workspace.AdvertiserId,
            job.CampaignReadinessHandshakeVersionId,
            job.CampaignPlacementId,
            job.CampaignPlacementRunId,
            job.QaReviewReportVersionId,
            job.ApprovedCreativePackageVersionId,
            job.SelectedVariantId,
            job.SelectedCreativeAssetId,
            job.ApprovedConceptPackageVersionId,
            job.SelectedConceptId,
            job.ApprovedBrandDnaVersionId,
            job.ApprovedBrandDnaVersionNumber,
            job.ApprovedColorProfileVersionId,
            job.ApprovedColorProfileVersionNumber,
            job.ApprovedResearchReportVersionId,
            job.ApprovedResearchReportVersionNumber,
            job.BlissMatchId,
            job.CampaignId,
            job.ContentItemId,
            job.AdInventorySlotId,
            handshake,
            placement,
            placementRun,
            observationWindowValid,
            job.AttestationAcknowledged,
            job.SourceLabel,
            ExtractObservationSourceSystem(job.InputJson),
            job.Impressions,
            job.Clicks,
            job.Conversions,
            job.Spend,
            job.Revenue,
            job.CurrencyCode,
            noEventLevel,
            provenancePinsValid);

        return WeddingPlannerMeasurementLearningRulesEngine.Evaluate(context);
    }

    private async Task<bool> ValidateProvenancePinsAsync(
        WeddingPlannerCampaignReadinessHandshakeVersion handshake,
        WeddingPlannerWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var qa = await _db.WeddingPlannerQaReviewReportVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.QaReviewReportVersionId, cancellationToken);
        var package = await _db.WeddingPlannerCreativePackageVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.ApprovedCreativePackageVersionId, cancellationToken);
        var asset = await _db.WeddingPlannerCreativeAssets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.SelectedCreativeAssetId, cancellationToken);
        var concept = await _db.WeddingPlannerConceptPackageVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.ApprovedConceptPackageVersionId, cancellationToken);
        var dna = await _db.WeddingPlannerBrandDnaVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.ApprovedBrandDnaVersionId, cancellationToken);
        var color = await _db.WeddingPlannerColorProfileVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.ApprovedColorProfileVersionId, cancellationToken);
        var research = await _db.WeddingPlannerResearchReportVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.ApprovedResearchReportVersionId, cancellationToken);
        var match = await _db.BlissMatches.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.BlissMatchId, cancellationToken);
        var campaign = await _db.Campaigns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.CampaignId, cancellationToken);
        var content = await _db.ContentItems.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.ContentItemId, cancellationToken);
        var slot = await _db.AdInventorySlots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == handshake.AdInventorySlotId, cancellationToken);

        return qa is not null && qa.WorkspaceId == workspace.Id && qa.AdvertiserId == workspace.AdvertiserId
               && package is not null && package.WorkspaceId == workspace.Id && package.AdvertiserId == workspace.AdvertiserId
               && asset is not null && asset.WorkspaceId == workspace.Id && asset.AdvertiserId == workspace.AdvertiserId
               && concept is not null && concept.WorkspaceId == workspace.Id && concept.AdvertiserId == workspace.AdvertiserId
               && dna is not null && dna.WorkspaceId == workspace.Id && dna.AdvertiserId == workspace.AdvertiserId
               && dna.VersionNumber == handshake.ApprovedBrandDnaVersionNumber
               && color is not null && color.WorkspaceId == workspace.Id && color.AdvertiserId == workspace.AdvertiserId
               && color.VersionNumber == handshake.ApprovedColorProfileVersionNumber
               && research is not null && research.WorkspaceId == workspace.Id && research.AdvertiserId == workspace.AdvertiserId
               && research.VersionNumber == handshake.ApprovedResearchReportVersionNumber
               && match is not null
               && campaign is not null
               && content is not null
               && slot is not null;
    }

    private async Task<WeddingPlannerAgentRun> ExecuteStageAsync(
        WeddingPlannerMeasurementLearningJob job,
        WeddingPlannerWorkspace workspace,
        CanonicalMeasurementLearningBrief brief,
        CanonicalMeasurementLearningRulesFindings rules,
        string workerProfileVersion,
        bool requireSyntheticMarker,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var stageRole = WeddingPlannerMeasurementLearningWorkerProfiles.StageLogicalRole(workerProfileVersion);
        var promptPack = WeddingPlannerMeasurementLearningWorkerProfiles.PromptPack(workerProfileVersion);
        var assignedRoles = WeddingPlannerMeasurementLearningWorkerProfiles.AssignedRoles(workerProfileVersion);
        var stageKey = WeddingPlannerMeasurementLearningIdempotency.StageKey(job.IdempotencyKey, workerProfileVersion);

        var run = new WeddingPlannerAgentRun
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            SessionId = null,
            LogicalRole = stageRole,
            WorkerKey = _ai.WorkerKey,
            PromptPackVersion = promptPack,
            WorkerProfileVersion = workerProfileVersion,
            AssignedRolesJson = JsonSerializer.Serialize(assignedRoles, JsonOptions),
            RequestId = requestId,
            SourceSystem = job.SourceSystem,
            IdempotencyKey = stageKey,
            Status = WeddingPlannerAgentRunStatuses.Running,
            StartedAt = DateTime.UtcNow
        };
        _db.WeddingPlannerAgentRuns.Add(run);
        if (workerProfileVersion == WeddingPlannerMeasurementLearningWorkerProfiles.PerformanceAnalysisV1)
        {
            job.PerformanceAnalysisAgentRunId = run.Id;
        }
        else
        {
            job.LearningSynthesisAgentRunId = run.Id;
        }

        _planner.AddAuditForOrchestration(
            workspace.AdvertiserId,
            workspace.Id,
            null,
            null,
            WeddingPlannerAuditActions.AgentRunStarted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"Measurement learning run {run.Id} ({workerProfileVersion}) started.");
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var messages = BuildStageMessages(job, brief, rules, workerProfileVersion);
            AssertAiContextSafe(messages);
            var completion = await _ai.CompleteAsync(
                new WeddingPlannerAiCompletionRequest(
                    stageRole,
                    promptPack,
                    messages,
                    WeddingPlannerResponseFormats.Json,
                    2048,
                    workerProfileVersion,
                    assignedRoles),
                cancellationToken);

            string canonicalJson;
            if (workerProfileVersion == WeddingPlannerMeasurementLearningWorkerProfiles.PerformanceAnalysisV1)
            {
                var canonical = WeddingPlannerMeasurementLearningValidation.CanonicalizePerformanceAnalysisOutput(
                    completion.Content,
                    requireSyntheticMarker);
                canonicalJson = canonical.StageJson;
                job.PerformanceAnalysisStageOutputJson = canonicalJson;
            }
            else
            {
                var canonical = WeddingPlannerMeasurementLearningValidation.CanonicalizeLearningSynthesisOutput(
                    completion.Content,
                    requireSyntheticMarker);
                canonicalJson = canonical.StageJson;
                job.LearningSynthesisStageOutputJson = canonicalJson;
            }

            run.ProviderKey = completion.ProviderKey;
            run.ModelId = completion.ModelId;
            run.AdapterVersion = completion.AdapterVersion;
            run.ProviderRequestId = completion.ProviderRequestId;
            run.PromptTokens = completion.PromptTokens;
            run.CompletionTokens = completion.CompletionTokens;
            run.TotalTokens = completion.TotalTokens;
            run.EstimatedCostUsd = completion.EstimatedCostUsd;
            run.Status = WeddingPlannerAgentRunStatuses.Succeeded;
            run.Outcome = WeddingPlannerOutcomes.Succeeded;
            run.CompletedAt = DateTime.UtcNow;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.AgentRunSucceeded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"Measurement learning run {run.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            _ = canonicalJson;
            return run;
        }
        catch (Exception ex)
        {
            await FailRunAsync(run, workspace.AdvertiserId, workspace.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw new WeddingPlannerProviderException(run.Id, ex.Message, ex);
        }
    }

    private static IReadOnlyList<WeddingPlannerAiMessage> BuildStageMessages(
        WeddingPlannerMeasurementLearningJob job,
        CanonicalMeasurementLearningBrief brief,
        CanonicalMeasurementLearningRulesFindings rules,
        string workerProfileVersion)
    {
        var prior = workerProfileVersion == WeddingPlannerMeasurementLearningWorkerProfiles.LearningSynthesisV1
            ? job.PerformanceAnalysisStageOutputJson
            : null;
        var body = JsonSerializer.Serialize(new
        {
            schemaVersion = WeddingPlannerSchemaVersions.MeasurementLearningBriefV1,
            workerProfileVersion,
            jobId = job.Id,
            campaignReadinessHandshakeVersionId = job.CampaignReadinessHandshakeVersionId,
            handshakeStatusSnapshot = job.HandshakeStatusSnapshot,
            handshakeWasCurrentAtJobStart = job.HandshakeWasCurrentAtJobStart,
            campaignPlacementId = job.CampaignPlacementId,
            placementStatusClaim = EntityStatuses.Planned,
            observedAggregates = new
            {
                brief.ObservationStart,
                brief.ObservationEnd,
                brief.SourceLabel,
                brief.ObservationSourceSystem,
                brief.Impressions,
                brief.Clicks,
                brief.Conversions,
                brief.Spend,
                brief.Revenue,
                brief.CurrencyCode
            },
            derivedMetrics = JsonNode.Parse(brief.Metrics.MetricsJson),
            rulesFindings = JsonNode.Parse(rules.FindingsJson),
            priorStageOutput = prior is null ? null : JsonNode.Parse(prior),
            constraints = new
            {
                associationOnly = true,
                noCausalClaims = true,
                noUpstreamMutation = true,
                placementRemainsPlanned = true
            }
        }, JsonOptions);

        return
        [
            new WeddingPlannerAiMessage(WeddingPlannerActorTypes.System, body)
        ];
    }

    private static void AssertAiContextSafe(IReadOnlyList<WeddingPlannerAiMessage> messages)
    {
        foreach (var message in messages)
        {
            var body = message.Body;
            if (body.Contains("base64", StringComparison.OrdinalIgnoreCase)
                || body.Contains("imageBytes", StringComparison.OrdinalIgnoreCase)
                || body.Contains("\"bytes\"", StringComparison.OrdinalIgnoreCase)
                || body.Contains("data:image", StringComparison.OrdinalIgnoreCase)
                || body.Contains("http://", StringComparison.OrdinalIgnoreCase)
                || body.Contains("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Measurement-learning AI context must not include bytes, URLs, or event-level payloads.");
            }
        }
    }

    private static bool ContainsEventLevelSignals(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("\"events\"", StringComparison.OrdinalIgnoreCase)
               || text.Contains("userId", StringComparison.OrdinalIgnoreCase)
               || text.Contains("deviceId", StringComparison.OrdinalIgnoreCase)
               || text.Contains("cookie", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ipAddress", StringComparison.OrdinalIgnoreCase)
               || text.Contains("https://", StringComparison.OrdinalIgnoreCase)
               || text.Contains("http://", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractObservationSourceSystem(string inputJson)
    {
        try
        {
            var node = JsonNode.Parse(inputJson) as JsonObject;
            return node?["observationSourceSystem"]?.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void EnsureAiProviderAllowed()
    {
        if (_aiOptions.RequireRemoteAiProvider && IsLocalAiProvider())
        {
            throw new InvalidOperationException(
                "WeddingPlannerAi:Provider must be OpenAiCompatible when RequireRemoteAiProvider is enabled. "
                + "The Local provider is a deterministic development/test worker, not a production AI provider.");
        }
    }

    private bool IsLocalAiProvider() =>
        string.Equals(_aiOptions.Provider, WeddingPlannerAiProviderKinds.Local, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(_aiOptions.Provider);

    private async Task FailJobAsync(
        WeddingPlannerMeasurementLearningJob job,
        string actorType,
        string actorLabel,
        string? requestId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        job.Status = WeddingPlannerMeasurementLearningJobStatuses.Failed;
        job.ErrorCode = Truncate(ex.GetType().Name, 64);
        job.ErrorMessage = Truncate(ex.Message, 2000);
        job.CompletedAt = DateTime.UtcNow;
        job.OutputMeasurementLearningReportVersionId = null;
        _planner.AddAuditForOrchestration(
            job.AdvertiserId,
            job.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.MeasurementLearningJobFailed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Failed,
            requestId,
            $"Measurement learning job {job.Id} failed: {job.ErrorCode}");
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task FailRunAsync(
        WeddingPlannerAgentRun run,
        Guid advertiserId,
        Guid workspaceId,
        string actorType,
        string actorLabel,
        string? requestId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        run.Status = WeddingPlannerAgentRunStatuses.Failed;
        run.Outcome = WeddingPlannerOutcomes.Failed;
        run.ErrorCode = Truncate(ex.GetType().Name, 64);
        run.ErrorMessage = Truncate(ex.Message, 2000);
        run.CompletedAt = DateTime.UtcNow;
        _planner.AddAuditForOrchestration(
            advertiserId,
            workspaceId,
            null,
            null,
            WeddingPlannerAuditActions.AgentRunFailed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Failed,
            requestId,
            $"Measurement learning run {run.Id} failed.");
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void AbandonPendingReportPersistence(
        WeddingPlannerMeasurementLearningJob job,
        WeddingPlannerAgentRun? synthesisRun)
    {
        job.OutputMeasurementLearningReportVersionId = null;
        job.OutputMeasurementLearningReportVersion = null;
        if (synthesisRun is not null)
        {
            synthesisRun.OutputMeasurementLearningReportVersionId = null;
            synthesisRun.OutputMeasurementLearningReportVersion = null;
        }

        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerMeasurementLearningRoleContribution>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.Entity.MeasurementLearningReportVersion = null!;
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerMeasurementLearningReportVersion>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerAuditEvent>()
            .Where(e => e.State == EntityState.Added
                        && (e.Entity.Action == WeddingPlannerAuditActions.MeasurementLearningReportProposed
                            || e.Entity.Action == WeddingPlannerAuditActions.MeasurementLearningJobSucceeded))
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task<WeddingPlannerMeasurementLearningReportVersionResult> ToReportResultAsync(
        WeddingPlannerMeasurementLearningReportVersion version,
        bool isCurrentAccepted,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        decimal? cost = null;
        var runIds = new[] { version.PerformanceAnalysisAgentRunId, version.LearningSynthesisAgentRunId };
        cost = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => runIds.Contains(x.Id))
            .SumAsync(x => x.EstimatedCostUsd ?? 0m, cancellationToken);

        return new WeddingPlannerMeasurementLearningReportVersionResult(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.DocumentJson,
            version.Summary,
            version.ProducingMeasurementLearningJobId,
            version.ProducingAgentRunId,
            version.PerformanceAnalysisAgentRunId,
            version.LearningSynthesisAgentRunId,
            version.CampaignReadinessHandshakeVersionId,
            version.HandshakeStatusSnapshot,
            version.HandshakeWasCurrentAtJobStart,
            version.CampaignPlacementId,
            version.CampaignPlacementRunId,
            version.BlissMatchId,
            version.CampaignId,
            version.ContentItemId,
            version.AdInventorySlotId,
            version.QaReviewReportVersionId,
            version.ApprovedCreativePackageVersionId,
            version.SelectedVariantId,
            version.SelectedCreativeAssetId,
            version.ApprovedConceptPackageVersionId,
            version.SelectedConceptId,
            version.ApprovedBrandDnaVersionId,
            version.ApprovedBrandDnaVersionNumber,
            version.ApprovedColorProfileVersionId,
            version.ApprovedColorProfileVersionNumber,
            version.ApprovedResearchReportVersionId,
            version.ApprovedResearchReportVersionNumber,
            version.ObservationStart,
            version.ObservationEnd,
            version.SourceLabel,
            version.ObservationSourceSystem,
            version.Impressions,
            version.Clicks,
            version.Conversions,
            version.Spend,
            version.Revenue,
            version.CurrencyCode,
            version.MetricsJson,
            version.RulesFindingsJson,
            version.Status,
            version.SourceSystem,
            version.IdempotencyKey,
            version.ActorType,
            version.ActorLabel,
            version.CreatedAt,
            isCurrentAccepted,
            cost,
            isReplay);
    }

    private static WeddingPlannerMeasurementLearningJobResult ToJobResult(
        WeddingPlannerMeasurementLearningJob job,
        bool isReplay)
    {
        string? severity = null;
        if (!string.IsNullOrWhiteSpace(job.RulesFindingsJson))
        {
            try
            {
                var node = JsonNode.Parse(job.RulesFindingsJson) as JsonObject;
                severity = node?["overallSeverity"]?.GetValue<string>();
            }
            catch
            {
                severity = null;
            }
        }

        return new WeddingPlannerMeasurementLearningJobResult(
            job.Id,
            job.AdvertiserId,
            job.WorkspaceId,
            job.CampaignReadinessHandshakeVersionId,
            job.HandshakeStatusSnapshot,
            job.HandshakeWasCurrentAtJobStart,
            job.CampaignPlacementId,
            job.CampaignPlacementRunId,
            job.BlissMatchId,
            job.CampaignId,
            job.ContentItemId,
            job.AdInventorySlotId,
            job.QaReviewReportVersionId,
            job.ApprovedCreativePackageVersionId,
            job.SelectedVariantId,
            job.SelectedCreativeAssetId,
            job.ApprovedConceptPackageVersionId,
            job.SelectedConceptId,
            job.ApprovedBrandDnaVersionId,
            job.ApprovedBrandDnaVersionNumber,
            job.ApprovedColorProfileVersionId,
            job.ApprovedColorProfileVersionNumber,
            job.ApprovedResearchReportVersionId,
            job.ApprovedResearchReportVersionNumber,
            job.ObservationStart,
            job.ObservationEnd,
            job.SourceLabel,
            job.AttestationAcknowledged,
            job.Impressions,
            job.Clicks,
            job.Conversions,
            job.Spend,
            job.Revenue,
            job.CurrencyCode,
            job.Notes,
            job.InputJson,
            job.InputSha256,
            job.MetricsJson,
            job.RulesFindingsJson,
            severity,
            job.PerformanceAnalysisAgentRunId,
            job.LearningSynthesisAgentRunId,
            job.OutputMeasurementLearningReportVersionId,
            job.Status,
            job.ErrorCode,
            job.ErrorMessage,
            job.SourceSystem,
            job.IdempotencyKey,
            job.ActorType,
            job.ActorLabel,
            job.StartedAt,
            job.CompletedAt,
            isReplay);
    }

    private static void EnsureJobReplayMatches(
        WeddingPlannerMeasurementLearningJob existing,
        Guid campaignReadinessHandshakeVersionId,
        DateTime observationStart,
        DateTime observationEnd,
        string sourceLabel,
        bool attestationAcknowledged,
        long impressions,
        long clicks,
        long conversions,
        decimal spend,
        decimal? revenue,
        string currencyCode,
        string? notes)
    {
        var normalizedLabel = sourceLabel?.Trim() ?? string.Empty;
        var normalizedCurrency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (existing.CampaignReadinessHandshakeVersionId != campaignReadinessHandshakeVersionId
            || existing.ObservationStart != observationStart
            || existing.ObservationEnd != observationEnd
            || !string.Equals(existing.SourceLabel, normalizedLabel, StringComparison.Ordinal)
            || existing.AttestationAcknowledged != attestationAcknowledged
            || existing.Impressions != impressions
            || existing.Clicks != clicks
            || existing.Conversions != conversions
            || existing.Spend != spend
            || existing.Revenue != revenue
            || !string.Equals(existing.CurrencyCode, normalizedCurrency, StringComparison.Ordinal)
            || !string.Equals(existing.Notes, normalizedNotes, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Idempotency key is already bound to a different measurement-learning job request.");
        }
    }

    private static WeddingPlannerAgentRunResult ToAgentRunResult(WeddingPlannerAgentRun run, bool isReplay) =>
        new(
            run.Id,
            run.AdvertiserId,
            run.WorkspaceId,
            run.SessionId,
            run.LogicalRole,
            run.WorkerKey,
            run.PromptPackVersion,
            run.ProviderKey,
            run.ModelId,
            run.AdapterVersion,
            run.TriggerMessageId,
            run.OutputMessageId,
            run.OutputBrandDnaVersionId,
            run.WorkerProfileVersion,
            run.AssignedRolesJson,
            run.OutputResearchReportVersionId,
            run.OutputConceptPackageVersionId,
            run.OutputCreativePackageVersionId,
            run.OutputQaReviewReportVersionId,
            run.OutputMeasurementLearningReportVersionId,
            run.RequestId,
            run.ProviderRequestId,
            run.SourceSystem,
            run.IdempotencyKey,
            run.Status,
            run.Outcome,
            run.ErrorCode,
            run.ErrorMessage,
            run.StartedAt,
            run.CompletedAt,
            run.PromptTokens,
            run.CompletionTokens,
            run.TotalTokens,
            run.EstimatedCostUsd,
            isReplay);

    private static string Required(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} exceeds max length {maxLength}.");
        }

        return trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
