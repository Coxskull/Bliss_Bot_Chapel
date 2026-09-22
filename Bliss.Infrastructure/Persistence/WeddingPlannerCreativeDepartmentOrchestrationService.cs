using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerCreativeProductionJobResult(
    Guid CreativeProductionJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string JobKind,
    string Objective,
    IReadOnlyList<string> Formats,
    int RequestedVariantCount,
    Guid? RevisionParentCreativePackageVersionId,
    string? RevisionNotes,
    string InputJson,
    string InputSha256,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    Guid? CreativeDirectionAgentRunId,
    Guid? StrategyAdaptationAgentRunId,
    Guid? VisualSystemAgentRunId,
    Guid? ImageDirectionAgentRunId,
    Guid? CopySystemAgentRunId,
    Guid? VariantProductionAgentRunId,
    string? AssetProviderKey,
    string? AssetProviderAdapterVersion,
    string? AssetProviderRequestId,
    decimal? AssetProviderEstimatedCostUsd,
    Guid? OutputCreativePackageVersionId,
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

public sealed record WeddingPlannerCreativePackageVersionResult(
    Guid CreativePackageVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingCreativeProductionJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string JobKind,
    Guid? ParentCreativePackageVersionId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    decimal? EstimatedTotalCostUsd,
    decimal? EstimatedAssetCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerCreativePackageListResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedCreativePackageVersionId,
    IReadOnlyList<WeddingPlannerCreativePackageVersionResult> Versions);

public sealed record WeddingPlannerCreativeRoleContributionResult(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid CreativePackageVersionId,
    Guid CreativeProductionJobId,
    string LogicalRole,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerCreativeAssetResult(
    Guid CreativeAssetId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid CreativePackageVersionId,
    Guid CreativeProductionJobId,
    string VariantId,
    string Format,
    int Width,
    int Height,
    string ContentType,
    int ByteSize,
    string Sha256,
    string ProviderKey,
    string AdapterVersion,
    string? ProviderRequestId,
    decimal? EstimatedCostUsd,
    DateTime CreatedAt);

public sealed record WeddingPlannerCreativeAssetContentResult(
    Guid CreativeAssetId,
    byte[] Bytes,
    string ContentType,
    string Sha256,
    string FileName);

public sealed record WeddingPlannerCreativePackageDecisionResult(
    Guid DecisionId,
    Guid CreativePackageVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string? SelectedVariantId,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerCreativePackageVersionResult Version,
    bool IsReplay);

/// <summary>
/// Creative Production saga: exactly six AI stage profiles, deterministic merge, then per-variant
/// asset provider calls. Package + 13 contributions + N assets are created only after all assets
/// validate. Phase 5 concept profiles are never invoked. Never accepts client-uploaded bytes.
/// </summary>
public sealed class WeddingPlannerCreativeDepartmentOrchestrationService
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
    private readonly IWeddingPlannerCreativeAssetProvider _assets;
    private readonly WeddingPlannerAiOptions _aiOptions;
    private readonly WeddingPlannerCreativeAssetOptions _assetOptions;

    public WeddingPlannerCreativeDepartmentOrchestrationService(
        BlissDbContext db,
        WeddingPlannerService planner,
        IWeddingPlannerAiProvider ai,
        IWeddingPlannerCreativeAssetProvider assets,
        IOptions<WeddingPlannerAiOptions> aiOptions,
        IOptions<WeddingPlannerCreativeAssetOptions> assetOptions)
    {
        _db = db;
        _planner = planner;
        _ai = ai;
        _assets = assets;
        _aiOptions = aiOptions.Value;
        _assetOptions = assetOptions.Value;
    }

    public async Task<WeddingPlannerCreativeProductionJobResult> CreateCreativeProductionJobAsync(
        Guid workspaceId,
        string jobKind,
        string objective,
        IReadOnlyList<string> formats,
        int requestedVariantCount,
        Guid? revisionParentCreativePackageVersionId,
        string? revisionNotes,
        int? clientCanvasWidth,
        int? clientCanvasHeight,
        JsonNode? rawBriefNode,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        EnsureAiProviderAllowed();
        EnsureAssetProviderAllowed();

        if (rawBriefNode is not null)
        {
            WeddingPlannerCreativeDepartmentValidation.RejectForbiddenBriefFields(rawBriefNode);
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), WeddingPlannerCreativeDepartmentIdempotency.MaxJobIdempotencyKeyLength);

        var existing = await _db.WeddingPlannerCreativeProductionJobs
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkspaceId != workspace.Id || existing.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Creative production job was not found.");
            }

            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.CreativeProductionJobReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Creative production job {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(existing, true);
        }

        if (workspace.CurrentApprovedConceptPackageVersionId is null)
        {
            throw new InvalidOperationException(
                "A current-approved concept package with SelectedConceptId is required before starting a creative-production job.");
        }

        var conceptPackage = await _db.WeddingPlannerConceptPackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedConceptPackageVersionId.Value, cancellationToken);
        if (conceptPackage is null
            || conceptPackage.WorkspaceId != workspace.Id
            || !string.Equals(conceptPackage.Status, WeddingPlannerConceptPackageStatuses.Approved, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Current-approved concept package must exist with APPROVED status.");
        }

        var latestApprove = await _db.WeddingPlannerConceptPackageDecisions
            .AsNoTracking()
            .Where(x => x.ConceptPackageVersionId == conceptPackage.Id
                        && x.Decision == WeddingPlannerConceptPackageDecisions.Approve)
            .OrderByDescending(x => x.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestApprove is null
            || string.IsNullOrWhiteSpace(latestApprove.SelectedConceptId)
            || !WeddingPlannerConceptIds.All.Contains(latestApprove.SelectedConceptId))
        {
            throw new InvalidOperationException(
                "Latest APPROVE decision for the current-approved concept package must carry a valid SelectedConceptId.");
        }

        var selectedConceptId = latestApprove.SelectedConceptId;
        var selectedConcept = WeddingPlannerCreativeDepartmentValidation.ExtractSelectedConceptSnapshot(
            conceptPackage.DocumentJson, selectedConceptId);

        // Concept package pins win over possibly-drifted workspace pointers.
        var brandDna = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conceptPackage.ApprovedBrandDnaVersionId, cancellationToken);
        var color = await _db.WeddingPlannerColorProfileVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conceptPackage.ApprovedColorProfileVersionId, cancellationToken);
        var research = await _db.WeddingPlannerResearchReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conceptPackage.ApprovedResearchReportVersionId, cancellationToken);
        if (brandDna is null
            || color is null
            || research is null
            || brandDna.WorkspaceId != workspace.Id
            || color.WorkspaceId != workspace.Id
            || research.WorkspaceId != workspace.Id)
        {
            throw new InvalidOperationException(
                "Pinned Brand DNA, Color Profile, and Research Report from the concept package are required.");
        }

        var requireSynthetic = IsLocalAiProvider();
        var brief = WeddingPlannerCreativeDepartmentValidation.CanonicalizeBrief(
            jobKind,
            objective,
            formats,
            requestedVariantCount,
            revisionParentCreativePackageVersionId,
            revisionNotes,
            conceptPackage.Id,
            selectedConceptId,
            brandDna.Id,
            conceptPackage.ApprovedBrandDnaVersionNumber,
            color.Id,
            conceptPackage.ApprovedColorProfileVersionNumber,
            research.Id,
            conceptPackage.ApprovedResearchReportVersionNumber,
            clientCanvasWidth,
            clientCanvasHeight,
            string.IsNullOrWhiteSpace(_aiOptions.Provider)
                ? WeddingPlannerAiProviderKinds.Local
                : _aiOptions.Provider.Trim(),
            _ai.WorkerKey,
            string.IsNullOrWhiteSpace(_assetOptions.Provider)
                ? WeddingPlannerCreativeAssetProviderKinds.Local
                : _assetOptions.Provider.Trim(),
            _assets.WorkerKey);

        if (brief.JobKind == WeddingPlannerCreativeProductionJobKinds.Revision)
        {
            await ValidateRevisionParentAsync(workspace, brief, cancellationToken);
        }

        var knownPaletteRoles = WeddingPlannerCreativeDepartmentValidation.ExtractPaletteRoleNames(color.DocumentJson);
        var forbiddenSourceIds = new HashSet<string>(StringComparer.Ordinal)
        {
            brandDna.Id.ToString("D"),
            color.Id.ToString("D"),
            research.Id.ToString("D"),
            conceptPackage.Id.ToString("D")
        };

        var now = DateTime.UtcNow;
        var job = new WeddingPlannerCreativeProductionJob
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            JobKind = brief.JobKind,
            Objective = brief.Objective,
            FormatsJson = JsonSerializer.Serialize(brief.Formats, JsonOptions),
            RequestedVariantCount = brief.RequestedVariantCount,
            RevisionParentCreativePackageVersionId = brief.RevisionParentCreativePackageVersionId,
            RevisionNotes = brief.RevisionNotes,
            InputJson = brief.InputJson,
            InputSha256 = brief.InputSha256,
            ApprovedConceptPackageVersionId = conceptPackage.Id,
            SelectedConceptId = selectedConceptId,
            ApprovedBrandDnaVersionId = brandDna.Id,
            ApprovedBrandDnaVersionNumber = conceptPackage.ApprovedBrandDnaVersionNumber,
            ApprovedColorProfileVersionId = color.Id,
            ApprovedColorProfileVersionNumber = conceptPackage.ApprovedColorProfileVersionNumber,
            ApprovedResearchReportVersionId = research.Id,
            ApprovedResearchReportVersionNumber = conceptPackage.ApprovedResearchReportVersionNumber,
            Status = WeddingPlannerCreativeProductionJobStatuses.Running,
            SourceSystem = source,
            IdempotencyKey = key,
            ActorType = actorType,
            ActorLabel = actorLabel,
            StartedAt = now
        };
        _db.WeddingPlannerCreativeProductionJobs.Add(job);
        _planner.AddAuditForOrchestration(
            workspace.AdvertiserId,
            workspace.Id,
            null,
            null,
            WeddingPlannerAuditActions.CreativeProductionJobStarted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"Creative production job {job.Id} started.");
        await _db.SaveChangesAsync(cancellationToken);

        forbiddenSourceIds.Add(job.Id.ToString("D"));

        var stageOutputs = new List<CanonicalCreativeStageOutput>(6);
        var stageRuns = new Dictionary<string, WeddingPlannerAgentRun>(StringComparer.Ordinal);

        foreach (var profile in WeddingPlannerCreativeDepartmentWorkerProfiles.All)
        {
            var run = await ExecuteStageAsync(
                job,
                workspace,
                brandDna,
                color,
                research,
                conceptPackage,
                selectedConcept,
                brief,
                stageOutputs,
                profile,
                knownPaletteRoles,
                forbiddenSourceIds,
                requireSynthetic,
                actorType,
                actorLabel,
                requestId,
                cancellationToken);
            stageRuns[profile] = run;
        }

        CanonicalCreativePackagePlan plan;
        try
        {
            plan = WeddingPlannerCreativeDepartmentValidation.MergePackagePlan(
                brief,
                selectedConcept,
                stageOutputs,
                job.Id,
                requireSynthetic);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException
                                       and not WeddingPlannerForbiddenException
                                       and not WeddingPlannerProviderException)
        {
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }

        List<(CanonicalCreativePlannedVariant Variant, WeddingPlannerCreativeAssetGenerationResult Generated, WeddingPlannerValidatedPng Validated)> generated;
        try
        {
            generated = await GenerateAndValidateAssetsAsync(
                job, brief, plan, actorType, actorLabel, requestId, cancellationToken);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException
                                       and not WeddingPlannerForbiddenException)
        {
            // If asset-provider Succeeded audit was tracked but its SaveChanges failed,
            // drop it so FailJobAsync does not persist a success audit alongside FAILED.
            DetachAddedAudits(WeddingPlannerAuditActions.CreativeAssetProviderSucceeded);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            if (ex is WeddingPlannerCreativeAssetProviderException assetEx)
            {
                throw new WeddingPlannerCreativeAssetJobProviderException(job.Id, assetEx.Message, assetEx.ErrorCode, assetEx);
            }

            throw;
        }

        try
        {
            var assetRefs = new List<CanonicalCreativeAssetRef>(generated.Count);
            var assetEntities = new List<WeddingPlannerCreativeAsset>(generated.Count);
            var nowAssets = DateTime.UtcNow;
            foreach (var item in generated)
            {
                var assetId = Guid.NewGuid();
                assetRefs.Add(new CanonicalCreativeAssetRef(
                    item.Variant.Id,
                    assetId,
                    WeddingPlannerCreativeAssetContentTypes.ImagePng,
                    item.Validated.ByteSize,
                    item.Validated.Sha256,
                    item.Validated.Width,
                    item.Validated.Height));
                assetEntities.Add(new WeddingPlannerCreativeAsset
                {
                    Id = assetId,
                    AdvertiserId = workspace.AdvertiserId,
                    WorkspaceId = workspace.Id,
                    CreativePackageVersionId = Guid.Empty, // set after package id known
                    CreativeProductionJobId = job.Id,
                    VariantId = item.Variant.Id,
                    Format = item.Variant.Format,
                    Width = item.Validated.Width,
                    Height = item.Validated.Height,
                    ContentType = WeddingPlannerCreativeAssetContentTypes.ImagePng,
                    Bytes = item.Validated.Bytes,
                    ByteSize = item.Validated.ByteSize,
                    Sha256 = item.Validated.Sha256,
                    ProviderKey = item.Generated.ProviderKey,
                    AdapterVersion = item.Generated.AdapterVersion,
                    ProviderRequestId = item.Generated.ProviderRequestId,
                    ReceiptJson = item.Generated.ReceiptHeadersJson,
                    EstimatedCostUsd = item.Generated.EstimatedCostUsd,
                    CreatedAt = nowAssets
                });
            }

            var packageCanonical = WeddingPlannerCreativeDepartmentValidation.FinalizePackageDocument(plan, assetRefs);

            var nextVersion = await _db.WeddingPlannerCreativePackageVersions
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => (int?)x.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            var productionRun = stageRuns[WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1];
            var package = new WeddingPlannerCreativePackageVersion
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                VersionNumber = nextVersion + 1,
                SchemaVersion = WeddingPlannerSchemaVersions.CreativePackageV1,
                DocumentJson = packageCanonical.DocumentJson,
                Summary = packageCanonical.Summary,
                ProducingCreativeProductionJobId = job.Id,
                ProducingAgentRunId = productionRun.Id,
                ApprovedConceptPackageVersionId = conceptPackage.Id,
                SelectedConceptId = selectedConceptId,
                ApprovedBrandDnaVersionId = brandDna.Id,
                ApprovedBrandDnaVersionNumber = conceptPackage.ApprovedBrandDnaVersionNumber,
                ApprovedColorProfileVersionId = color.Id,
                ApprovedColorProfileVersionNumber = conceptPackage.ApprovedColorProfileVersionNumber,
                ApprovedResearchReportVersionId = research.Id,
                ApprovedResearchReportVersionNumber = conceptPackage.ApprovedResearchReportVersionNumber,
                JobKind = brief.JobKind,
                ParentCreativePackageVersionId = brief.RevisionParentCreativePackageVersionId,
                Status = WeddingPlannerCreativePackageStatuses.Proposed,
                SourceSystem = source,
                IdempotencyKey = WeddingPlannerCreativeDepartmentIdempotency.PackageKey(key),
                ActorType = actorType,
                ActorLabel = actorLabel,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerCreativePackageVersions.Add(package);

            foreach (var asset in assetEntities)
            {
                asset.CreativePackageVersionId = package.Id;
                _db.WeddingPlannerCreativeAssets.Add(asset);
            }

            var nowComplete = DateTime.UtcNow;
            foreach (var contribution in packageCanonical.RoleContributions)
            {
                var producingRun = ResolveProducingRun(contribution.LogicalRole, stageRuns);
                _db.WeddingPlannerCreativeRoleContributions.Add(new WeddingPlannerCreativeRoleContribution
                {
                    Id = Guid.NewGuid(),
                    AdvertiserId = workspace.AdvertiserId,
                    WorkspaceId = workspace.Id,
                    CreativePackageVersionId = package.Id,
                    CreativeProductionJobId = job.Id,
                    LogicalRole = contribution.LogicalRole,
                    ProducingAgentRunId = producingRun.Id,
                    ContributionJson = WeddingPlannerCreativeDepartmentValidation.SerializeContributionJson(contribution),
                    CreatedAt = nowComplete
                });
            }

            productionRun.OutputCreativePackageVersionId = package.Id;
            job.OutputCreativePackageVersionId = package.Id;
            job.Status = WeddingPlannerCreativeProductionJobStatuses.Succeeded;
            job.CompletedAt = nowComplete;
            job.AssetProviderKey = generated[0].Generated.ProviderKey;
            job.AssetProviderAdapterVersion = generated[0].Generated.AdapterVersion;
            job.AssetProviderRequestId = string.Join(",", generated.Select(g => g.Generated.ProviderRequestId).Take(8));
            job.AssetProviderEstimatedCostUsd = generated.Sum(g => g.Generated.EstimatedCostUsd);
            job.AssetProviderReceiptJson = JsonSerializer.Serialize(
                generated.Select(g => new
                {
                    g.Variant.Id,
                    g.Generated.ProviderRequestId,
                    g.Generated.EstimatedCostUsd,
                    g.Validated.Sha256
                }),
                JsonOptions);

            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.CreativePackageProposed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Proposed,
                requestId,
                $"Creative package {package.Id} proposed.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.CreativeProductionJobSucceeded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"Creative production job {job.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(job, false);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException
                                       and not WeddingPlannerForbiddenException
                                       and not WeddingPlannerProviderException
                                       and not WeddingPlannerCreativeAssetJobProviderException)
        {
            // SaveChanges may fail after package/assets/contributions and success
            // audits are already tracked. Detach them so FailJobAsync can persist a
            // clean FAILED outcome without partial package rows (InMemory-safe; no tx).
            var productionRun = stageRuns.GetValueOrDefault(
                WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1);
            AbandonPendingFinalPackagePersistence(job, productionRun);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<WeddingPlannerCreativeProductionJobResult>> ListCreativeProductionJobsAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var jobs = await _db.WeddingPlannerCreativeProductionJobs
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return jobs.Select(x => ToJobResult(x, false)).ToList();
    }

    public async Task<WeddingPlannerCreativeProductionJobResult> GetCreativeProductionJobAsync(
        Guid creativeProductionJobId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.WeddingPlannerCreativeProductionJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == creativeProductionJobId, cancellationToken);
        if (job is null)
        {
            throw new WeddingPlannerNotFoundException("Creative production job was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(job.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative production job");
        return ToJobResult(job, false);
    }

    public async Task<WeddingPlannerCreativePackageListResult> ListCreativePackagesAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var versions = await _db.WeddingPlannerCreativePackageVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

        var results = new List<WeddingPlannerCreativePackageVersionResult>(versions.Count);
        foreach (var version in versions)
        {
            results.Add(await ToPackageResultAsync(
                version,
                workspace.CurrentApprovedCreativePackageVersionId == version.Id,
                false,
                cancellationToken));
        }

        return new WeddingPlannerCreativePackageListResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentApprovedCreativePackageVersionId,
            results);
    }

    public async Task<WeddingPlannerCreativePackageVersionResult> GetCreativePackageAsync(
        Guid creativePackageVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerCreativePackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == creativePackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Creative package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative package");
        var workspace = await _db.WeddingPlannerWorkspaces.AsNoTracking()
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);
        return await ToPackageResultAsync(
            version,
            workspace.CurrentApprovedCreativePackageVersionId == version.Id,
            false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WeddingPlannerCreativeRoleContributionResult>> ListContributionsAsync(
        Guid creativePackageVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerCreativePackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == creativePackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Creative package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative package");
        var rows = await _db.WeddingPlannerCreativeRoleContributions
            .AsNoTracking()
            .Where(x => x.CreativePackageVersionId == version.Id)
            .ToListAsync(cancellationToken);

        return WeddingPlannerCreativeDepartmentLogicalRoles.AllInOrder
            .Select(role => rows.Single(x => x.LogicalRole == role))
            .Select(x => new WeddingPlannerCreativeRoleContributionResult(
                x.Id,
                x.AdvertiserId,
                x.WorkspaceId,
                x.CreativePackageVersionId,
                x.CreativeProductionJobId,
                x.LogicalRole,
                x.ProducingAgentRunId,
                x.ContributionJson,
                x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerCreativeAssetResult>> ListAssetsAsync(
        Guid creativePackageVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerCreativePackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == creativePackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Creative package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative package");
        var rows = await _db.WeddingPlannerCreativeAssets
            .AsNoTracking()
            .Where(x => x.CreativePackageVersionId == version.Id)
            .OrderBy(x => x.VariantId)
            .ToListAsync(cancellationToken);
        return rows.Select(ToAssetResult).ToList();
    }

    public async Task<WeddingPlannerCreativeAssetResult> GetAssetAsync(
        Guid creativeAssetId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.WeddingPlannerCreativeAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == creativeAssetId, cancellationToken);
        if (asset is null)
        {
            throw new WeddingPlannerNotFoundException("Creative asset was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(asset.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative asset");
        return ToAssetResult(asset);
    }

    public async Task<WeddingPlannerCreativeAssetContentResult> GetAssetContentAsync(
        Guid creativeAssetId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.WeddingPlannerCreativeAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == creativeAssetId, cancellationToken);
        if (asset is null)
        {
            throw new WeddingPlannerNotFoundException("Creative asset was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(asset.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative asset");
        _planner.AddAuditForOrchestration(
            asset.AdvertiserId,
            asset.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.CreativeAssetContentRead,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Succeeded,
            requestId,
            Truncate($"Creative asset {asset.Id} content read.", 2000));
        await _db.SaveChangesAsync(cancellationToken);

        return new WeddingPlannerCreativeAssetContentResult(
            asset.Id,
            asset.Bytes,
            asset.ContentType,
            asset.Sha256,
            $"creative-asset-{asset.Id:D}.png");
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListPackageAgentRunsAsync(
        Guid creativePackageVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerCreativePackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == creativePackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Creative package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative package");
        var job = await _db.WeddingPlannerCreativeProductionJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingCreativeProductionJobId, cancellationToken);

        var runIds = new[]
            {
                job.CreativeDirectionAgentRunId,
                job.StrategyAdaptationAgentRunId,
                job.VisualSystemAgentRunId,
                job.ImageDirectionAgentRunId,
                job.CopySystemAgentRunId,
                job.VariantProductionAgentRunId
            }
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();

        var runs = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => runIds.Contains(x.Id))
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);

        return runs.Select(x => ToAgentRunResult(x, false)).ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListWorkspaceAgentRunsAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var runs = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return runs.Select(x => ToAgentRunResult(x, false)).ToList();
    }

    public async Task<WeddingPlannerCreativePackageDecisionResult> DecideAsync(
        Guid creativePackageVersionId,
        string decision,
        string rationale,
        string? selectedVariantId,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var normalizedDecision = Required(decision, nameof(decision), 32).ToUpperInvariant();
        if (normalizedDecision is not (WeddingPlannerCreativePackageDecisions.Approve or WeddingPlannerCreativePackageDecisions.Reject))
        {
            throw new InvalidOperationException("Decision must be APPROVE or REJECT.");
        }

        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var reason = Required(rationale, nameof(rationale), 2000);

        var existing = await _db.WeddingPlannerCreativePackageDecisions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.CreativePackageVersionId != creativePackageVersionId)
            {
                throw new WeddingPlannerNotFoundException("Creative package decision was not found.");
            }

            _planner.EnsureAdvertiserAccessForOrchestration(
                existing.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative package decision");
            var existingVersion = await _db.WeddingPlannerCreativePackageVersions
                .SingleAsync(x => x.Id == existing.CreativePackageVersionId, cancellationToken);
            var workspace = await _db.WeddingPlannerWorkspaces
                .SingleAsync(x => x.Id == existing.WorkspaceId, cancellationToken);
            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.CreativePackageReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Creative package decision {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return new WeddingPlannerCreativePackageDecisionResult(
                existing.Id,
                existing.CreativePackageVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Decision,
                existing.SelectedVariantId,
                existing.ActorType,
                existing.ActorLabel,
                existing.Rationale,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                await ToPackageResultAsync(
                    existingVersion,
                    workspace.CurrentApprovedCreativePackageVersionId == existingVersion.Id,
                    true,
                    cancellationToken),
                true);
        }

        var version = await _db.WeddingPlannerCreativePackageVersions
            .SingleOrDefaultAsync(x => x.Id == creativePackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Creative package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "creative package");
        var workspaceEntity = await _db.WeddingPlannerWorkspaces
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);

        if (!string.Equals(version.Status, WeddingPlannerCreativePackageStatuses.Proposed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only PROPOSED creative packages accept a first decision.");
        }

        string? normalizedSelection = null;
        if (normalizedDecision == WeddingPlannerCreativePackageDecisions.Approve)
        {
            normalizedSelection = Required(selectedVariantId, nameof(selectedVariantId), 32);
            var packageVariantIds = WeddingPlannerCreativeDepartmentValidation.ExtractVariantIdsFromPackage(version.DocumentJson);
            if (!packageVariantIds.Contains(normalizedSelection))
            {
                throw new InvalidOperationException(
                    "APPROVE requires selectedVariantId present in the package (variant_1..variant_N).");
            }
        }
        else if (!string.IsNullOrWhiteSpace(selectedVariantId))
        {
            throw new InvalidOperationException("REJECT forbids selectedVariantId.");
        }

        var documentBefore = version.DocumentJson;
        var row = new WeddingPlannerCreativePackageDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = version.AdvertiserId,
            WorkspaceId = version.WorkspaceId,
            CreativePackageVersionId = version.Id,
            Decision = normalizedDecision,
            SelectedVariantId = normalizedSelection,
            ActorType = actorType,
            ActorLabel = actorLabel,
            Rationale = reason,
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = DateTime.UtcNow
        };
        _db.WeddingPlannerCreativePackageDecisions.Add(row);

        if (normalizedDecision == WeddingPlannerCreativePackageDecisions.Approve)
        {
            if (workspaceEntity.CurrentApprovedCreativePackageVersionId is Guid previousId
                && previousId != version.Id)
            {
                var previous = await _db.WeddingPlannerCreativePackageVersions
                    .SingleOrDefaultAsync(x => x.Id == previousId, cancellationToken);
                if (previous is not null
                    && string.Equals(previous.Status, WeddingPlannerCreativePackageStatuses.Approved, StringComparison.Ordinal))
                {
                    previous.Status = WeddingPlannerCreativePackageStatuses.Superseded;
                    _planner.AddAuditForOrchestration(
                        previous.AdvertiserId,
                        previous.WorkspaceId,
                        null,
                        null,
                        WeddingPlannerAuditActions.CreativePackageSuperseded,
                        actorType,
                        actorLabel,
                        WeddingPlannerOutcomes.Superseded,
                        requestId,
                        $"Creative package {previous.Id} superseded.");
                }
            }

            version.Status = WeddingPlannerCreativePackageStatuses.Approved;
            workspaceEntity.CurrentApprovedCreativePackageVersionId = version.Id;
            workspaceEntity.UpdatedAt = DateTime.UtcNow;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.CreativePackageApproved,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                $"Creative package {version.Id} approved with selectedVariantId {normalizedSelection}.");
        }
        else
        {
            version.Status = WeddingPlannerCreativePackageStatuses.Rejected;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.CreativePackageRejected,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                $"Creative package {version.Id} rejected.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.Equals(version.DocumentJson, documentBefore, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Creative package DocumentJson must never mutate on decision.");
        }

        return new WeddingPlannerCreativePackageDecisionResult(
            row.Id,
            row.CreativePackageVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Decision,
            row.SelectedVariantId,
            row.ActorType,
            row.ActorLabel,
            row.Rationale,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            await ToPackageResultAsync(
                version,
                workspaceEntity.CurrentApprovedCreativePackageVersionId == version.Id,
                false,
                cancellationToken),
            false);
    }

    private async Task ValidateRevisionParentAsync(
        WeddingPlannerWorkspace workspace,
        CanonicalCreativeProductionBrief brief,
        CancellationToken cancellationToken)
    {
        var parent = await _db.WeddingPlannerCreativePackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == brief.RevisionParentCreativePackageVersionId, cancellationToken);
        if (parent is null || parent.WorkspaceId != workspace.Id || parent.AdvertiserId != workspace.AdvertiserId)
        {
            throw new InvalidOperationException("REVISION parent creative package must exist in the same workspace.");
        }

        if (parent.ApprovedConceptPackageVersionId != brief.ApprovedConceptPackageVersionId
            || !string.Equals(parent.SelectedConceptId, brief.SelectedConceptId, StringComparison.Ordinal)
            || parent.ApprovedBrandDnaVersionId != brief.ApprovedBrandDnaVersionId
            || parent.ApprovedColorProfileVersionId != brief.ApprovedColorProfileVersionId
            || parent.ApprovedResearchReportVersionId != brief.ApprovedResearchReportVersionId
            || parent.ApprovedBrandDnaVersionNumber != brief.ApprovedBrandDnaVersionNumber
            || parent.ApprovedColorProfileVersionNumber != brief.ApprovedColorProfileVersionNumber
            || parent.ApprovedResearchReportVersionNumber != brief.ApprovedResearchReportVersionNumber)
        {
            throw new InvalidOperationException(
                "REVISION parent must share exact concept package, selectedConceptId, and DNA/color/research pins.");
        }

        if (workspace.CurrentApprovedConceptPackageVersionId != brief.ApprovedConceptPackageVersionId)
        {
            throw new InvalidOperationException(
                "Current-approved concept package must still match the revision pins at job start.");
        }
    }

    private async Task<List<(CanonicalCreativePlannedVariant Variant, WeddingPlannerCreativeAssetGenerationResult Generated, WeddingPlannerValidatedPng Validated)>> GenerateAndValidateAssetsAsync(
        WeddingPlannerCreativeProductionJob job,
        CanonicalCreativeProductionBrief brief,
        CanonicalCreativePackagePlan plan,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        _planner.AddAuditForOrchestration(
            job.AdvertiserId,
            job.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.CreativeAssetProviderAttempted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"Creative asset provider attempted for job {job.Id} ({plan.Variants.Count} variants).");
        await _db.SaveChangesAsync(cancellationToken);

        var results = new List<(CanonicalCreativePlannedVariant, WeddingPlannerCreativeAssetGenerationResult, WeddingPlannerValidatedPng)>(plan.Variants.Count);
        foreach (var variant in plan.Variants)
        {
            var renderSpec = WeddingPlannerCreativeDepartmentValidation.BuildRenderSpecJson(variant, brief);
            var generated = await _assets.GeneratePngAsync(
                new WeddingPlannerCreativeAssetGenerationRequest(
                    variant.Id,
                    variant.Format,
                    variant.Width,
                    variant.Height,
                    renderSpec,
                    job.Id,
                    brief.ApprovedConceptPackageVersionId,
                    brief.SelectedConceptId),
                cancellationToken);

            var validated = WeddingPlannerPngValidator.ValidateExactCanvas(
                generated.PngBytes, variant.Width, variant.Height);
            results.Add((variant, generated, validated));
        }

        if (results.Count > WeddingPlannerPngValidator.MaxAssetsPerPackage)
        {
            throw new InvalidOperationException(
                $"Creative package cannot exceed {WeddingPlannerPngValidator.MaxAssetsPerPackage} assets.");
        }

        _planner.AddAuditForOrchestration(
            job.AdvertiserId,
            job.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.CreativeAssetProviderSucceeded,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Succeeded,
            requestId,
            $"Creative asset provider succeeded for job {job.Id}.");
        await _db.SaveChangesAsync(cancellationToken);
        return results;
    }

    private async Task<WeddingPlannerAgentRun> ExecuteStageAsync(
        WeddingPlannerCreativeProductionJob job,
        WeddingPlannerWorkspace workspace,
        WeddingPlannerBrandDnaVersion brandDna,
        WeddingPlannerColorProfileVersion color,
        WeddingPlannerResearchReportVersion research,
        WeddingPlannerConceptPackageVersion conceptPackage,
        SelectedConceptSnapshot selectedConcept,
        CanonicalCreativeProductionBrief brief,
        List<CanonicalCreativeStageOutput> priorStages,
        string workerProfileVersion,
        IReadOnlySet<string> knownPaletteRoles,
        IReadOnlySet<string> forbiddenSourceIds,
        bool requireSynthetic,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var stageRole = WeddingPlannerCreativeDepartmentWorkerProfiles.StageLogicalRole(workerProfileVersion);
        var promptPack = WeddingPlannerCreativeDepartmentWorkerProfiles.PromptPack(workerProfileVersion);
        var assignedRoles = WeddingPlannerCreativeDepartmentWorkerProfiles.AssignedRoles(workerProfileVersion);
        var stageKey = WeddingPlannerCreativeDepartmentIdempotency.StageKey(job.IdempotencyKey, workerProfileVersion);

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
            AssignedRolesJson = WeddingPlannerCreativeDepartmentValidation.SerializeAssignedRolesJson(assignedRoles),
            RequestId = requestId,
            SourceSystem = job.SourceSystem,
            IdempotencyKey = stageKey,
            Status = WeddingPlannerAgentRunStatuses.Running,
            StartedAt = DateTime.UtcNow
        };
        _db.WeddingPlannerAgentRuns.Add(run);
        AssignJobRunPointer(job, workerProfileVersion, run.Id);
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
            $"Creative production run {run.Id} ({workerProfileVersion}) started.");
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var messages = BuildStageMessages(
                job, brandDna, color, research, conceptPackage, selectedConcept, brief, priorStages, workerProfileVersion, knownPaletteRoles);
            var completion = await _ai.CompleteAsync(
                new WeddingPlannerAiCompletionRequest(
                    stageRole,
                    promptPack,
                    messages,
                    WeddingPlannerResponseFormats.Json,
                    4096,
                    workerProfileVersion,
                    assignedRoles),
                cancellationToken);

            var canonical = CanonicalizeStage(
                completion.Content,
                workerProfileVersion,
                brief,
                selectedConcept,
                priorStages,
                knownPaletteRoles,
                forbiddenSourceIds,
                requireSynthetic);

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
            StoreStageOutput(job, workerProfileVersion, canonical.CanonicalJson);
            priorStages.Add(canonical);

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
                $"Creative production run {run.Id} ({workerProfileVersion}) succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailRunAsync(run, workspace.AdvertiserId, workspace.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw new WeddingPlannerProviderException(run.Id, "Wedding Planner Creative Production AI provider failed.", ex);
        }
    }

    private static CanonicalCreativeStageOutput CanonicalizeStage(
        string content,
        string workerProfileVersion,
        CanonicalCreativeProductionBrief brief,
        SelectedConceptSnapshot selectedConcept,
        IReadOnlyList<CanonicalCreativeStageOutput> priorStages,
        IReadOnlySet<string> knownPaletteRoles,
        IReadOnlySet<string> forbiddenSourceIds,
        bool requireSynthetic) =>
        workerProfileVersion switch
        {
            WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1 =>
                WeddingPlannerCreativeDepartmentValidation.CanonicalizeCreativeDirectionOutput(
                    content, brief.SelectedConceptId, requireSynthetic),
            WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1 =>
                WeddingPlannerCreativeDepartmentValidation.CanonicalizeStrategyAdaptationOutput(
                    content, brief.SelectedConceptId, brief.Formats, requireSynthetic),
            WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1 =>
                WeddingPlannerCreativeDepartmentValidation.CanonicalizeVisualSystemOutput(
                    content, brief.SelectedConceptId, knownPaletteRoles, requireSynthetic),
            WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1 =>
                WeddingPlannerCreativeDepartmentValidation.CanonicalizeImageDirectionOutput(
                    content, brief.SelectedConceptId, brief.RequestedVariantCount, requireSynthetic),
            WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1 =>
                WeddingPlannerCreativeDepartmentValidation.CanonicalizeCopySystemOutput(
                    content,
                    brief.SelectedConceptId,
                    brief.RequestedVariantCount,
                    selectedConcept.FactualClaims,
                    forbiddenSourceIds,
                    requireSynthetic),
            WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1 =>
                WeddingPlannerCreativeDepartmentValidation.CanonicalizeVariantProductionOutput(
                    content,
                    brief.SelectedConceptId,
                    brief,
                    priorStages.Single(s => s.WorkerProfileVersion == WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1),
                    priorStages.Single(s => s.WorkerProfileVersion == WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1),
                    knownPaletteRoles,
                    requireSynthetic),
            _ => throw new InvalidOperationException($"Unknown creative department profile '{workerProfileVersion}'.")
        };

    private static IReadOnlyList<WeddingPlannerAiMessage> BuildStageMessages(
        WeddingPlannerCreativeProductionJob job,
        WeddingPlannerBrandDnaVersion brandDna,
        WeddingPlannerColorProfileVersion color,
        WeddingPlannerResearchReportVersion research,
        WeddingPlannerConceptPackageVersion conceptPackage,
        SelectedConceptSnapshot selectedConcept,
        CanonicalCreativeProductionBrief brief,
        IReadOnlyList<CanonicalCreativeStageOutput> priorStages,
        string workerProfileVersion,
        IReadOnlySet<string> knownPaletteRoles)
    {
        var messages = new List<WeddingPlannerAiMessage>
        {
            new(WeddingPlannerActorTypes.System,
                JsonSerializer.Serialize(new
                {
                    brief = new
                    {
                        jobKind = brief.JobKind,
                        objective = brief.Objective,
                        formats = brief.Formats,
                        requestedVariantCount = brief.RequestedVariantCount,
                        revisionNotes = brief.RevisionNotes
                    },
                    selectedConceptId = brief.SelectedConceptId,
                    selectedConceptSnapshot = new
                    {
                        selectedConcept.Id,
                        selectedConcept.Name,
                        selectedConcept.Rationale,
                        selectedConcept.VisualDirection,
                        selectedConcept.PaletteRoleRefs,
                        copy = selectedConcept.Copy,
                        factualClaims = selectedConcept.FactualClaims
                    },
                    approvedConceptPackageVersionId = conceptPackage.Id,
                    brandDnaConstraint = new
                    {
                        approvedBrandDnaVersionId = brandDna.Id,
                        summary = brandDna.Summary
                    },
                    colorProfileConstraint = new
                    {
                        approvedColorProfileVersionId = color.Id,
                        summary = color.Summary,
                        paletteRoles = knownPaletteRoles.OrderBy(x => x, StringComparer.Ordinal).ToArray()
                    },
                    researchEvidenceBoundary = new
                    {
                        approvedResearchReportVersionId = research.Id,
                        summary = research.Summary
                    },
                    phase5ProvenanceNote = "Phase 5 concept contributions are pinned provenance only and must not be re-authored.",
                    workerProfileVersion,
                    note = "Brand DNA and Color Profile are creative constraints only, never factual sourceIds. AI stages emit prompts/specs only — never image bytes."
                }, JsonOptions))
        };

        foreach (var prior in priorStages)
        {
            messages.Add(new WeddingPlannerAiMessage(WeddingPlannerActorTypes.Planner, prior.CanonicalJson));
        }

        return messages;
    }

    private void EnsureAiProviderAllowed()
    {
        var isLocal = IsLocalAiProvider();
        if (_aiOptions.RequireRemoteAiProvider && isLocal)
        {
            throw new InvalidOperationException(
                "WeddingPlannerAi:Provider must be OpenAiCompatible when RequireRemoteAiProvider is enabled. "
                + "The Local provider is a deterministic development/test worker, not a production AI provider.");
        }
    }

    private void EnsureAssetProviderAllowed()
    {
        var isLocal = IsLocalAssetProvider();
        if (_assetOptions.RequireRemoteCreativeAssetProvider && isLocal)
        {
            throw new InvalidOperationException(
                "WeddingPlannerCreativeAsset:Provider must be RemoteHttp when RequireRemoteCreativeAssetProvider is enabled. "
                + "The Local provider is a deterministic development/test worker, not a production creative asset provider.");
        }
    }

    private bool IsLocalAiProvider() =>
        string.Equals(_aiOptions.Provider, WeddingPlannerAiProviderKinds.Local, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(_aiOptions.Provider);

    private bool IsLocalAssetProvider() =>
        string.Equals(_assetOptions.Provider, WeddingPlannerCreativeAssetProviderKinds.Local, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(_assetOptions.Provider);

    private void AbandonPendingFinalPackagePersistence(
        WeddingPlannerCreativeProductionJob job,
        WeddingPlannerAgentRun? productionRun)
    {
        job.OutputCreativePackageVersionId = null;
        job.OutputCreativePackageVersion = null;
        if (productionRun is not null)
        {
            productionRun.OutputCreativePackageVersionId = null;
            productionRun.OutputCreativePackageVersion = null;
        }

        // Dependents first, then package principal — avoids EF association errors when
        // clearing a half-built package graph after a failed SaveChanges.
        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerCreativeAsset>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.Entity.CreativePackageVersion = null!;
            entry.State = EntityState.Detached;
        }

        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerCreativeRoleContribution>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.Entity.CreativePackageVersion = null!;
            entry.State = EntityState.Detached;
        }

        DetachAddedEntities<WeddingPlannerCreativePackageVersion>();
        DetachAddedAudits(
            WeddingPlannerAuditActions.CreativePackageProposed,
            WeddingPlannerAuditActions.CreativeProductionJobSucceeded);
    }

    private void DetachAddedEntities<TEntity>()
        where TEntity : class
    {
        foreach (var entry in _db.ChangeTracker.Entries<TEntity>()
            .Where(e => e.State == EntityState.Added)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private void DetachAddedAudits(params string[] actions)
    {
        if (actions.Length == 0)
        {
            return;
        }

        var actionSet = new HashSet<string>(actions, StringComparer.Ordinal);
        foreach (var entry in _db.ChangeTracker.Entries<WeddingPlannerAuditEvent>()
            .Where(e => e.State == EntityState.Added && actionSet.Contains(e.Entity.Action))
            .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task FailJobAsync(
        WeddingPlannerCreativeProductionJob job,
        string actorType,
        string actorLabel,
        string? requestId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        job.Status = WeddingPlannerCreativeProductionJobStatuses.Failed;
        job.OutputCreativePackageVersionId = null;
        job.ErrorCode = ex switch
        {
            WeddingPlannerAiProviderException aiEx => Truncate(aiEx.ErrorCode, 64),
            WeddingPlannerCreativeAssetProviderException assetEx => Truncate(assetEx.ErrorCode, 64),
            _ => Truncate(ex.GetType().Name, 64)
        };
        job.ErrorMessage = Truncate(ex.Message, 2000);
        job.CompletedAt = DateTime.UtcNow;
        _planner.AddAuditForOrchestration(
            job.AdvertiserId,
            job.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.CreativeProductionJobFailed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Failed,
            requestId,
            $"Creative production job {job.Id} failed: {job.ErrorCode}");
        if (ex is WeddingPlannerCreativeAssetProviderException)
        {
            _planner.AddAuditForOrchestration(
                job.AdvertiserId,
                job.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.CreativeAssetProviderFailed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Failed,
                requestId,
                $"Creative asset provider failed for job {job.Id}.");
        }

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
        run.ErrorCode = ex is WeddingPlannerAiProviderException providerEx
            ? Truncate(providerEx.ErrorCode, 64)
            : Truncate(ex.GetType().Name, 64);
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
            $"Agent run {run.Id} failed: {run.ErrorCode}");
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void AssignJobRunPointer(WeddingPlannerCreativeProductionJob job, string profile, Guid runId)
    {
        switch (profile)
        {
            case WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1:
                job.CreativeDirectionAgentRunId = runId;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1:
                job.StrategyAdaptationAgentRunId = runId;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1:
                job.VisualSystemAgentRunId = runId;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1:
                job.ImageDirectionAgentRunId = runId;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1:
                job.CopySystemAgentRunId = runId;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1:
                job.VariantProductionAgentRunId = runId;
                break;
        }
    }

    private static void StoreStageOutput(WeddingPlannerCreativeProductionJob job, string profile, string json)
    {
        switch (profile)
        {
            case WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1:
                job.CreativeDirectionStageOutputJson = json;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1:
                job.StrategyAdaptationStageOutputJson = json;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1:
                job.VisualSystemStageOutputJson = json;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1:
                job.ImageDirectionStageOutputJson = json;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1:
                job.CopySystemStageOutputJson = json;
                break;
            case WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1:
                job.VariantProductionStageOutputJson = json;
                break;
        }
    }

    private static WeddingPlannerAgentRun ResolveProducingRun(
        string logicalRole,
        IReadOnlyDictionary<string, WeddingPlannerAgentRun> stageRuns) =>
        logicalRole switch
        {
            WeddingPlannerCreativeDepartmentLogicalRoles.CreativeDirector
                or WeddingPlannerCreativeDepartmentLogicalRoles.CampaignStrategist =>
                stageRuns[WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1],
            WeddingPlannerCreativeDepartmentLogicalRoles.AudienceStrategist
                or WeddingPlannerCreativeDepartmentLogicalRoles.OfferStrategist
                or WeddingPlannerCreativeDepartmentLogicalRoles.ChannelStrategist =>
                stageRuns[WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1],
            WeddingPlannerCreativeDepartmentLogicalRoles.VisualDesigner
                or WeddingPlannerCreativeDepartmentLogicalRoles.LayoutDesigner
                or WeddingPlannerCreativeDepartmentLogicalRoles.TypographyDesigner =>
                stageRuns[WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1],
            WeddingPlannerCreativeDepartmentLogicalRoles.ImagePromptDesigner =>
                stageRuns[WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1],
            WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist
                or WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist
                or WeddingPlannerCreativeDepartmentLogicalRoles.CtaSpecialist =>
                stageRuns[WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1],
            WeddingPlannerCreativeDepartmentLogicalRoles.VariantProducer =>
                stageRuns[WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1],
            _ => throw new InvalidOperationException($"Unknown creative role '{logicalRole}'.")
        };

    private async Task<WeddingPlannerCreativePackageVersionResult> ToPackageResultAsync(
        WeddingPlannerCreativePackageVersion version,
        bool isCurrentApproved,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var job = await _db.WeddingPlannerCreativeProductionJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingCreativeProductionJobId, cancellationToken);
        var runIds = new[]
            {
                job.CreativeDirectionAgentRunId,
                job.StrategyAdaptationAgentRunId,
                job.VisualSystemAgentRunId,
                job.ImageDirectionAgentRunId,
                job.CopySystemAgentRunId,
                job.VariantProductionAgentRunId
            }
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();
        var aiCost = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => runIds.Contains(x.Id))
            .SumAsync(x => x.EstimatedCostUsd ?? 0m, cancellationToken);

        return new WeddingPlannerCreativePackageVersionResult(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.DocumentJson,
            version.Summary,
            version.ProducingCreativeProductionJobId,
            version.ProducingAgentRunId,
            version.ApprovedConceptPackageVersionId,
            version.SelectedConceptId,
            version.ApprovedBrandDnaVersionId,
            version.ApprovedBrandDnaVersionNumber,
            version.ApprovedColorProfileVersionId,
            version.ApprovedColorProfileVersionNumber,
            version.ApprovedResearchReportVersionId,
            version.ApprovedResearchReportVersionNumber,
            version.JobKind,
            version.ParentCreativePackageVersionId,
            version.Status,
            version.SourceSystem,
            version.IdempotencyKey,
            version.ActorType,
            version.ActorLabel,
            version.CreatedAt,
            isCurrentApproved,
            aiCost + (job.AssetProviderEstimatedCostUsd ?? 0m),
            job.AssetProviderEstimatedCostUsd,
            isReplay);
    }

    private static WeddingPlannerCreativeProductionJobResult ToJobResult(WeddingPlannerCreativeProductionJob job, bool isReplay) =>
        new(
            job.Id,
            job.AdvertiserId,
            job.WorkspaceId,
            job.JobKind,
            job.Objective,
            JsonSerializer.Deserialize<string[]>(job.FormatsJson) ?? Array.Empty<string>(),
            job.RequestedVariantCount,
            job.RevisionParentCreativePackageVersionId,
            job.RevisionNotes,
            job.InputJson,
            job.InputSha256,
            job.ApprovedConceptPackageVersionId,
            job.SelectedConceptId,
            job.ApprovedBrandDnaVersionId,
            job.ApprovedBrandDnaVersionNumber,
            job.ApprovedColorProfileVersionId,
            job.ApprovedColorProfileVersionNumber,
            job.ApprovedResearchReportVersionId,
            job.ApprovedResearchReportVersionNumber,
            job.CreativeDirectionAgentRunId,
            job.StrategyAdaptationAgentRunId,
            job.VisualSystemAgentRunId,
            job.ImageDirectionAgentRunId,
            job.CopySystemAgentRunId,
            job.VariantProductionAgentRunId,
            job.AssetProviderKey,
            job.AssetProviderAdapterVersion,
            job.AssetProviderRequestId,
            job.AssetProviderEstimatedCostUsd,
            job.OutputCreativePackageVersionId,
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

    private static WeddingPlannerCreativeAssetResult ToAssetResult(WeddingPlannerCreativeAsset asset) =>
        new(
            asset.Id,
            asset.AdvertiserId,
            asset.WorkspaceId,
            asset.CreativePackageVersionId,
            asset.CreativeProductionJobId,
            asset.VariantId,
            asset.Format,
            asset.Width,
            asset.Height,
            asset.ContentType,
            asset.ByteSize,
            asset.Sha256,
            asset.ProviderKey,
            asset.AdapterVersion,
            asset.ProviderRequestId,
            asset.EstimatedCostUsd,
            asset.CreatedAt);

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
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} exceeds max length {maxLength}.");
        }

        return trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

/// <summary>
/// Raised after a durable FAILED creative-production job is persisted when asset generation fails
/// after six successful AI runs. Controllers map this to HTTP 502.
/// </summary>
public sealed class WeddingPlannerCreativeAssetJobProviderException : InvalidOperationException
{
    public Guid CreativeProductionJobId { get; }
    public string ErrorCode { get; }

    public WeddingPlannerCreativeAssetJobProviderException(
        Guid creativeProductionJobId,
        string message,
        string errorCode,
        Exception? innerException = null)
        : base(message, innerException)
    {
        CreativeProductionJobId = creativeProductionJobId;
        ErrorCode = errorCode;
    }
}
