using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerWorkshopJobResult(
    Guid WorkshopJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string Objective,
    string CampaignGoal,
    string AudienceFocus,
    string ChannelFormat,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<string> Deliverables,
    string Cta,
    IReadOnlyList<string> Constraints,
    string InputJson,
    string InputSha256,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    Guid? StrategyAgentRunId,
    Guid? CreativeAgentRunId,
    Guid? ProductionAgentRunId,
    Guid? OutputConceptPackageVersionId,
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

public sealed record WeddingPlannerConceptPackageVersionResult(
    Guid ConceptPackageVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingWorkshopJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string ChannelFormat,
    int CanvasWidth,
    int CanvasHeight,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerConceptPackageListResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedConceptPackageVersionId,
    IReadOnlyList<WeddingPlannerConceptPackageVersionResult> Versions);

public sealed record WeddingPlannerConceptRoleContributionResult(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid ConceptPackageVersionId,
    Guid WorkshopJobId,
    string LogicalRole,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerConceptPackageDecisionResult(
    Guid DecisionId,
    Guid ConceptPackageVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string? SelectedConceptId,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerConceptPackageVersionResult Version,
    bool IsReplay);

/// <summary>
/// Concept Workshop saga: exactly three AI stage profiles + merged concept package.
/// Does not rely on EF InMemory transactions. Package/contributions are created only after all
/// three stage outputs validate. Never generates images or fetches creative URLs.
/// </summary>
public sealed class WeddingPlannerConceptWorkshopOrchestrationService
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

    public WeddingPlannerConceptWorkshopOrchestrationService(
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

    public async Task<WeddingPlannerWorkshopJobResult> CreateWorkshopJobAsync(
        Guid workspaceId,
        string objective,
        string campaignGoal,
        string audienceFocus,
        string channelFormat,
        IReadOnlyList<string> deliverables,
        string cta,
        IReadOnlyList<string>? constraints,
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

        if (rawBriefNode is not null)
        {
            WeddingPlannerConceptWorkshopValidation.RejectForbiddenBriefFields(rawBriefNode);
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), WeddingPlannerConceptWorkshopIdempotency.MaxJobIdempotencyKeyLength);

        var existing = await _db.WeddingPlannerWorkshopJobs
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkspaceId != workspace.Id || existing.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Workshop job was not found.");
            }

            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.WorkshopJobReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Workshop job {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(existing, true);
        }

        if (workspace.CurrentApprovedBrandDnaVersionId is null
            || workspace.CurrentApprovedColorProfileVersionId is null
            || workspace.CurrentApprovedResearchReportVersionId is null)
        {
            throw new InvalidOperationException(
                "Current-approved Brand DNA, Color Profile, and Research Report versions are all required before starting a workshop job.");
        }

        var brandDna = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedBrandDnaVersionId.Value, cancellationToken);
        if (brandDna is null
            || brandDna.WorkspaceId != workspace.Id
            || !string.Equals(
                brandDna.Status,
                WeddingPlannerBrandDnaStatuses.Approved,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A current-approved Brand DNA version is required before starting a workshop job.");
        }

        var color = await _db.WeddingPlannerColorProfileVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedColorProfileVersionId.Value, cancellationToken);
        if (color is null
            || color.WorkspaceId != workspace.Id
            || !string.Equals(
                color.Status,
                WeddingPlannerColorProfileStatuses.Approved,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A current-approved Color Profile version is required before starting a workshop job.");
        }

        var research = await _db.WeddingPlannerResearchReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedResearchReportVersionId.Value, cancellationToken);
        if (research is null
            || research.WorkspaceId != workspace.Id
            || !string.Equals(
                research.Status,
                WeddingPlannerResearchReportStatuses.Approved,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A current-approved Research Report version is required before starting a workshop job.");
        }

        var requireSynthetic = IsLocalAiProvider();
        var brief = WeddingPlannerConceptWorkshopValidation.CanonicalizeBrief(
            objective,
            campaignGoal,
            audienceFocus,
            channelFormat,
            deliverables,
            cta,
            constraints,
            brandDna.Id,
            brandDna.VersionNumber,
            color.Id,
            color.VersionNumber,
            research.Id,
            research.VersionNumber,
            clientCanvasWidth,
            clientCanvasHeight,
            string.IsNullOrWhiteSpace(_aiOptions.Provider)
                ? WeddingPlannerAiProviderKinds.Local
                : _aiOptions.Provider.Trim(),
            _ai.WorkerKey);

        var knownPaletteRoles = WeddingPlannerConceptWorkshopValidation.ExtractPaletteRoleNames(color.DocumentJson);
        var knownSourceIds = WeddingPlannerConceptWorkshopValidation.ExtractResearchSourceIds(research.DocumentJson);
        var forbiddenSourceIds = new HashSet<string>(StringComparer.Ordinal)
        {
            brandDna.Id.ToString("D"),
            color.Id.ToString("D"),
            research.Id.ToString("D")
        };

        var now = DateTime.UtcNow;
        var job = new WeddingPlannerWorkshopJob
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            Objective = brief.Objective,
            CampaignGoal = brief.CampaignGoal,
            AudienceFocus = brief.AudienceFocus,
            ChannelFormat = brief.ChannelFormat,
            CanvasWidth = brief.CanvasWidth,
            CanvasHeight = brief.CanvasHeight,
            DeliverablesJson = JsonSerializer.Serialize(brief.Deliverables, JsonOptions),
            Cta = brief.Cta,
            ConstraintsJson = JsonSerializer.Serialize(brief.Constraints, JsonOptions),
            InputJson = brief.InputJson,
            InputSha256 = brief.InputSha256,
            ApprovedBrandDnaVersionId = brandDna.Id,
            ApprovedBrandDnaVersionNumber = brandDna.VersionNumber,
            ApprovedColorProfileVersionId = color.Id,
            ApprovedColorProfileVersionNumber = color.VersionNumber,
            ApprovedResearchReportVersionId = research.Id,
            ApprovedResearchReportVersionNumber = research.VersionNumber,
            Status = WeddingPlannerWorkshopJobStatuses.Running,
            SourceSystem = source,
            IdempotencyKey = key,
            ActorType = actorType,
            ActorLabel = actorLabel,
            StartedAt = now
        };
        _db.WeddingPlannerWorkshopJobs.Add(job);
        _planner.AddAuditForOrchestration(
            workspace.AdvertiserId,
            workspace.Id,
            null,
            null,
            WeddingPlannerAuditActions.WorkshopJobStarted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"Workshop job {job.Id} started.");
        await _db.SaveChangesAsync(cancellationToken);

        forbiddenSourceIds.Add(job.Id.ToString("D"));

        var stageOutputs = new List<CanonicalWorkshopStageOutput>(3);
        var stageRuns = new Dictionary<string, WeddingPlannerAgentRun>(StringComparer.Ordinal);
        IReadOnlyList<string> conceptIds = WeddingPlannerConceptIds.All;

        foreach (var profile in WeddingPlannerConceptWorkshopWorkerProfiles.All)
        {
            var run = await ExecuteStageAsync(
                job,
                workspace,
                brandDna,
                color,
                research,
                stageOutputs,
                profile,
                conceptIds,
                knownSourceIds,
                knownPaletteRoles,
                forbiddenSourceIds,
                requireSynthetic,
                actorType,
                actorLabel,
                requestId,
                cancellationToken);
            stageRuns[profile] = run;
            if (profile == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1)
            {
                conceptIds = stageOutputs[^1].ConceptIds;
            }
        }

        try
        {
            var packageCanonical = WeddingPlannerConceptWorkshopValidation.MergeAndCanonicalizePackage(
                brief,
                stageOutputs[0],
                stageOutputs[1],
                stageOutputs[2],
                job.Id,
                requireSynthetic);

            var nextVersion = await _db.WeddingPlannerConceptPackageVersions
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => (int?)x.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            var productionRun = stageRuns[WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1];
            var package = new WeddingPlannerConceptPackageVersion
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                VersionNumber = nextVersion + 1,
                SchemaVersion = WeddingPlannerSchemaVersions.ConceptPackageV1,
                DocumentJson = packageCanonical.DocumentJson,
                Summary = packageCanonical.Summary,
                ProducingWorkshopJobId = job.Id,
                ProducingAgentRunId = productionRun.Id,
                ApprovedBrandDnaVersionId = brandDna.Id,
                ApprovedBrandDnaVersionNumber = brandDna.VersionNumber,
                ApprovedColorProfileVersionId = color.Id,
                ApprovedColorProfileVersionNumber = color.VersionNumber,
                ApprovedResearchReportVersionId = research.Id,
                ApprovedResearchReportVersionNumber = research.VersionNumber,
                ChannelFormat = brief.ChannelFormat,
                CanvasWidth = brief.CanvasWidth,
                CanvasHeight = brief.CanvasHeight,
                Status = WeddingPlannerConceptPackageStatuses.Proposed,
                SourceSystem = source,
                IdempotencyKey = WeddingPlannerConceptWorkshopIdempotency.PackageKey(key),
                ActorType = actorType,
                ActorLabel = actorLabel,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerConceptPackageVersions.Add(package);

            var nowComplete = DateTime.UtcNow;
            foreach (var contribution in packageCanonical.Contributions)
            {
                var producingRun = ResolveProducingRun(contribution.LogicalRole, stageRuns);
                var stageContribution = stageOutputs
                    .SelectMany(s => s.Contributions)
                    .Single(c => c.LogicalRole == contribution.LogicalRole);
                var contributionJson = SerializeContribution(stageContribution);

                _db.WeddingPlannerConceptRoleContributions.Add(new WeddingPlannerConceptRoleContribution
                {
                    Id = Guid.NewGuid(),
                    AdvertiserId = workspace.AdvertiserId,
                    WorkspaceId = workspace.Id,
                    ConceptPackageVersionId = package.Id,
                    WorkshopJobId = job.Id,
                    LogicalRole = contribution.LogicalRole,
                    ProducingAgentRunId = producingRun.Id,
                    ContributionJson = contributionJson,
                    CreatedAt = nowComplete
                });
            }

            productionRun.OutputConceptPackageVersionId = package.Id;
            job.OutputConceptPackageVersionId = package.Id;
            job.Status = WeddingPlannerWorkshopJobStatuses.Succeeded;
            job.CompletedAt = nowComplete;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.ConceptPackageProposed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Proposed,
                requestId,
                $"Concept package {package.Id} proposed.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.WorkshopJobSucceeded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"Workshop job {job.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(job, false);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException
                                       and not WeddingPlannerForbiddenException
                                       and not WeddingPlannerProviderException)
        {
            var productionRun = stageRuns.GetValueOrDefault(WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1);
            if (productionRun is not null
                && string.Equals(productionRun.Status, WeddingPlannerAgentRunStatuses.Succeeded, StringComparison.Ordinal))
            {
                await FailRunAsync(productionRun, workspace.AdvertiserId, workspace.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            }

            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<WeddingPlannerWorkshopJobResult>> ListWorkshopJobsAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var jobs = await _db.WeddingPlannerWorkshopJobs
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return jobs.Select(x => ToJobResult(x, false)).ToList();
    }

    public async Task<WeddingPlannerWorkshopJobResult> GetWorkshopJobAsync(
        Guid workshopJobId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.WeddingPlannerWorkshopJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workshopJobId, cancellationToken);
        if (job is null)
        {
            throw new WeddingPlannerNotFoundException("Workshop job was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(job.AdvertiserId, isChapelStaff, boundAdvertiserId, "workshop job");
        return ToJobResult(job, false);
    }

    public async Task<WeddingPlannerConceptPackageListResult> ListConceptPackagesAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var versions = await _db.WeddingPlannerConceptPackageVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

        var results = new List<WeddingPlannerConceptPackageVersionResult>(versions.Count);
        foreach (var version in versions)
        {
            results.Add(await ToPackageResultAsync(
                version,
                workspace.CurrentApprovedConceptPackageVersionId == version.Id,
                false,
                cancellationToken));
        }

        return new WeddingPlannerConceptPackageListResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentApprovedConceptPackageVersionId,
            results);
    }

    public async Task<WeddingPlannerConceptPackageVersionResult> GetConceptPackageAsync(
        Guid conceptPackageVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerConceptPackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conceptPackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Concept package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "concept package");
        var workspace = await _db.WeddingPlannerWorkspaces.AsNoTracking()
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);
        return await ToPackageResultAsync(
            version,
            workspace.CurrentApprovedConceptPackageVersionId == version.Id,
            false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WeddingPlannerConceptRoleContributionResult>> ListContributionsAsync(
        Guid conceptPackageVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerConceptPackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conceptPackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Concept package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "concept package");
        var rows = await _db.WeddingPlannerConceptRoleContributions
            .AsNoTracking()
            .Where(x => x.ConceptPackageVersionId == version.Id)
            .ToListAsync(cancellationToken);

        return WeddingPlannerConceptWorkshopLogicalRoles.AllInOrder
            .Select(role => rows.Single(x => x.LogicalRole == role))
            .Select(x => new WeddingPlannerConceptRoleContributionResult(
                x.Id,
                x.AdvertiserId,
                x.WorkspaceId,
                x.ConceptPackageVersionId,
                x.WorkshopJobId,
                x.LogicalRole,
                x.ProducingAgentRunId,
                x.ContributionJson,
                x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListPackageAgentRunsAsync(
        Guid conceptPackageVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerConceptPackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == conceptPackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Concept package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "concept package");
        var job = await _db.WeddingPlannerWorkshopJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingWorkshopJobId, cancellationToken);

        var runIds = new[] { job.StrategyAgentRunId, job.CreativeAgentRunId, job.ProductionAgentRunId }
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

    public async Task<WeddingPlannerConceptPackageDecisionResult> DecideAsync(
        Guid conceptPackageVersionId,
        string decision,
        string rationale,
        string? selectedConceptId,
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
        if (normalizedDecision is not (WeddingPlannerConceptPackageDecisions.Approve or WeddingPlannerConceptPackageDecisions.Reject))
        {
            throw new InvalidOperationException("Decision must be APPROVE or REJECT.");
        }

        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var reason = Required(rationale, nameof(rationale), 2000);

        var existing = await _db.WeddingPlannerConceptPackageDecisions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.ConceptPackageVersionId != conceptPackageVersionId)
            {
                throw new WeddingPlannerNotFoundException("Concept package decision was not found.");
            }

            _planner.EnsureAdvertiserAccessForOrchestration(
                existing.AdvertiserId, isChapelStaff, boundAdvertiserId, "concept package decision");
            var existingVersion = await _db.WeddingPlannerConceptPackageVersions
                .SingleAsync(x => x.Id == existing.ConceptPackageVersionId, cancellationToken);
            var workspace = await _db.WeddingPlannerWorkspaces
                .SingleAsync(x => x.Id == existing.WorkspaceId, cancellationToken);
            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ConceptPackageReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Concept package decision {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return new WeddingPlannerConceptPackageDecisionResult(
                existing.Id,
                existing.ConceptPackageVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Decision,
                existing.SelectedConceptId,
                existing.ActorType,
                existing.ActorLabel,
                existing.Rationale,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                await ToPackageResultAsync(
                    existingVersion,
                    workspace.CurrentApprovedConceptPackageVersionId == existingVersion.Id,
                    true,
                    cancellationToken),
                true);
        }

        var version = await _db.WeddingPlannerConceptPackageVersions
            .SingleOrDefaultAsync(x => x.Id == conceptPackageVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Concept package was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "concept package");
        var workspaceEntity = await _db.WeddingPlannerWorkspaces
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);

        if (!string.Equals(version.Status, WeddingPlannerConceptPackageStatuses.Proposed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only PROPOSED concept packages accept a first decision.");
        }

        string? normalizedSelection = null;
        if (normalizedDecision == WeddingPlannerConceptPackageDecisions.Approve)
        {
            normalizedSelection = Required(selectedConceptId, nameof(selectedConceptId), 32);
            var packageConceptIds = WeddingPlannerConceptWorkshopValidation.ExtractConceptIdsFromPackage(version.DocumentJson);
            if (!packageConceptIds.Contains(normalizedSelection)
                || !WeddingPlannerConceptIds.All.Contains(normalizedSelection))
            {
                throw new InvalidOperationException(
                    "APPROVE requires selectedConceptId of concept_1, concept_2, or concept_3 present in the package.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(selectedConceptId))
        {
            throw new InvalidOperationException("REJECT forbids selectedConceptId.");
        }

        var documentBefore = version.DocumentJson;
        var row = new WeddingPlannerConceptPackageDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = version.AdvertiserId,
            WorkspaceId = version.WorkspaceId,
            ConceptPackageVersionId = version.Id,
            Decision = normalizedDecision,
            SelectedConceptId = normalizedSelection,
            ActorType = actorType,
            ActorLabel = actorLabel,
            Rationale = reason,
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = DateTime.UtcNow
        };
        _db.WeddingPlannerConceptPackageDecisions.Add(row);

        if (normalizedDecision == WeddingPlannerConceptPackageDecisions.Approve)
        {
            if (workspaceEntity.CurrentApprovedConceptPackageVersionId is Guid previousId
                && previousId != version.Id)
            {
                var previous = await _db.WeddingPlannerConceptPackageVersions
                    .SingleOrDefaultAsync(x => x.Id == previousId, cancellationToken);
                if (previous is not null
                    && string.Equals(previous.Status, WeddingPlannerConceptPackageStatuses.Approved, StringComparison.Ordinal))
                {
                    previous.Status = WeddingPlannerConceptPackageStatuses.Superseded;
                    _planner.AddAuditForOrchestration(
                        previous.AdvertiserId,
                        previous.WorkspaceId,
                        null,
                        null,
                        WeddingPlannerAuditActions.ConceptPackageSuperseded,
                        actorType,
                        actorLabel,
                        WeddingPlannerOutcomes.Superseded,
                        requestId,
                        $"Concept package {previous.Id} superseded.");
                }
            }

            version.Status = WeddingPlannerConceptPackageStatuses.Approved;
            workspaceEntity.CurrentApprovedConceptPackageVersionId = version.Id;
            workspaceEntity.UpdatedAt = DateTime.UtcNow;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ConceptPackageApproved,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                $"Concept package {version.Id} approved with selectedConceptId {normalizedSelection}.");
        }
        else
        {
            version.Status = WeddingPlannerConceptPackageStatuses.Rejected;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ConceptPackageRejected,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                $"Concept package {version.Id} rejected.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.Equals(version.DocumentJson, documentBefore, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Concept package DocumentJson must never mutate on decision.");
        }

        return new WeddingPlannerConceptPackageDecisionResult(
            row.Id,
            row.ConceptPackageVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Decision,
            row.SelectedConceptId,
            row.ActorType,
            row.ActorLabel,
            row.Rationale,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            await ToPackageResultAsync(
                version,
                workspaceEntity.CurrentApprovedConceptPackageVersionId == version.Id,
                false,
                cancellationToken),
            false);
    }

    private async Task<WeddingPlannerAgentRun> ExecuteStageAsync(
        WeddingPlannerWorkshopJob job,
        WeddingPlannerWorkspace workspace,
        WeddingPlannerBrandDnaVersion brandDna,
        WeddingPlannerColorProfileVersion color,
        WeddingPlannerResearchReportVersion research,
        List<CanonicalWorkshopStageOutput> priorStages,
        string workerProfileVersion,
        IReadOnlyList<string> expectedConceptIds,
        IReadOnlySet<string> knownSourceIds,
        IReadOnlySet<string> knownPaletteRoles,
        IReadOnlySet<string> forbiddenSourceIds,
        bool requireSynthetic,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var stageRole = WeddingPlannerConceptWorkshopWorkerProfiles.StageLogicalRole(workerProfileVersion);
        var promptPack = WeddingPlannerConceptWorkshopWorkerProfiles.PromptPack(workerProfileVersion);
        var assignedRoles = WeddingPlannerConceptWorkshopWorkerProfiles.AssignedRoles(workerProfileVersion);
        var stageKey = WeddingPlannerConceptWorkshopIdempotency.StageKey(job.IdempotencyKey, workerProfileVersion);

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
            AssignedRolesJson = WeddingPlannerConceptWorkshopValidation.SerializeAssignedRolesJson(assignedRoles),
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
            $"Workshop run {run.Id} ({workerProfileVersion}) started.");
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var messages = BuildStageMessages(job, brandDna, color, research, priorStages, workerProfileVersion, knownSourceIds, knownPaletteRoles);
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
                expectedConceptIds,
                knownSourceIds,
                knownPaletteRoles,
                forbiddenSourceIds,
                job,
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
            StoreStageOutput(job, workerProfileVersion, canonical.OutputJson);
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
                $"Workshop run {run.Id} ({workerProfileVersion}) succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailRunAsync(run, workspace.AdvertiserId, workspace.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw new WeddingPlannerProviderException(run.Id, "Wedding Planner Concept Workshop AI provider failed.", ex);
        }
    }

    private static CanonicalWorkshopStageOutput CanonicalizeStage(
        string content,
        string workerProfileVersion,
        IReadOnlyList<string> expectedConceptIds,
        IReadOnlySet<string> knownSourceIds,
        IReadOnlySet<string> knownPaletteRoles,
        IReadOnlySet<string> forbiddenSourceIds,
        WeddingPlannerWorkshopJob job,
        bool requireSynthetic) =>
        workerProfileVersion switch
        {
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1 =>
                WeddingPlannerConceptWorkshopValidation.CanonicalizeStrategyOutput(content, requireSynthetic),
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1 =>
                WeddingPlannerConceptWorkshopValidation.CanonicalizeCreativeOutput(
                    content, expectedConceptIds, knownSourceIds, knownPaletteRoles, forbiddenSourceIds, requireSynthetic),
            WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1 =>
                WeddingPlannerConceptWorkshopValidation.CanonicalizeProductionOutput(
                    content,
                    expectedConceptIds,
                    job.ChannelFormat,
                    job.CanvasWidth,
                    job.CanvasHeight,
                    knownPaletteRoles,
                    requireSynthetic),
            _ => throw new InvalidOperationException($"Unknown workshop profile '{workerProfileVersion}'.")
        };

    private static IReadOnlyList<WeddingPlannerAiMessage> BuildStageMessages(
        WeddingPlannerWorkshopJob job,
        WeddingPlannerBrandDnaVersion brandDna,
        WeddingPlannerColorProfileVersion color,
        WeddingPlannerResearchReportVersion research,
        IReadOnlyList<CanonicalWorkshopStageOutput> priorStages,
        string workerProfileVersion,
        IReadOnlySet<string> knownSourceIds,
        IReadOnlySet<string> knownPaletteRoles)
    {
        var messages = new List<WeddingPlannerAiMessage>
        {
            new(WeddingPlannerActorTypes.System,
                JsonSerializer.Serialize(new
                {
                    brief = new
                    {
                        job.Objective,
                        job.CampaignGoal,
                        job.AudienceFocus,
                        job.ChannelFormat,
                        canvas = new { width = job.CanvasWidth, height = job.CanvasHeight },
                        deliverables = JsonSerializer.Deserialize<string[]>(job.DeliverablesJson) ?? Array.Empty<string>(),
                        job.Cta,
                        constraints = JsonSerializer.Deserialize<string[]>(job.ConstraintsJson) ?? Array.Empty<string>()
                    },
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
                        summary = research.Summary,
                        sourceIds = knownSourceIds.OrderBy(x => x, StringComparer.Ordinal).ToArray()
                    },
                    workerProfileVersion,
                    note = "Brand DNA and Color Profile are creative constraints only, never factual sourceIds."
                }, JsonOptions))
        };

        foreach (var prior in priorStages)
        {
            messages.Add(new WeddingPlannerAiMessage(WeddingPlannerActorTypes.Planner, prior.OutputJson));
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

    private bool IsLocalAiProvider() =>
        string.Equals(_aiOptions.Provider, WeddingPlannerAiProviderKinds.Local, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(_aiOptions.Provider);

    private async Task FailJobAsync(
        WeddingPlannerWorkshopJob job,
        string actorType,
        string actorLabel,
        string? requestId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        job.Status = WeddingPlannerWorkshopJobStatuses.Failed;
        job.ErrorCode = ex is WeddingPlannerAiProviderException aiEx
            ? Truncate(aiEx.ErrorCode, 64)
            : Truncate(ex.GetType().Name, 64);
        job.ErrorMessage = Truncate(ex.Message, 2000);
        job.CompletedAt = DateTime.UtcNow;
        _planner.AddAuditForOrchestration(
            job.AdvertiserId,
            job.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.WorkshopJobFailed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Failed,
            requestId,
            $"Workshop job {job.Id} failed: {job.ErrorCode}");
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

    private static void AssignJobRunPointer(WeddingPlannerWorkshopJob job, string profile, Guid runId)
    {
        if (profile == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1)
        {
            job.StrategyAgentRunId = runId;
        }
        else if (profile == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1)
        {
            job.CreativeAgentRunId = runId;
        }
        else if (profile == WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1)
        {
            job.ProductionAgentRunId = runId;
        }
    }

    private static void StoreStageOutput(WeddingPlannerWorkshopJob job, string profile, string json)
    {
        if (profile == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1)
        {
            job.StrategyStageOutputJson = json;
        }
        else if (profile == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1)
        {
            job.CreativeStageOutputJson = json;
        }
        else if (profile == WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1)
        {
            job.ProductionStageOutputJson = json;
        }
    }

    private static WeddingPlannerAgentRun ResolveProducingRun(
        string logicalRole,
        IReadOnlyDictionary<string, WeddingPlannerAgentRun> stageRuns) =>
        logicalRole switch
        {
            WeddingPlannerConceptWorkshopLogicalRoles.BrandStrategist =>
                stageRuns[WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1],
            WeddingPlannerConceptWorkshopLogicalRoles.ArtDirector
                or WeddingPlannerConceptWorkshopLogicalRoles.Copywriter =>
                stageRuns[WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1],
            WeddingPlannerConceptWorkshopLogicalRoles.ProductionArtist =>
                stageRuns[WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1],
            _ => throw new InvalidOperationException($"Unknown workshop logical role '{logicalRole}'.")
        };

    private static string SerializeContribution(CanonicalWorkshopContribution contribution)
    {
        if (contribution.LogicalRole == WeddingPlannerConceptWorkshopLogicalRoles.BrandStrategist)
        {
            return JsonSerializer.Serialize(new
            {
                logicalRole = contribution.LogicalRole,
                summary = contribution.Summary,
                concepts = contribution.StrategyConcepts.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    rationale = c.Rationale
                })
            }, JsonOptions);
        }

        if (contribution.LogicalRole == WeddingPlannerConceptWorkshopLogicalRoles.ArtDirector)
        {
            return JsonSerializer.Serialize(new
            {
                logicalRole = contribution.LogicalRole,
                summary = contribution.Summary,
                concepts = contribution.ArtDirections.Select(c => new
                {
                    id = c.Id,
                    visualDirection = c.VisualDirection,
                    paletteRoleRefs = c.PaletteRoleRefs
                })
            }, JsonOptions);
        }

        if (contribution.LogicalRole == WeddingPlannerConceptWorkshopLogicalRoles.Copywriter)
        {
            return JsonSerializer.Serialize(new
            {
                logicalRole = contribution.LogicalRole,
                summary = contribution.Summary,
                concepts = contribution.CopyConcepts.Select(c => new
                {
                    id = c.Id,
                    copy = new
                    {
                        kind = c.Copy.Kind,
                        headline = c.Copy.Headline,
                        body = c.Copy.Body,
                        cta = c.Copy.Cta
                    },
                    factualClaims = c.FactualClaims.Select(f => new
                    {
                        statement = f.Statement,
                        sourceIds = f.SourceIds
                    })
                })
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            logicalRole = contribution.LogicalRole,
            summary = contribution.Summary,
            prototypes = contribution.Prototypes.Select(p => new
            {
                conceptId = p.ConceptId,
                spec = JsonNode.Parse(p.SpecNode.ToJsonString())
            })
        }, JsonOptions);
    }

    private async Task<WeddingPlannerConceptPackageVersionResult> ToPackageResultAsync(
        WeddingPlannerConceptPackageVersion version,
        bool isCurrentApproved,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var job = await _db.WeddingPlannerWorkshopJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingWorkshopJobId, cancellationToken);
        var runIds = new[] { job.StrategyAgentRunId, job.CreativeAgentRunId, job.ProductionAgentRunId }
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .ToList();
        var cost = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => runIds.Contains(x.Id))
            .SumAsync(x => x.EstimatedCostUsd ?? 0m, cancellationToken);

        return new WeddingPlannerConceptPackageVersionResult(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.DocumentJson,
            version.Summary,
            version.ProducingWorkshopJobId,
            version.ProducingAgentRunId,
            version.ApprovedBrandDnaVersionId,
            version.ApprovedBrandDnaVersionNumber,
            version.ApprovedColorProfileVersionId,
            version.ApprovedColorProfileVersionNumber,
            version.ApprovedResearchReportVersionId,
            version.ApprovedResearchReportVersionNumber,
            version.ChannelFormat,
            version.CanvasWidth,
            version.CanvasHeight,
            version.Status,
            version.SourceSystem,
            version.IdempotencyKey,
            version.ActorType,
            version.ActorLabel,
            version.CreatedAt,
            isCurrentApproved,
            cost,
            isReplay);
    }

    private static WeddingPlannerWorkshopJobResult ToJobResult(WeddingPlannerWorkshopJob job, bool isReplay) =>
        new(
            job.Id,
            job.AdvertiserId,
            job.WorkspaceId,
            job.Objective,
            job.CampaignGoal,
            job.AudienceFocus,
            job.ChannelFormat,
            job.CanvasWidth,
            job.CanvasHeight,
            JsonSerializer.Deserialize<string[]>(job.DeliverablesJson, JsonOptions) ?? Array.Empty<string>(),
            job.Cta,
            JsonSerializer.Deserialize<string[]>(job.ConstraintsJson, JsonOptions) ?? Array.Empty<string>(),
            job.InputJson,
            job.InputSha256,
            job.ApprovedBrandDnaVersionId,
            job.ApprovedBrandDnaVersionNumber,
            job.ApprovedColorProfileVersionId,
            job.ApprovedColorProfileVersionNumber,
            job.ApprovedResearchReportVersionId,
            job.ApprovedResearchReportVersionNumber,
            job.StrategyAgentRunId,
            job.CreativeAgentRunId,
            job.ProductionAgentRunId,
            job.OutputConceptPackageVersionId,
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
            throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
