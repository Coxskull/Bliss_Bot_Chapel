using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerQaReviewJobResult(
    Guid QaReviewJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string ReviewObjective,
    IReadOnlyList<string> FocusAreas,
    string? Notes,
    string InputJson,
    string InputSha256,
    Guid ApprovedCreativePackageVersionId,
    string CreativePackageDocumentSha256,
    Guid CreativePackageDecisionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    string SelectedCreativeAssetSha256,
    string SelectedCreativeAssetContentType,
    int SelectedCreativeAssetByteSize,
    int SelectedCreativeAssetWidth,
    int SelectedCreativeAssetHeight,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string? RulesFindingsJson,
    string? RulesOverallSeverity,
    Guid? ChaperoneReviewAgentRunId,
    Guid? QaInspectionAgentRunId,
    Guid? OutputQaReviewReportVersionId,
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

public sealed record WeddingPlannerQaReviewReportVersionResult(
    Guid QaReviewReportVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingQaReviewJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedCreativePackageVersionId,
    string CreativePackageDocumentSha256,
    Guid CreativePackageDecisionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    string SelectedCreativeAssetSha256,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentAccepted,
    string? RulesOverallSeverity,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerQaReviewReportListResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentAcceptedQaReviewReportVersionId,
    IReadOnlyList<WeddingPlannerQaReviewReportVersionResult> Versions);

public sealed record WeddingPlannerQaRoleContributionResult(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid QaReviewReportVersionId,
    Guid QaReviewJobId,
    string LogicalRole,
    string ContributionSource,
    Guid? ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerQaReviewDecisionResult(
    Guid DecisionId,
    Guid QaReviewReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string SelectedVariantId,
    string Rationale,
    bool? VisualReviewConfirmed,
    bool? CopyReviewConfirmed,
    bool? ProvenanceReviewConfirmed,
    bool? SyntheticMarkerAcknowledged,
    string? EscalationCategory,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerQaReviewReportVersionResult Version,
    Guid? EscalationCaseId,
    bool IsReplay);

public sealed record WeddingPlannerQaEscalationCaseResult(
    Guid EscalationCaseId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid QaReviewReportVersionId,
    Guid QaReviewDecisionId,
    string Category,
    string Status,
    string RationaleSnapshot,
    string SelectedVariantId,
    Guid ApprovedCreativePackageVersionId,
    Guid SelectedCreativeAssetId,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    Guid? ResolutionId,
    bool IsReplay);

public sealed record WeddingPlannerQaEscalationResolutionResult(
    Guid ResolutionId,
    Guid EscalationCaseId,
    Guid QaReviewReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Resolution,
    string Rationale,
    string? ExceptionRationale,
    bool? ExceptionAcknowledged,
    IReadOnlyList<string>? AcknowledgedBlockerCodes,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerQaEscalationCaseResult Case,
    WeddingPlannerQaReviewReportVersionResult Version,
    bool IsReplay);

/// <summary>
/// QA / Chaperone saga: deterministic qa-rules.v1, exactly two AI profiles, then server-built
/// Steward RULES_HUMAN contribution. Exactly three contributions; never a third Steward agent run.
/// AI never receives image bytes. Phase 6 creative profiles are never invoked.
/// </summary>
public sealed class WeddingPlannerQaReviewOrchestrationService
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

    public WeddingPlannerQaReviewOrchestrationService(
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

    public async Task<WeddingPlannerQaReviewJobResult> CreateQaReviewJobAsync(
        Guid workspaceId,
        string reviewObjective,
        IReadOnlyList<string> focusAreas,
        string? notes,
        JsonNode? rawBriefNode,
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

        if (rawBriefNode is not null)
        {
            WeddingPlannerQaReviewValidation.RejectForbiddenBriefFields(rawBriefNode);
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), WeddingPlannerQaIdempotency.MaxJobIdempotencyKeyLength);

        var existing = await _db.WeddingPlannerQaReviewJobs
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkspaceId != workspace.Id || existing.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("QA review job was not found.");
            }

            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewJobReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"QA review job {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(existing, true);
        }

        if (workspace.CurrentApprovedCreativePackageVersionId is null)
        {
            throw new InvalidOperationException(
                "A current-approved creative package with SelectedVariantId and exactly one selected PNG asset is required before starting a QA review job.");
        }

        var package = await _db.WeddingPlannerCreativePackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedCreativePackageVersionId.Value, cancellationToken);
        if (package is null
            || package.WorkspaceId != workspace.Id
            || !string.Equals(package.Status, WeddingPlannerCreativePackageStatuses.Approved, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Current-approved creative package must exist with APPROVED status.");
        }

        var latestApprove = await _db.WeddingPlannerCreativePackageDecisions
            .AsNoTracking()
            .Where(x => x.CreativePackageVersionId == package.Id
                        && x.Decision == WeddingPlannerCreativePackageDecisions.Approve)
            .OrderByDescending(x => x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestApprove is null || string.IsNullOrWhiteSpace(latestApprove.SelectedVariantId))
        {
            throw new InvalidOperationException(
                "Latest APPROVE decision for the current-approved creative package must carry a valid SelectedVariantId.");
        }

        var selectedVariantId = latestApprove.SelectedVariantId;
        var assets = await _db.WeddingPlannerCreativeAssets
            .AsNoTracking()
            .Where(x => x.CreativePackageVersionId == package.Id && x.VariantId == selectedVariantId)
            .ToListAsync(cancellationToken);
        if (assets.Count != 1)
        {
            throw new InvalidOperationException(
                "Selected variant must have exactly one linked PNG CreativeAsset.");
        }

        var asset = assets[0];
        var packageSha = WeddingPlannerQaReviewValidation.Sha256Hex(package.DocumentJson);
        var requireSynthetic = IsLocalAiProvider();
        var brief = WeddingPlannerQaReviewValidation.CanonicalizeBrief(
            reviewObjective,
            focusAreas,
            notes,
            package.Id,
            packageSha,
            latestApprove.Id,
            selectedVariantId,
            asset.Id,
            asset.Sha256,
            package.SelectedConceptId,
            package.ApprovedBrandDnaVersionId,
            package.ApprovedBrandDnaVersionNumber,
            package.ApprovedColorProfileVersionId,
            package.ApprovedColorProfileVersionNumber,
            package.ApprovedResearchReportVersionId,
            package.ApprovedResearchReportVersionNumber,
            string.IsNullOrWhiteSpace(_aiOptions.Provider)
                ? WeddingPlannerAiProviderKinds.Local
                : _aiOptions.Provider.Trim(),
            _ai.WorkerKey);

        var now = DateTime.UtcNow;
        var job = new WeddingPlannerQaReviewJob
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            ReviewObjective = brief.ReviewObjective,
            FocusAreasJson = JsonSerializer.Serialize(brief.FocusAreas, JsonOptions),
            Notes = brief.Notes,
            InputJson = brief.InputJson,
            InputSha256 = brief.InputSha256,
            ApprovedCreativePackageVersionId = package.Id,
            CreativePackageDocumentSha256 = packageSha,
            CreativePackageDecisionId = latestApprove.Id,
            SelectedVariantId = selectedVariantId,
            SelectedCreativeAssetId = asset.Id,
            SelectedCreativeAssetSha256 = asset.Sha256.ToLowerInvariant(),
            SelectedCreativeAssetContentType = asset.ContentType,
            SelectedCreativeAssetByteSize = asset.ByteSize,
            SelectedCreativeAssetWidth = asset.Width,
            SelectedCreativeAssetHeight = asset.Height,
            SelectedConceptId = package.SelectedConceptId,
            ApprovedBrandDnaVersionId = package.ApprovedBrandDnaVersionId,
            ApprovedBrandDnaVersionNumber = package.ApprovedBrandDnaVersionNumber,
            ApprovedColorProfileVersionId = package.ApprovedColorProfileVersionId,
            ApprovedColorProfileVersionNumber = package.ApprovedColorProfileVersionNumber,
            ApprovedResearchReportVersionId = package.ApprovedResearchReportVersionId,
            ApprovedResearchReportVersionNumber = package.ApprovedResearchReportVersionNumber,
            Status = WeddingPlannerQaReviewJobStatuses.Running,
            SourceSystem = source,
            IdempotencyKey = key,
            ActorType = actorType,
            ActorLabel = actorLabel,
            StartedAt = now
        };
        _db.WeddingPlannerQaReviewJobs.Add(job);
        _planner.AddAuditForOrchestration(
            workspace.AdvertiserId,
            workspace.Id,
            null,
            null,
            WeddingPlannerAuditActions.QaReviewJobStarted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"QA review job {job.Id} started.");
        await _db.SaveChangesAsync(cancellationToken);

        CanonicalQaRulesFindings rules;
        try
        {
            rules = await EvaluateRulesAsync(job, workspace, package, latestApprove, asset, cancellationToken);
            job.RulesFindingsJson = rules.FindingsJson;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.QaRulesFindingsRecorded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                Truncate($"QA rules findings recorded for job {job.Id}: {rules.OverallSeverity}.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }

        var stageOutputs = new Dictionary<string, string>(StringComparer.Ordinal);
        WeddingPlannerAgentRun chaperoneRun;
        WeddingPlannerAgentRun qaRun;
        try
        {
            chaperoneRun = await ExecuteStageAsync(
                job,
                workspace,
                package,
                rules,
                stageOutputs,
                WeddingPlannerQaWorkerProfiles.ChaperoneReviewV1,
                requireSynthetic,
                actorType,
                actorLabel,
                requestId,
                cancellationToken);
            qaRun = await ExecuteStageAsync(
                job,
                workspace,
                package,
                rules,
                stageOutputs,
                WeddingPlannerQaWorkerProfiles.QaInspectionV1,
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
            var chaperone = WeddingPlannerQaReviewValidation.CanonicalizeChaperoneOutput(
                job.ChaperoneReviewStageOutputJson,
                job.SelectedVariantId,
                requireSynthetic);
            var inspection = WeddingPlannerQaReviewValidation.CanonicalizeQaInspectionOutput(
                job.QaInspectionStageOutputJson,
                job.SelectedVariantId,
                rules.OverallSeverity,
                requireSynthetic);

            var blockerCodes = rules.Findings
                .Where(f => f.Severity == WeddingPlannerQaFindingSeverities.Block)
                .Select(f => f.Code)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            var warnCodes = rules.Findings
                .Where(f => f.Severity == WeddingPlannerQaFindingSeverities.Warn)
                .Select(f => f.Code)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            var steward = WeddingPlannerQaReviewValidation.BuildStewardContribution(
                rules.OverallSeverity,
                blockerCodes,
                warnCodes,
                inspection.Contribution.ProposedOutcome);

            var selectedVariant = WeddingPlannerQaRulesEngine.ExtractSelectedVariant(package.DocumentJson, job.SelectedVariantId)
                ?? throw new InvalidOperationException("Selected variant snapshot is required for QA report merge.");
            var assetMeta = new QaAssetMetaSnapshot(
                job.SelectedCreativeAssetContentType,
                job.SelectedCreativeAssetByteSize,
                job.SelectedCreativeAssetWidth,
                job.SelectedCreativeAssetHeight);
            var reportCanonical = WeddingPlannerQaReviewValidation.MergeReport(
                brief,
                rules,
                chaperone,
                inspection,
                steward,
                chaperoneRun.Id,
                qaRun.Id,
                job.Id,
                selectedVariant,
                assetMeta,
                requireSynthetic);

            var nextVersion = await _db.WeddingPlannerQaReviewReportVersions
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => (int?)x.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            var report = new WeddingPlannerQaReviewReportVersion
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                VersionNumber = nextVersion + 1,
                SchemaVersion = WeddingPlannerSchemaVersions.QaReviewReportV1,
                DocumentJson = reportCanonical.DocumentJson,
                Summary = reportCanonical.Summary,
                ProducingQaReviewJobId = job.Id,
                ProducingAgentRunId = qaRun.Id,
                ApprovedCreativePackageVersionId = job.ApprovedCreativePackageVersionId,
                CreativePackageDocumentSha256 = job.CreativePackageDocumentSha256,
                CreativePackageDecisionId = job.CreativePackageDecisionId,
                SelectedVariantId = job.SelectedVariantId,
                SelectedCreativeAssetId = job.SelectedCreativeAssetId,
                SelectedCreativeAssetSha256 = job.SelectedCreativeAssetSha256,
                SelectedConceptId = job.SelectedConceptId,
                ApprovedBrandDnaVersionId = job.ApprovedBrandDnaVersionId,
                ApprovedBrandDnaVersionNumber = job.ApprovedBrandDnaVersionNumber,
                ApprovedColorProfileVersionId = job.ApprovedColorProfileVersionId,
                ApprovedColorProfileVersionNumber = job.ApprovedColorProfileVersionNumber,
                ApprovedResearchReportVersionId = job.ApprovedResearchReportVersionId,
                ApprovedResearchReportVersionNumber = job.ApprovedResearchReportVersionNumber,
                Status = WeddingPlannerQaReviewReportStatuses.Proposed,
                SourceSystem = source,
                IdempotencyKey = WeddingPlannerQaIdempotency.ReportKey(key),
                ActorType = actorType,
                ActorLabel = actorLabel,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerQaReviewReportVersions.Add(report);

            var nowComplete = DateTime.UtcNow;
            foreach (var contribution in reportCanonical.RoleContributions)
            {
                if (string.Equals(contribution.ContributionSource, WeddingPlannerQaContributionSources.Ai, StringComparison.Ordinal)
                    && contribution.ProducingAgentRunId is null)
                {
                    throw new InvalidOperationException("AI QA contributions require ProducingAgentRunId.");
                }

                if (string.Equals(contribution.LogicalRole, WeddingPlannerQaLogicalRoles.HumanEscalationSteward, StringComparison.Ordinal)
                    && contribution.ProducingAgentRunId is not null)
                {
                    throw new InvalidOperationException("Steward ProducingAgentRunId must be null.");
                }

                _db.WeddingPlannerQaRoleContributions.Add(new WeddingPlannerQaRoleContribution
                {
                    Id = Guid.NewGuid(),
                    AdvertiserId = workspace.AdvertiserId,
                    WorkspaceId = workspace.Id,
                    QaReviewReportVersionId = report.Id,
                    QaReviewJobId = job.Id,
                    LogicalRole = contribution.LogicalRole,
                    ContributionSource = contribution.ContributionSource,
                    ProducingAgentRunId = contribution.ProducingAgentRunId,
                    ContributionJson = contribution.ContributionJson,
                    CreatedAt = nowComplete
                });
            }

            qaRun.OutputQaReviewReportVersionId = report.Id;
            job.OutputQaReviewReportVersionId = report.Id;
            job.Status = WeddingPlannerQaReviewJobStatuses.Succeeded;
            job.CompletedAt = nowComplete;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewReportProposed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Proposed,
                requestId,
                $"QA review report {report.Id} proposed.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewJobSucceeded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"QA review job {job.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(job, false);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException
                                       and not WeddingPlannerForbiddenException
                                       and not WeddingPlannerProviderException)
        {
            AbandonPendingReportPersistence(job, qaRun);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<WeddingPlannerQaReviewJobResult>> ListQaReviewJobsAsync(
        Guid workspaceId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var jobs = await _db.WeddingPlannerQaReviewJobs
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return jobs.Select(j => ToJobResult(j, false)).ToList();
    }

    public async Task<WeddingPlannerQaReviewJobResult> GetQaReviewJobAsync(
        Guid qaReviewJobId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.WeddingPlannerQaReviewJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == qaReviewJobId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("QA review job was not found.");
        _planner.EnsureAdvertiserAccessForOrchestration(
            job.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA review job");
        return ToJobResult(job, false);
    }

    public async Task<WeddingPlannerQaReviewReportListResult> ListQaReviewReportsAsync(
        Guid workspaceId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var versions = await _db.WeddingPlannerQaReviewReportVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);
        var results = new List<WeddingPlannerQaReviewReportVersionResult>();
        foreach (var version in versions)
        {
            results.Add(await ToReportResultAsync(
                version,
                workspace.CurrentAcceptedQaReviewReportVersionId == version.Id,
                false,
                cancellationToken));
        }

        return new WeddingPlannerQaReviewReportListResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentAcceptedQaReviewReportVersionId,
            results);
    }

    public async Task<WeddingPlannerQaReviewReportVersionResult> GetQaReviewReportAsync(
        Guid qaReviewReportVersionId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerQaReviewReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == qaReviewReportVersionId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("QA review report was not found.");
        _planner.EnsureAdvertiserAccessForOrchestration(
            version.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA review report");
        var workspace = await _db.WeddingPlannerWorkspaces
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);
        return await ToReportResultAsync(
            version,
            workspace.CurrentAcceptedQaReviewReportVersionId == version.Id,
            false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WeddingPlannerQaRoleContributionResult>> ListContributionsAsync(
        Guid qaReviewReportVersionId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerQaReviewReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == qaReviewReportVersionId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("QA review report was not found.");
        _planner.EnsureAdvertiserAccessForOrchestration(
            version.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA review report");
        var rows = await _db.WeddingPlannerQaRoleContributions
            .AsNoTracking()
            .Where(x => x.QaReviewReportVersionId == version.Id)
            .ToListAsync(cancellationToken);
        return WeddingPlannerQaLogicalRoles.AllInOrder
            .Select(role => rows.Single(r => r.LogicalRole == role))
            .Select(r => new WeddingPlannerQaRoleContributionResult(
                r.Id, r.AdvertiserId, r.WorkspaceId, r.QaReviewReportVersionId, r.QaReviewJobId,
                r.LogicalRole, r.ContributionSource, r.ProducingAgentRunId, r.ContributionJson, r.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListReportAgentRunsAsync(
        Guid qaReviewReportVersionId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerQaReviewReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == qaReviewReportVersionId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("QA review report was not found.");
        _planner.EnsureAdvertiserAccessForOrchestration(
            version.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA review report");
        var job = await _db.WeddingPlannerQaReviewJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingQaReviewJobId, cancellationToken);
        var runIds = new[] { job.ChaperoneReviewAgentRunId, job.QaInspectionAgentRunId }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();
        var runs = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => runIds.Contains(x.Id))
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return runs.Select(r => ToAgentRunResult(r, false)).ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListWorkspaceAgentRunsAsync(
        Guid workspaceId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var runs = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return runs.Select(r => ToAgentRunResult(r, false)).ToList();
    }

    public async Task<WeddingPlannerQaReviewDecisionResult> DecideQaReviewReportAsync(
        Guid qaReviewReportVersionId,
        string decision,
        string rationale,
        string selectedVariantId,
        bool? visualReviewConfirmed,
        bool? copyReviewConfirmed,
        bool? provenanceReviewConfirmed,
        bool? syntheticMarkerAcknowledged,
        string? escalationCategory,
        JsonNode? rawNode,
        string sourceSystem,
        string idempotencyKey,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        bool canDecide,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (!canDecide)
        {
            throw new WeddingPlannerForbiddenException(
                "The authenticated identity cannot record QA review decisions.");
        }

        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var existing = await _db.WeddingPlannerQaReviewDecisions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            _planner.EnsureAdvertiserAccessForOrchestration(
                existing.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA review decision");
            var existingVersion = await _db.WeddingPlannerQaReviewReportVersions
                .SingleAsync(x => x.Id == existing.QaReviewReportVersionId, cancellationToken);
            var workspace = await _db.WeddingPlannerWorkspaces
                .SingleAsync(x => x.Id == existing.WorkspaceId, cancellationToken);
            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewReportReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"QA review decision {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            Guid? caseId = null;
            if (existing.Decision == WeddingPlannerQaReviewDecisions.Escalate)
            {
                caseId = await _db.WeddingPlannerQaEscalationCases
                    .Where(x => x.QaReviewDecisionId == existing.Id)
                    .Select(x => (Guid?)x.Id)
                    .SingleOrDefaultAsync(cancellationToken);
            }

            return new WeddingPlannerQaReviewDecisionResult(
                existing.Id,
                existing.QaReviewReportVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Decision,
                existing.SelectedVariantId,
                existing.Rationale,
                existing.VisualReviewConfirmed,
                existing.CopyReviewConfirmed,
                existing.ProvenanceReviewConfirmed,
                existing.SyntheticMarkerAcknowledged,
                existing.EscalationCategory,
                existing.ActorType,
                existing.ActorLabel,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                await ToReportResultAsync(
                    existingVersion,
                    workspace.CurrentAcceptedQaReviewReportVersionId == existingVersion.Id,
                    true,
                    cancellationToken),
                caseId,
                true);
        }

        var version = await _db.WeddingPlannerQaReviewReportVersions
            .SingleOrDefaultAsync(x => x.Id == qaReviewReportVersionId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("QA review report was not found.");
        _planner.EnsureAdvertiserAccessForOrchestration(
            version.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA review report");
        var workspaceEntity = await _db.WeddingPlannerWorkspaces
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);

        if (!string.Equals(version.Status, WeddingPlannerQaReviewReportStatuses.Proposed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only PROPOSED QA review reports accept a first decision.");
        }

        var job = await _db.WeddingPlannerQaReviewJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingQaReviewJobId, cancellationToken);
        var rulesSeverity = string.IsNullOrWhiteSpace(job.RulesFindingsJson)
            ? WeddingPlannerQaFindingSeverities.Pass
            : WeddingPlannerQaReviewValidation.ExtractRulesOverallSeverity(job.RulesFindingsJson);
        var hasSynthetic = WeddingPlannerQaReviewValidation.DocumentContainsSyntheticMarker(version.DocumentJson);
        WeddingPlannerQaReviewValidation.ValidateDecisionBody(
            decision,
            rationale,
            selectedVariantId,
            version.SelectedVariantId,
            visualReviewConfirmed,
            copyReviewConfirmed,
            provenanceReviewConfirmed,
            syntheticMarkerAcknowledged,
            escalationCategory,
            hasSynthetic,
            rulesSeverity,
            rawNode);

        var normalizedDecision = decision.Trim().ToUpperInvariant();
        var reason = rationale.Trim();
        var documentBefore = version.DocumentJson;
        var row = new WeddingPlannerQaReviewDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = version.AdvertiserId,
            WorkspaceId = version.WorkspaceId,
            QaReviewReportVersionId = version.Id,
            Decision = normalizedDecision,
            SelectedVariantId = version.SelectedVariantId,
            Rationale = reason,
            VisualReviewConfirmed = normalizedDecision == WeddingPlannerQaReviewDecisions.Accept ? true : visualReviewConfirmed,
            CopyReviewConfirmed = normalizedDecision == WeddingPlannerQaReviewDecisions.Accept ? true : copyReviewConfirmed,
            ProvenanceReviewConfirmed = normalizedDecision == WeddingPlannerQaReviewDecisions.Accept ? true : provenanceReviewConfirmed,
            SyntheticMarkerAcknowledged = normalizedDecision == WeddingPlannerQaReviewDecisions.Accept && hasSynthetic
                ? true
                : syntheticMarkerAcknowledged,
            EscalationCategory = normalizedDecision == WeddingPlannerQaReviewDecisions.Escalate
                ? escalationCategory!.Trim()
                : null,
            ActorType = actorType,
            ActorLabel = actorLabel,
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = DateTime.UtcNow
        };
        _db.WeddingPlannerQaReviewDecisions.Add(row);

        Guid? createdCaseId = null;
        if (normalizedDecision == WeddingPlannerQaReviewDecisions.Accept)
        {
            version.Status = WeddingPlannerQaReviewReportStatuses.Accepted;
            workspaceEntity.CurrentAcceptedQaReviewReportVersionId = version.Id;
            workspaceEntity.UpdatedAt = DateTime.UtcNow;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewReportAccepted,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                $"QA review report {version.Id} accepted for selectedVariantId {version.SelectedVariantId}.");
        }
        else if (normalizedDecision == WeddingPlannerQaReviewDecisions.ReturnForRevision)
        {
            version.Status = WeddingPlannerQaReviewReportStatuses.ReturnedForRevision;
            if (workspaceEntity.CurrentAcceptedQaReviewReportVersionId == version.Id)
            {
                workspaceEntity.CurrentAcceptedQaReviewReportVersionId = null;
                workspaceEntity.UpdatedAt = DateTime.UtcNow;
            }

            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewReportReturned,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                $"QA review report {version.Id} returned for revision.");
        }
        else
        {
            version.Status = WeddingPlannerQaReviewReportStatuses.Escalated;
            if (workspaceEntity.CurrentAcceptedQaReviewReportVersionId == version.Id)
            {
                workspaceEntity.CurrentAcceptedQaReviewReportVersionId = null;
                workspaceEntity.UpdatedAt = DateTime.UtcNow;
            }

            var escalationCase = new WeddingPlannerQaEscalationCase
            {
                Id = Guid.NewGuid(),
                AdvertiserId = version.AdvertiserId,
                WorkspaceId = version.WorkspaceId,
                QaReviewReportVersionId = version.Id,
                QaReviewDecisionId = row.Id,
                Category = row.EscalationCategory!,
                Status = WeddingPlannerQaEscalationCaseStatuses.Open,
                RationaleSnapshot = reason,
                SelectedVariantId = version.SelectedVariantId,
                ApprovedCreativePackageVersionId = version.ApprovedCreativePackageVersionId,
                SelectedCreativeAssetId = version.SelectedCreativeAssetId,
                ActorType = actorType,
                ActorLabel = actorLabel,
                SourceSystem = source,
                IdempotencyKey = key + ":CASE",
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerQaEscalationCases.Add(escalationCase);
            createdCaseId = escalationCase.Id;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewReportEscalated,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Created,
                requestId,
                $"QA review report {version.Id} escalated ({row.EscalationCategory}).");
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaEscalationCaseOpened,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Created,
                requestId,
                $"QA escalation case {escalationCase.Id} opened.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        if (!string.Equals(version.DocumentJson, documentBefore, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("QA review report DocumentJson must never mutate on decision.");
        }

        return new WeddingPlannerQaReviewDecisionResult(
            row.Id,
            row.QaReviewReportVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Decision,
            row.SelectedVariantId,
            row.Rationale,
            row.VisualReviewConfirmed,
            row.CopyReviewConfirmed,
            row.ProvenanceReviewConfirmed,
            row.SyntheticMarkerAcknowledged,
            row.EscalationCategory,
            row.ActorType,
            row.ActorLabel,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            await ToReportResultAsync(
                version,
                workspaceEntity.CurrentAcceptedQaReviewReportVersionId == version.Id,
                false,
                cancellationToken),
            createdCaseId,
            false);
    }

    public async Task<IReadOnlyList<WeddingPlannerQaEscalationCaseResult>> ListEscalationCasesAsync(
        Guid workspaceId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, canAccessAcrossWorkspaces, boundAdvertiserId, cancellationToken);
        var cases = await _db.WeddingPlannerQaEscalationCases
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var results = new List<WeddingPlannerQaEscalationCaseResult>();
        foreach (var item in cases)
        {
            var resolutionId = await _db.WeddingPlannerQaEscalationResolutions
                .AsNoTracking()
                .Where(x => x.QaEscalationCaseId == item.Id)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);
            results.Add(ToCaseResult(item, resolutionId, false));
        }

        return results;
    }

    public async Task<WeddingPlannerQaEscalationCaseResult> GetEscalationCaseAsync(
        Guid escalationCaseId,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var item = await _db.WeddingPlannerQaEscalationCases
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == escalationCaseId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("QA escalation case was not found.");
        _planner.EnsureAdvertiserAccessForOrchestration(
            item.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA escalation case");
        var resolutionId = await _db.WeddingPlannerQaEscalationResolutions
            .AsNoTracking()
            .Where(x => x.QaEscalationCaseId == item.Id)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return ToCaseResult(item, resolutionId, false);
    }

    public async Task<WeddingPlannerQaEscalationResolutionResult> ResolveEscalationCaseAsync(
        Guid escalationCaseId,
        string resolution,
        string rationale,
        string? exceptionRationale,
        bool? exceptionAcknowledged,
        IReadOnlyList<string>? acknowledgedBlockerCodes,
        JsonNode? rawNode,
        string sourceSystem,
        string idempotencyKey,
        bool canAccessAcrossWorkspaces,
        Guid? boundAdvertiserId,
        bool canResolveReturn,
        bool canWaive,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        if (!canResolveReturn && !canWaive)
        {
            throw new WeddingPlannerForbiddenException(
                "The authenticated identity cannot resolve QA escalation cases.");
        }

        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var existing = await _db.WeddingPlannerQaEscalationResolutions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            _planner.EnsureAdvertiserAccessForOrchestration(
                existing.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA escalation resolution");
            var existingCase = await _db.WeddingPlannerQaEscalationCases
                .SingleAsync(x => x.Id == existing.QaEscalationCaseId, cancellationToken);
            var existingVersion = await _db.WeddingPlannerQaReviewReportVersions
                .SingleAsync(x => x.Id == existing.QaReviewReportVersionId, cancellationToken);
            var workspace = await _db.WeddingPlannerWorkspaces
                .SingleAsync(x => x.Id == existing.WorkspaceId, cancellationToken);
            return new WeddingPlannerQaEscalationResolutionResult(
                existing.Id,
                existing.QaEscalationCaseId,
                existing.QaReviewReportVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Resolution,
                existing.Rationale,
                existing.ExceptionRationale,
                existing.ExceptionAcknowledged,
                DeserializeStringArray(existing.AcknowledgedBlockerCodesJson),
                existing.ActorType,
                existing.ActorLabel,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                ToCaseResult(existingCase, existing.Id, true),
                await ToReportResultAsync(
                    existingVersion,
                    workspace.CurrentAcceptedQaReviewReportVersionId == existingVersion.Id,
                    true,
                    cancellationToken),
                true);
        }

        var escalationCase = await _db.WeddingPlannerQaEscalationCases
            .SingleOrDefaultAsync(x => x.Id == escalationCaseId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("QA escalation case was not found.");
        _planner.EnsureAdvertiserAccessForOrchestration(
            escalationCase.AdvertiserId, canAccessAcrossWorkspaces, boundAdvertiserId, "QA escalation case");
        if (!string.Equals(escalationCase.Status, WeddingPlannerQaEscalationCaseStatuses.Open, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only OPEN escalation cases accept a first resolution.");
        }

        var version = await _db.WeddingPlannerQaReviewReportVersions
            .SingleAsync(x => x.Id == escalationCase.QaReviewReportVersionId, cancellationToken);
        var workspaceEntity = await _db.WeddingPlannerWorkspaces
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);
        var job = await _db.WeddingPlannerQaReviewJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingQaReviewJobId, cancellationToken);
        var expectedBlockers = string.IsNullOrWhiteSpace(job.RulesFindingsJson)
            ? Array.Empty<string>()
            : WeddingPlannerQaReviewValidation.ExtractBlockerCodes(job.RulesFindingsJson);

        var normalized = resolution.Trim().ToUpperInvariant();
        if (normalized == WeddingPlannerQaEscalationResolutions.ReturnForRevision && !canResolveReturn)
        {
            throw new WeddingPlannerForbiddenException(
                "The authenticated identity cannot resolve QA escalation cases.");
        }

        WeddingPlannerQaReviewValidation.ValidateResolutionBody(
            resolution,
            rationale,
            exceptionRationale,
            exceptionAcknowledged,
            acknowledgedBlockerCodes,
            expectedBlockers,
            canWaive,
            rawNode);

        var documentBefore = version.DocumentJson;
        var row = new WeddingPlannerQaEscalationResolution
        {
            Id = Guid.NewGuid(),
            AdvertiserId = version.AdvertiserId,
            WorkspaceId = version.WorkspaceId,
            QaEscalationCaseId = escalationCase.Id,
            QaReviewReportVersionId = version.Id,
            Resolution = normalized,
            Rationale = rationale.Trim(),
            ExceptionRationale = normalized == WeddingPlannerQaEscalationResolutions.WaiveAndAccept
                ? exceptionRationale!.Trim()
                : null,
            ExceptionAcknowledged = normalized == WeddingPlannerQaEscalationResolutions.WaiveAndAccept
                ? true
                : exceptionAcknowledged,
            AcknowledgedBlockerCodesJson = normalized == WeddingPlannerQaEscalationResolutions.WaiveAndAccept
                ? JsonSerializer.Serialize(expectedBlockers, JsonOptions)
                : null,
            ActorType = actorType,
            ActorLabel = actorLabel,
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = DateTime.UtcNow
        };
        _db.WeddingPlannerQaEscalationResolutions.Add(row);
        escalationCase.Status = WeddingPlannerQaEscalationCaseStatuses.Resolved;

        if (normalized == WeddingPlannerQaEscalationResolutions.ReturnForRevision)
        {
            version.Status = WeddingPlannerQaReviewReportStatuses.ReturnedForRevision;
            if (workspaceEntity.CurrentAcceptedQaReviewReportVersionId == version.Id)
            {
                workspaceEntity.CurrentAcceptedQaReviewReportVersionId = null;
                workspaceEntity.UpdatedAt = DateTime.UtcNow;
            }

            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewReportReturned,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                $"QA review report {version.Id} returned via escalation resolution.");
        }
        else
        {
            version.Status = WeddingPlannerQaReviewReportStatuses.AcceptedWithException;
            workspaceEntity.CurrentAcceptedQaReviewReportVersionId = version.Id;
            workspaceEntity.UpdatedAt = DateTime.UtcNow;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.QaReviewReportAcceptedWithException,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                $"QA review report {version.Id} accepted with exception.");
        }

        _planner.AddAuditForOrchestration(
            version.AdvertiserId,
            version.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.QaEscalationCaseResolved,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Succeeded,
            requestId,
            $"QA escalation case {escalationCase.Id} resolved ({normalized}).");
        await _db.SaveChangesAsync(cancellationToken);
        if (!string.Equals(version.DocumentJson, documentBefore, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("QA review report DocumentJson must never mutate on resolution.");
        }

        return new WeddingPlannerQaEscalationResolutionResult(
            row.Id,
            row.QaEscalationCaseId,
            row.QaReviewReportVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Resolution,
            row.Rationale,
            row.ExceptionRationale,
            row.ExceptionAcknowledged,
            DeserializeStringArray(row.AcknowledgedBlockerCodesJson),
            row.ActorType,
            row.ActorLabel,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            ToCaseResult(escalationCase, row.Id, false),
            await ToReportResultAsync(
                version,
                workspaceEntity.CurrentAcceptedQaReviewReportVersionId == version.Id,
                false,
                cancellationToken),
            false);
    }

    private async Task<CanonicalQaRulesFindings> EvaluateRulesAsync(
        WeddingPlannerQaReviewJob job,
        WeddingPlannerWorkspace workspace,
        WeddingPlannerCreativePackageVersion package,
        WeddingPlannerCreativePackageDecision latestApprove,
        WeddingPlannerCreativeAsset asset,
        CancellationToken cancellationToken)
    {
        var producingJob = await _db.WeddingPlannerCreativeProductionJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == package.ProducingCreativeProductionJobId, cancellationToken);
        var contributionCount = await _db.WeddingPlannerCreativeRoleContributions
            .CountAsync(x => x.CreativePackageVersionId == package.Id, cancellationToken);
        var assetsForVariant = await _db.WeddingPlannerCreativeAssets
            .CountAsync(x => x.CreativePackageVersionId == package.Id && x.VariantId == job.SelectedVariantId, cancellationToken);

        var phase6RunIds = new List<Guid>();
        var successfulCount = 0;
        if (producingJob is not null)
        {
            var ids = new Guid?[]
            {
                producingJob.CreativeDirectionAgentRunId,
                producingJob.StrategyAdaptationAgentRunId,
                producingJob.VisualSystemAgentRunId,
                producingJob.ImageDirectionAgentRunId,
                producingJob.CopySystemAgentRunId,
                producingJob.VariantProductionAgentRunId
            };
            foreach (var id in ids)
            {
                if (id is Guid runId)
                {
                    phase6RunIds.Add(runId);
                }
            }

            if (phase6RunIds.Count > 0)
            {
                successfulCount = await _db.WeddingPlannerAgentRuns.CountAsync(
                    x => phase6RunIds.Contains(x.Id)
                         && x.Status == WeddingPlannerAgentRunStatuses.Succeeded
                         && WeddingPlannerCreativeDepartmentWorkerProfiles.All.Contains(x.WorkerProfileVersion!),
                    cancellationToken);
            }
        }

        var trackedAsset = await _db.WeddingPlannerCreativeAssets
            .AsNoTracking()
            .SingleAsync(x => x.Id == asset.Id, cancellationToken);
        var selectedVariant = WeddingPlannerQaRulesEngine.ExtractSelectedVariant(package.DocumentJson, job.SelectedVariantId);

        // Load pinned provenance rows; missing/cross-tenant => ProvenancePinsValid=false (BLOCK), never throw.
        var conceptPackage = await _db.WeddingPlannerConceptPackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == package.ApprovedConceptPackageVersionId, cancellationToken);
        var brandDna = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == job.ApprovedBrandDnaVersionId, cancellationToken);
        var color = await _db.WeddingPlannerColorProfileVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == job.ApprovedColorProfileVersionId, cancellationToken);
        var research = await _db.WeddingPlannerResearchReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == job.ApprovedResearchReportVersionId, cancellationToken);

        var conceptOk = conceptPackage is not null
                        && conceptPackage.WorkspaceId == workspace.Id
                        && conceptPackage.AdvertiserId == workspace.AdvertiserId
                        && conceptPackage.Id == package.ApprovedConceptPackageVersionId;
        var dnaOk = brandDna is not null
                    && brandDna.WorkspaceId == workspace.Id
                    && brandDna.AdvertiserId == workspace.AdvertiserId
                    && brandDna.Id == job.ApprovedBrandDnaVersionId
                    && brandDna.VersionNumber == job.ApprovedBrandDnaVersionNumber
                    && brandDna.Id == package.ApprovedBrandDnaVersionId
                    && brandDna.VersionNumber == package.ApprovedBrandDnaVersionNumber;
        var colorOk = color is not null
                      && color.WorkspaceId == workspace.Id
                      && color.AdvertiserId == workspace.AdvertiserId
                      && color.Id == job.ApprovedColorProfileVersionId
                      && color.VersionNumber == job.ApprovedColorProfileVersionNumber
                      && color.Id == package.ApprovedColorProfileVersionId
                      && color.VersionNumber == package.ApprovedColorProfileVersionNumber;
        var researchOk = research is not null
                         && research.WorkspaceId == workspace.Id
                         && research.AdvertiserId == workspace.AdvertiserId
                         && research.Id == job.ApprovedResearchReportVersionId
                         && research.VersionNumber == job.ApprovedResearchReportVersionNumber
                         && research.Id == package.ApprovedResearchReportVersionId
                         && research.VersionNumber == package.ApprovedResearchReportVersionNumber;
        var provenancePinsValid = conceptOk && dnaOk && colorOk && researchOk;

        IReadOnlySet<string> knownPaletteRoles = new HashSet<string>(StringComparer.Ordinal);
        if (color is not null && colorOk)
        {
            try
            {
                knownPaletteRoles = WeddingPlannerCreativeDepartmentValidation.ExtractPaletteRoleNames(color.DocumentJson);
            }
            catch
            {
                knownPaletteRoles = new HashSet<string>(StringComparer.Ordinal);
            }
        }

        SelectedConceptSnapshot? selectedConcept = null;
        if (conceptPackage is not null && conceptOk)
        {
            try
            {
                selectedConcept = WeddingPlannerCreativeDepartmentValidation.ExtractSelectedConceptSnapshot(
                    conceptPackage.DocumentJson,
                    package.SelectedConceptId);
            }
            catch
            {
                selectedConcept = null;
            }
        }

        var context = new WeddingPlannerQaRulesContext(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentApprovedCreativePackageVersionId,
            job.ApprovedCreativePackageVersionId,
            job.CreativePackageDecisionId,
            job.SelectedVariantId,
            job.SelectedCreativeAssetId,
            job.SelectedCreativeAssetSha256,
            job.SelectedCreativeAssetContentType,
            job.SelectedCreativeAssetByteSize,
            job.SelectedCreativeAssetWidth,
            job.SelectedCreativeAssetHeight,
            job.SelectedConceptId,
            job.ApprovedBrandDnaVersionId,
            job.ApprovedBrandDnaVersionNumber,
            job.ApprovedColorProfileVersionId,
            job.ApprovedColorProfileVersionNumber,
            job.ApprovedResearchReportVersionId,
            job.ApprovedResearchReportVersionNumber,
            package,
            latestApprove,
            trackedAsset,
            assetsForVariant,
            contributionCount,
            successfulCount,
            phase6RunIds,
            selectedVariant,
            selectedConcept,
            provenancePinsValid,
            knownPaletteRoles);

        return WeddingPlannerQaRulesEngine.Evaluate(context);
    }

    private async Task<WeddingPlannerAgentRun> ExecuteStageAsync(
        WeddingPlannerQaReviewJob job,
        WeddingPlannerWorkspace workspace,
        WeddingPlannerCreativePackageVersion package,
        CanonicalQaRulesFindings rules,
        Dictionary<string, string> priorStageOutputs,
        string workerProfileVersion,
        bool requireSyntheticMarker,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var stageRole = WeddingPlannerQaWorkerProfiles.StageLogicalRole(workerProfileVersion);
        var promptPack = WeddingPlannerQaWorkerProfiles.PromptPack(workerProfileVersion);
        var assignedRoles = WeddingPlannerQaWorkerProfiles.AssignedRoles(workerProfileVersion);
        var stageKey = WeddingPlannerQaIdempotency.StageKey(job.IdempotencyKey, workerProfileVersion);

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
        if (workerProfileVersion == WeddingPlannerQaWorkerProfiles.ChaperoneReviewV1)
        {
            job.ChaperoneReviewAgentRunId = run.Id;
        }
        else
        {
            job.QaInspectionAgentRunId = run.Id;
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
            $"QA run {run.Id} ({workerProfileVersion}) started.");
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var messages = BuildStageMessages(job, package, rules, priorStageOutputs, workerProfileVersion);
            AssertAiContextExcludesBytes(messages);
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
            if (workerProfileVersion == WeddingPlannerQaWorkerProfiles.ChaperoneReviewV1)
            {
                var canonical = WeddingPlannerQaReviewValidation.CanonicalizeChaperoneOutput(
                    completion.Content,
                    job.SelectedVariantId,
                    requireSyntheticMarker);
                canonicalJson = canonical.CanonicalJson;
                job.ChaperoneReviewStageOutputJson = canonicalJson;
            }
            else
            {
                var canonical = WeddingPlannerQaReviewValidation.CanonicalizeQaInspectionOutput(
                    completion.Content,
                    job.SelectedVariantId,
                    rules.OverallSeverity,
                    requireSyntheticMarker);
                canonicalJson = canonical.CanonicalJson;
                job.QaInspectionStageOutputJson = canonicalJson;
            }

            priorStageOutputs[workerProfileVersion] = canonicalJson;
            run.ProviderKey = completion.ProviderKey;
            run.ModelId = completion.ModelId;
            run.AdapterVersion = completion.AdapterVersion;
            run.ProviderRequestId = completion.ProviderRequestId;
            run.WorkerKey = completion.WorkerKey;
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
                $"QA run {run.Id} ({workerProfileVersion}) succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailRunAsync(run, workspace.AdvertiserId, workspace.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw new WeddingPlannerProviderException(run.Id, "Wedding Planner QA AI provider failed.", ex);
        }
    }

    private static IReadOnlyList<WeddingPlannerAiMessage> BuildStageMessages(
        WeddingPlannerQaReviewJob job,
        WeddingPlannerCreativePackageVersion package,
        CanonicalQaRulesFindings rules,
        IReadOnlyDictionary<string, string> priorStageOutputs,
        string workerProfileVersion)
    {
        var selectedVariant = WeddingPlannerQaRulesEngine.ExtractSelectedVariant(package.DocumentJson, job.SelectedVariantId);
        var packageSummary = new
        {
            schemaVersion = package.SchemaVersion,
            packageId = package.Id,
            selectedConceptId = package.SelectedConceptId,
            provenance = new
            {
                package.ApprovedBrandDnaVersionId,
                package.ApprovedBrandDnaVersionNumber,
                package.ApprovedColorProfileVersionId,
                package.ApprovedColorProfileVersionNumber,
                package.ApprovedResearchReportVersionId,
                package.ApprovedResearchReportVersionNumber
            },
            contributionCount = 13,
            selectedVariant,
            selectedAssetMetadata = new
            {
                creativeAssetId = job.SelectedCreativeAssetId,
                contentType = job.SelectedCreativeAssetContentType,
                byteSize = job.SelectedCreativeAssetByteSize,
                sha256 = job.SelectedCreativeAssetSha256,
                width = job.SelectedCreativeAssetWidth,
                height = job.SelectedCreativeAssetHeight
            }
        };

        var messages = new List<WeddingPlannerAiMessage>
        {
            new(WeddingPlannerActorTypes.System,
                JsonSerializer.Serialize(new
                {
                    brief = new
                    {
                        job.ReviewObjective,
                        focusAreas = JsonSerializer.Deserialize<string[]>(job.FocusAreasJson) ?? Array.Empty<string>(),
                        job.Notes
                    },
                    pins = new
                    {
                        job.ApprovedCreativePackageVersionId,
                        job.CreativePackageDocumentSha256,
                        job.CreativePackageDecisionId,
                        job.SelectedVariantId,
                        job.SelectedCreativeAssetId,
                        job.SelectedCreativeAssetSha256
                    },
                    packageSummary,
                    rules = JsonNode.Parse(rules.FindingsJson),
                    workerProfileVersion
                }, JsonOptions))
        };

        foreach (var prior in priorStageOutputs.Values)
        {
            messages.Add(new WeddingPlannerAiMessage(WeddingPlannerActorTypes.Planner, prior));
        }

        return messages;
    }

    private static void AssertAiContextExcludesBytes(IReadOnlyList<WeddingPlannerAiMessage> messages)
    {
        foreach (var message in messages)
        {
            var body = message.Body;
            if (body.Contains("\"bytes\"", StringComparison.OrdinalIgnoreCase)
                || body.Contains("base64", StringComparison.OrdinalIgnoreCase)
                || body.Contains("imageBytes", StringComparison.OrdinalIgnoreCase)
                || body.Contains("/creative-assets/", StringComparison.OrdinalIgnoreCase)
                || body.Contains("data:image", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("QA AI context must not include image bytes, base64, or asset content URLs.");
            }
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
        WeddingPlannerQaReviewJob job,
        string actorType,
        string actorLabel,
        string? requestId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        job.Status = WeddingPlannerQaReviewJobStatuses.Failed;
        job.ErrorCode = Truncate(ex.GetType().Name, 64);
        job.ErrorMessage = Truncate(ex.Message, 2000);
        job.CompletedAt = DateTime.UtcNow;
        job.OutputQaReviewReportVersionId = null;
        _planner.AddAuditForOrchestration(
            job.AdvertiserId,
            job.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.QaReviewJobFailed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Failed,
            requestId,
            $"QA review job {job.Id} failed: {job.ErrorCode}");
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
            $"QA run {run.Id} failed.");
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void AbandonPendingReportPersistence(
        WeddingPlannerQaReviewJob job,
        WeddingPlannerAgentRun? qaRun)
    {
        job.OutputQaReviewReportVersionId = null;
        job.OutputQaReviewReportVersion = null;
        if (qaRun is not null)
        {
            qaRun.OutputQaReviewReportVersionId = null;
            qaRun.OutputQaReviewReportVersion = null;
        }

        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerQaRoleContribution>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.Entity.QaReviewReportVersion = null!;
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerQaReviewReportVersion>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerAuditEvent>()
            .Where(e => e.State == EntityState.Added
                        && (e.Entity.Action == WeddingPlannerAuditActions.QaReviewReportProposed
                            || e.Entity.Action == WeddingPlannerAuditActions.QaReviewJobSucceeded))
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task<WeddingPlannerQaReviewReportVersionResult> ToReportResultAsync(
        WeddingPlannerQaReviewReportVersion version,
        bool isCurrentAccepted,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var job = await _db.WeddingPlannerQaReviewJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == version.ProducingQaReviewJobId, cancellationToken);
        decimal? cost = null;
        if (job is not null)
        {
            var runIds = new[] { job.ChaperoneReviewAgentRunId, job.QaInspectionAgentRunId }
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToArray();
            if (runIds.Length > 0)
            {
                cost = await _db.WeddingPlannerAgentRuns
                    .AsNoTracking()
                    .Where(x => runIds.Contains(x.Id))
                    .SumAsync(x => x.EstimatedCostUsd ?? 0m, cancellationToken);
            }
        }

        string? severity = null;
        if (!string.IsNullOrWhiteSpace(job?.RulesFindingsJson))
        {
            severity = WeddingPlannerQaReviewValidation.ExtractRulesOverallSeverity(job.RulesFindingsJson);
        }

        return new WeddingPlannerQaReviewReportVersionResult(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.DocumentJson,
            version.Summary,
            version.ProducingQaReviewJobId,
            version.ProducingAgentRunId,
            version.ApprovedCreativePackageVersionId,
            version.CreativePackageDocumentSha256,
            version.CreativePackageDecisionId,
            version.SelectedVariantId,
            version.SelectedCreativeAssetId,
            version.SelectedCreativeAssetSha256,
            version.SelectedConceptId,
            version.ApprovedBrandDnaVersionId,
            version.ApprovedBrandDnaVersionNumber,
            version.ApprovedColorProfileVersionId,
            version.ApprovedColorProfileVersionNumber,
            version.ApprovedResearchReportVersionId,
            version.ApprovedResearchReportVersionNumber,
            version.Status,
            version.SourceSystem,
            version.IdempotencyKey,
            version.ActorType,
            version.ActorLabel,
            version.CreatedAt,
            isCurrentAccepted,
            severity,
            cost,
            isReplay);
    }

    private static WeddingPlannerQaReviewJobResult ToJobResult(WeddingPlannerQaReviewJob job, bool isReplay)
    {
        var focus = JsonSerializer.Deserialize<string[]>(job.FocusAreasJson, JsonOptions) ?? Array.Empty<string>();
        string? severity = null;
        if (!string.IsNullOrWhiteSpace(job.RulesFindingsJson))
        {
            severity = WeddingPlannerQaReviewValidation.ExtractRulesOverallSeverity(job.RulesFindingsJson);
        }

        return new WeddingPlannerQaReviewJobResult(
            job.Id,
            job.AdvertiserId,
            job.WorkspaceId,
            job.ReviewObjective,
            focus,
            job.Notes,
            job.InputJson,
            job.InputSha256,
            job.ApprovedCreativePackageVersionId,
            job.CreativePackageDocumentSha256,
            job.CreativePackageDecisionId,
            job.SelectedVariantId,
            job.SelectedCreativeAssetId,
            job.SelectedCreativeAssetSha256,
            job.SelectedCreativeAssetContentType,
            job.SelectedCreativeAssetByteSize,
            job.SelectedCreativeAssetWidth,
            job.SelectedCreativeAssetHeight,
            job.SelectedConceptId,
            job.ApprovedBrandDnaVersionId,
            job.ApprovedBrandDnaVersionNumber,
            job.ApprovedColorProfileVersionId,
            job.ApprovedColorProfileVersionNumber,
            job.ApprovedResearchReportVersionId,
            job.ApprovedResearchReportVersionNumber,
            job.RulesFindingsJson,
            severity,
            job.ChaperoneReviewAgentRunId,
            job.QaInspectionAgentRunId,
            job.OutputQaReviewReportVersionId,
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

    private static WeddingPlannerQaEscalationCaseResult ToCaseResult(
        WeddingPlannerQaEscalationCase item,
        Guid? resolutionId,
        bool isReplay) =>
        new(
            item.Id,
            item.AdvertiserId,
            item.WorkspaceId,
            item.QaReviewReportVersionId,
            item.QaReviewDecisionId,
            item.Category,
            item.Status,
            item.RationaleSnapshot,
            item.SelectedVariantId,
            item.ApprovedCreativePackageVersionId,
            item.SelectedCreativeAssetId,
            item.ActorType,
            item.ActorLabel,
            item.SourceSystem,
            item.IdempotencyKey,
            item.CreatedAt,
            resolutionId,
            isReplay);

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

    private static IReadOnlyList<string>? DeserializeStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<string[]>(json, JsonOptions);
    }

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
