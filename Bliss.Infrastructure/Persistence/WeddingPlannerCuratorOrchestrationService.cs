using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerResearchJobResult(
    Guid ResearchJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string Topic,
    string Objective,
    IReadOnlyList<string> Questions,
    string Geography,
    string Language,
    IReadOnlyList<string> AllowedDomains,
    string InputJson,
    string InputSha256,
    Guid ApprovedBrandDnaVersionId,
    Guid? ApprovedColorProfileVersionId,
    string? ResearchProviderKey,
    string? ResearchAdapterVersion,
    string? ResearchProviderRequestId,
    string? ResearchWorkerKey,
    decimal? ResearchEstimatedCostUsd,
    string? SourceCatalogJson,
    Guid? ResearchAgentRunId,
    Guid? EvidenceAgentRunId,
    Guid? SynthesisRiskAgentRunId,
    Guid? OutputResearchReportVersionId,
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

public sealed record WeddingPlannerResearchReportVersionResult(
    Guid ResearchReportVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingResearchJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedBrandDnaVersionId,
    Guid? ApprovedColorProfileVersionId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerResearchReportListResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedResearchReportVersionId,
    IReadOnlyList<WeddingPlannerResearchReportVersionResult> Versions);

public sealed record WeddingPlannerResearchRoleContributionResult(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid ResearchReportVersionId,
    Guid ResearchJobId,
    string LogicalRole,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerResearchReportDecisionResult(
    Guid DecisionId,
    Guid ResearchReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerResearchReportVersionResult Version,
    bool IsReplay);

/// <summary>
/// Curator research saga: source acquisition + exactly three AI stage profiles + merged report.
/// Does not rely on EF InMemory transactions. Report/contributions are created only after all
/// three stage outputs validate. The app never fetches citation URLs.
/// </summary>
public sealed class WeddingPlannerCuratorOrchestrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly BlissDbContext _db;
    private readonly WeddingPlannerService _planner;
    private readonly IWeddingPlannerResearchProvider _research;
    private readonly IWeddingPlannerAiProvider _ai;
    private readonly WeddingPlannerResearchOptions _researchOptions;

    public WeddingPlannerCuratorOrchestrationService(
        BlissDbContext db,
        WeddingPlannerService planner,
        IWeddingPlannerResearchProvider research,
        IWeddingPlannerAiProvider ai,
        IOptions<WeddingPlannerResearchOptions> researchOptions)
    {
        _db = db;
        _planner = planner;
        _research = research;
        _ai = ai;
        _researchOptions = researchOptions.Value;
    }

    public async Task<WeddingPlannerResearchJobResult> CreateResearchJobAsync(
        Guid workspaceId,
        string topic,
        string objective,
        IReadOnlyList<string> questions,
        string geography,
        string language,
        IReadOnlyList<string>? allowedDomains,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        EnsureResearchProviderAllowed();

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), WeddingPlannerCuratorIdempotency.MaxJobIdempotencyKeyLength);

        var existing = await _db.WeddingPlannerResearchJobs
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.WorkspaceId != workspace.Id || existing.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Research job was not found.");
            }

            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ResearchJobReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Research job {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(existing, true);
        }

        if (workspace.CurrentApprovedBrandDnaVersionId is null)
        {
            throw new InvalidOperationException(
                "A current-approved Brand DNA version is required before starting a research job.");
        }

        var brandDna = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedBrandDnaVersionId.Value, cancellationToken);
        if (brandDna is null || brandDna.WorkspaceId != workspace.Id)
        {
            throw new InvalidOperationException(
                "A current-approved Brand DNA version is required before starting a research job.");
        }

        Guid? colorProfileId = workspace.CurrentApprovedColorProfileVersionId;
        if (colorProfileId is Guid colorId)
        {
            var color = await _db.WeddingPlannerColorProfileVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == colorId, cancellationToken);
            if (color is null || color.WorkspaceId != workspace.Id)
            {
                colorProfileId = null;
            }
        }

        var brief = WeddingPlannerCuratorValidation.CanonicalizeBrief(
            topic,
            objective,
            questions,
            geography,
            language,
            allowedDomains,
            brandDna.Id,
            colorProfileId);

        var now = DateTime.UtcNow;
        var job = new WeddingPlannerResearchJob
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            Topic = brief.Topic,
            Objective = brief.Objective,
            QuestionsJson = JsonSerializer.Serialize(brief.Questions, JsonOptions),
            Geography = brief.Geography,
            Language = brief.Language,
            AllowedDomainsJson = JsonSerializer.Serialize(brief.AllowedDomains, JsonOptions),
            InputJson = brief.InputJson,
            InputSha256 = brief.InputSha256,
            ApprovedBrandDnaVersionId = brandDna.Id,
            ApprovedColorProfileVersionId = colorProfileId,
            ResearchWorkerKey = _research.WorkerKey,
            Status = WeddingPlannerResearchJobStatuses.Running,
            SourceSystem = source,
            IdempotencyKey = key,
            ActorType = actorType,
            ActorLabel = actorLabel,
            StartedAt = now
        };
        _db.WeddingPlannerResearchJobs.Add(job);
        _planner.AddAuditForOrchestration(
            workspace.AdvertiserId,
            workspace.Id,
            null,
            null,
            WeddingPlannerAuditActions.ResearchJobStarted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"Research job {job.Id} started.");
        await _db.SaveChangesAsync(cancellationToken);

        CanonicalSourceCatalog catalog;
        try
        {
            var acquisition = await _research.AcquireSourcesAsync(
                new WeddingPlannerResearchAcquisitionRequest(
                    brief.Topic,
                    brief.Objective,
                    brief.Questions,
                    brief.Geography,
                    brief.Language,
                    brief.AllowedDomains,
                    brandDna.Id,
                    colorProfileId,
                    brandDna.Summary),
                cancellationToken);

            job.ResearchProviderKey = Truncate(acquisition.ProviderKey, 64);
            job.ResearchAdapterVersion = Truncate(acquisition.AdapterVersion, 64);
            job.ResearchProviderRequestId = Truncate(acquisition.ProviderRequestId, 128);
            job.ResearchWorkerKey = Truncate(acquisition.WorkerKey, 64);
            job.ResearchEstimatedCostUsd = acquisition.EstimatedCostUsd;

            catalog = WeddingPlannerCuratorValidation.CanonicalizeSourceCatalog(
                acquisition.SourceCatalogJson,
                brief.AllowedDomains);
            job.SourceCatalogJson = catalog.CatalogJson;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            var code = ex is WeddingPlannerResearchProviderException researchEx
                ? researchEx.ErrorCode
                : Truncate(ex.GetType().Name, 64);
            throw new WeddingPlannerResearchJobProviderException(
                job.Id,
                "Wedding Planner research provider failed.",
                code,
                ex);
        }

        var knownSourceIds = catalog.Sources.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var stageOutputs = new List<CanonicalStageOutput>(3);
        var stageRuns = new Dictionary<string, WeddingPlannerAgentRun>(StringComparer.Ordinal);

        foreach (var profile in WeddingPlannerCuratorWorkerProfiles.All)
        {
            var run = await ExecuteStageAsync(
                job,
                workspace,
                brandDna,
                catalog,
                stageOutputs,
                profile,
                knownSourceIds,
                actorType,
                actorLabel,
                requestId,
                cancellationToken);
            stageRuns[profile] = run;
        }

        try
        {
            var allContributions = stageOutputs.SelectMany(x => x.Contributions).ToList();
            var synthesis = ExtractSynthesisHints(stageOutputs[^1], brief);

            var reportCanonical = WeddingPlannerCuratorValidation.MergeAndCanonicalizeReport(
                brief,
                catalog,
                allContributions,
                job.Id,
                brandDna.Id,
                brandDna.VersionNumber,
                colorProfileId,
                synthesis.ExecutiveSummary,
                synthesis.OpenQuestions,
                synthesis.Risks);

            var nextVersion = await _db.WeddingPlannerResearchReportVersions
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => (int?)x.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            var synthesisRun = stageRuns[WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1];
            var report = new WeddingPlannerResearchReportVersion
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                VersionNumber = nextVersion + 1,
                SchemaVersion = WeddingPlannerSchemaVersions.ResearchReportV1,
                DocumentJson = reportCanonical.DocumentJson,
                Summary = reportCanonical.Summary,
                ProducingResearchJobId = job.Id,
                ProducingAgentRunId = synthesisRun.Id,
                ApprovedBrandDnaVersionId = brandDna.Id,
                ApprovedColorProfileVersionId = colorProfileId,
                Status = WeddingPlannerResearchReportStatuses.Proposed,
                SourceSystem = source,
                IdempotencyKey = WeddingPlannerCuratorIdempotency.ReportKey(key),
                ActorType = actorType,
                ActorLabel = actorLabel,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerResearchReportVersions.Add(report);

            var nowComplete = DateTime.UtcNow;
            foreach (var contribution in reportCanonical.Contributions)
            {
                var producingRun = ResolveProducingRun(contribution.LogicalRole, stageRuns);
                var contributionJson = JsonSerializer.Serialize(
                    new
                    {
                        logicalRole = contribution.LogicalRole,
                        summary = contribution.Summary,
                        findings = contribution.Findings.Select(f => new
                        {
                            type = f.Type,
                            statement = f.Statement,
                            confidence = f.Confidence,
                            citationSourceIds = f.CitationSourceIds
                        })
                    },
                    JsonOptions);

                _db.WeddingPlannerResearchRoleContributions.Add(new WeddingPlannerResearchRoleContribution
                {
                    Id = Guid.NewGuid(),
                    AdvertiserId = workspace.AdvertiserId,
                    WorkspaceId = workspace.Id,
                    ResearchReportVersionId = report.Id,
                    ResearchJobId = job.Id,
                    LogicalRole = contribution.LogicalRole,
                    ProducingAgentRunId = producingRun.Id,
                    ContributionJson = contributionJson,
                    CreatedAt = nowComplete
                });
            }

            synthesisRun.OutputResearchReportVersionId = report.Id;
            job.OutputResearchReportVersionId = report.Id;
            job.Status = WeddingPlannerResearchJobStatuses.Succeeded;
            job.CompletedAt = nowComplete;
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.ResearchReportProposed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Proposed,
                requestId,
                $"Research report {report.Id} proposed.");
            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.ResearchJobSucceeded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"Research job {job.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return ToJobResult(job, false);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException
                                       and not WeddingPlannerForbiddenException
                                       and not WeddingPlannerProviderException
                                       and not WeddingPlannerResearchJobProviderException)
        {
            var synthesisRun = stageRuns.GetValueOrDefault(WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1);
            if (synthesisRun is not null
                && string.Equals(synthesisRun.Status, WeddingPlannerAgentRunStatuses.Succeeded, StringComparison.Ordinal))
            {
                await FailRunAsync(synthesisRun, workspace.AdvertiserId, workspace.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            }

            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<WeddingPlannerResearchJobResult>> ListResearchJobsAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var jobs = await _db.WeddingPlannerResearchJobs
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return jobs.Select(x => ToJobResult(x, false)).ToList();
    }

    public async Task<WeddingPlannerResearchJobResult> GetResearchJobAsync(
        Guid researchJobId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var job = await _db.WeddingPlannerResearchJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == researchJobId, cancellationToken);
        if (job is null)
        {
            throw new WeddingPlannerNotFoundException("Research job was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(job.AdvertiserId, isChapelStaff, boundAdvertiserId, "research job");
        return ToJobResult(job, false);
    }

    public async Task<WeddingPlannerResearchReportListResult> ListResearchReportsAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var versions = await _db.WeddingPlannerResearchReportVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

        var results = new List<WeddingPlannerResearchReportVersionResult>(versions.Count);
        foreach (var version in versions)
        {
            results.Add(await ToReportResultAsync(
                version,
                workspace.CurrentApprovedResearchReportVersionId == version.Id,
                false,
                cancellationToken));
        }

        return new WeddingPlannerResearchReportListResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentApprovedResearchReportVersionId,
            results);
    }

    public async Task<WeddingPlannerResearchReportVersionResult> GetResearchReportAsync(
        Guid researchReportVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerResearchReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == researchReportVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Research report was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "research report");
        var workspace = await _db.WeddingPlannerWorkspaces.AsNoTracking()
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);
        return await ToReportResultAsync(
            version,
            workspace.CurrentApprovedResearchReportVersionId == version.Id,
            false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<WeddingPlannerResearchRoleContributionResult>> ListContributionsAsync(
        Guid researchReportVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerResearchReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == researchReportVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Research report was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "research report");
        var rows = await _db.WeddingPlannerResearchRoleContributions
            .AsNoTracking()
            .Where(x => x.ResearchReportVersionId == version.Id)
            .ToListAsync(cancellationToken);

        return WeddingPlannerCuratorLogicalRoles.AllInOrder
            .Select(role => rows.Single(x => x.LogicalRole == role))
            .Select(x => new WeddingPlannerResearchRoleContributionResult(
                x.Id,
                x.AdvertiserId,
                x.WorkspaceId,
                x.ResearchReportVersionId,
                x.ResearchJobId,
                x.LogicalRole,
                x.ProducingAgentRunId,
                x.ContributionJson,
                x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListReportAgentRunsAsync(
        Guid researchReportVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerResearchReportVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == researchReportVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Research report was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "research report");
        var job = await _db.WeddingPlannerResearchJobs
            .AsNoTracking()
            .SingleAsync(x => x.Id == version.ProducingResearchJobId, cancellationToken);

        var runIds = new[] { job.ResearchAgentRunId, job.EvidenceAgentRunId, job.SynthesisRiskAgentRunId }
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

    public async Task<WeddingPlannerResearchReportDecisionResult> DecideAsync(
        Guid researchReportVersionId,
        string decision,
        string rationale,
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
        if (normalizedDecision is not (WeddingPlannerResearchReportDecisions.Approve or WeddingPlannerResearchReportDecisions.Reject))
        {
            throw new InvalidOperationException("Decision must be APPROVE or REJECT.");
        }

        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var reason = Required(rationale, nameof(rationale), 2000);

        var existing = await _db.WeddingPlannerResearchReportDecisions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            if (existing.ResearchReportVersionId != researchReportVersionId)
            {
                throw new WeddingPlannerNotFoundException("Research report decision was not found.");
            }

            _planner.EnsureAdvertiserAccessForOrchestration(
                existing.AdvertiserId, isChapelStaff, boundAdvertiserId, "research report decision");
            var existingVersion = await _db.WeddingPlannerResearchReportVersions
                .SingleAsync(x => x.Id == existing.ResearchReportVersionId, cancellationToken);
            var workspace = await _db.WeddingPlannerWorkspaces
                .SingleAsync(x => x.Id == existing.WorkspaceId, cancellationToken);
            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ResearchReportReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                Truncate($"Research report decision {existing.Id} replayed.", 2000));
            await _db.SaveChangesAsync(cancellationToken);
            return new WeddingPlannerResearchReportDecisionResult(
                existing.Id,
                existing.ResearchReportVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Decision,
                existing.ActorType,
                existing.ActorLabel,
                existing.Rationale,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                await ToReportResultAsync(
                    existingVersion,
                    workspace.CurrentApprovedResearchReportVersionId == existingVersion.Id,
                    true,
                    cancellationToken),
                true);
        }

        var version = await _db.WeddingPlannerResearchReportVersions
            .SingleOrDefaultAsync(x => x.Id == researchReportVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Research report was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(version.AdvertiserId, isChapelStaff, boundAdvertiserId, "research report");
        var workspaceEntity = await _db.WeddingPlannerWorkspaces
            .SingleAsync(x => x.Id == version.WorkspaceId, cancellationToken);

        if (!string.Equals(version.Status, WeddingPlannerResearchReportStatuses.Proposed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only PROPOSED research reports accept a first decision.");
        }

        var row = new WeddingPlannerResearchReportDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = version.AdvertiserId,
            WorkspaceId = version.WorkspaceId,
            ResearchReportVersionId = version.Id,
            Decision = normalizedDecision,
            ActorType = actorType,
            ActorLabel = actorLabel,
            Rationale = reason,
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = DateTime.UtcNow
        };
        _db.WeddingPlannerResearchReportDecisions.Add(row);

        if (normalizedDecision == WeddingPlannerResearchReportDecisions.Approve)
        {
            if (workspaceEntity.CurrentApprovedResearchReportVersionId is Guid previousId
                && previousId != version.Id)
            {
                var previous = await _db.WeddingPlannerResearchReportVersions
                    .SingleOrDefaultAsync(x => x.Id == previousId, cancellationToken);
                if (previous is not null
                    && string.Equals(previous.Status, WeddingPlannerResearchReportStatuses.Approved, StringComparison.Ordinal))
                {
                    previous.Status = WeddingPlannerResearchReportStatuses.Superseded;
                    _planner.AddAuditForOrchestration(
                        previous.AdvertiserId,
                        previous.WorkspaceId,
                        null,
                        null,
                        WeddingPlannerAuditActions.ResearchReportSuperseded,
                        actorType,
                        actorLabel,
                        WeddingPlannerOutcomes.Superseded,
                        requestId,
                        $"Research report {previous.Id} superseded.");
                }
            }

            version.Status = WeddingPlannerResearchReportStatuses.Approved;
            workspaceEntity.CurrentApprovedResearchReportVersionId = version.Id;
            workspaceEntity.UpdatedAt = DateTime.UtcNow;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ResearchReportApproved,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                $"Research report {version.Id} approved.");
        }
        else
        {
            version.Status = WeddingPlannerResearchReportStatuses.Rejected;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.ResearchReportRejected,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                $"Research report {version.Id} rejected.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new WeddingPlannerResearchReportDecisionResult(
            row.Id,
            row.ResearchReportVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Decision,
            row.ActorType,
            row.ActorLabel,
            row.Rationale,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            await ToReportResultAsync(
                version,
                workspaceEntity.CurrentApprovedResearchReportVersionId == version.Id,
                false,
                cancellationToken),
            false);
    }

    private async Task<WeddingPlannerAgentRun> ExecuteStageAsync(
        WeddingPlannerResearchJob job,
        WeddingPlannerWorkspace workspace,
        WeddingPlannerBrandDnaVersion brandDna,
        CanonicalSourceCatalog catalog,
        List<CanonicalStageOutput> priorStages,
        string workerProfileVersion,
        IReadOnlySet<string> knownSourceIds,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var stageRole = WeddingPlannerCuratorWorkerProfiles.StageLogicalRole(workerProfileVersion);
        var promptPack = WeddingPlannerCuratorWorkerProfiles.PromptPack(workerProfileVersion);
        var assignedRoles = WeddingPlannerCuratorWorkerProfiles.AssignedRoles(workerProfileVersion);
        var stageKey = WeddingPlannerCuratorIdempotency.StageKey(job.IdempotencyKey, workerProfileVersion);

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
            AssignedRolesJson = WeddingPlannerCuratorValidation.SerializeAssignedRolesJson(assignedRoles),
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
            $"Curator run {run.Id} ({workerProfileVersion}) started.");
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var messages = BuildStageMessages(job, brandDna, catalog, priorStages, workerProfileVersion);
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

            var canonical = WeddingPlannerCuratorValidation.CanonicalizeStageOutput(
                completion.Content,
                workerProfileVersion,
                knownSourceIds);

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
                $"Curator run {run.Id} ({workerProfileVersion}) succeeded.");
            await _db.SaveChangesAsync(cancellationToken);
            return run;
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailRunAsync(run, workspace.AdvertiserId, workspace.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            await FailJobAsync(job, actorType, actorLabel, requestId, ex, cancellationToken);
            throw new WeddingPlannerProviderException(run.Id, "Wedding Planner Curator AI provider failed.", ex);
        }
    }

    private static IReadOnlyList<WeddingPlannerAiMessage> BuildStageMessages(
        WeddingPlannerResearchJob job,
        WeddingPlannerBrandDnaVersion brandDna,
        CanonicalSourceCatalog catalog,
        IReadOnlyList<CanonicalStageOutput> priorStages,
        string workerProfileVersion)
    {
        var messages = new List<WeddingPlannerAiMessage>
        {
            new(WeddingPlannerActorTypes.System,
                JsonSerializer.Serialize(new
                {
                    brief = new
                    {
                        job.Topic,
                        job.Objective,
                        questions = JsonSerializer.Deserialize<string[]>(job.QuestionsJson) ?? Array.Empty<string>(),
                        job.Geography,
                        job.Language,
                        allowedDomains = JsonSerializer.Deserialize<string[]>(job.AllowedDomainsJson) ?? Array.Empty<string>()
                    },
                    brandDnaFraming = new
                    {
                        approvedBrandDnaVersionId = brandDna.Id,
                        summary = brandDna.Summary
                    },
                    sourceCatalog = JsonNode.Parse(catalog.CatalogJson),
                    workerProfileVersion
                }, JsonOptions)),
        };

        foreach (var prior in priorStages)
        {
            messages.Add(new WeddingPlannerAiMessage(WeddingPlannerActorTypes.Planner, prior.OutputJson));
        }

        return messages;
    }

    private void EnsureResearchProviderAllowed()
    {
        var isLocal = string.Equals(
            _researchOptions.Provider,
            WeddingPlannerResearchProviderKinds.Local,
            StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(_researchOptions.Provider);

        if (_researchOptions.RequireRemoteResearchProvider && isLocal)
        {
            throw new InvalidOperationException(
                "WeddingPlannerResearch:Provider must be RemoteHttp when RequireRemoteResearchProvider is enabled. "
                + "The Local provider is a deterministic development/test worker, not a production research provider.");
        }
    }

    private async Task FailJobAsync(
        WeddingPlannerResearchJob job,
        string actorType,
        string actorLabel,
        string? requestId,
        Exception ex,
        CancellationToken cancellationToken)
    {
        job.Status = WeddingPlannerResearchJobStatuses.Failed;
        job.ErrorCode = ex is WeddingPlannerResearchProviderException researchEx
            ? Truncate(researchEx.ErrorCode, 64)
            : ex is WeddingPlannerAiProviderException aiEx
                ? Truncate(aiEx.ErrorCode, 64)
                : Truncate(ex.GetType().Name, 64);
        job.ErrorMessage = Truncate(ex.Message, 2000);
        job.CompletedAt = DateTime.UtcNow;
        _planner.AddAuditForOrchestration(
            job.AdvertiserId,
            job.WorkspaceId,
            null,
            null,
            WeddingPlannerAuditActions.ResearchJobFailed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Failed,
            requestId,
            $"Research job {job.Id} failed: {job.ErrorCode}");
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

    private static void AssignJobRunPointer(WeddingPlannerResearchJob job, string profile, Guid runId)
    {
        if (profile == WeddingPlannerCuratorWorkerProfiles.ResearchV1)
        {
            job.ResearchAgentRunId = runId;
        }
        else if (profile == WeddingPlannerCuratorWorkerProfiles.EvidenceV1)
        {
            job.EvidenceAgentRunId = runId;
        }
        else if (profile == WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1)
        {
            job.SynthesisRiskAgentRunId = runId;
        }
    }

    private static void StoreStageOutput(WeddingPlannerResearchJob job, string profile, string json)
    {
        if (profile == WeddingPlannerCuratorWorkerProfiles.ResearchV1)
        {
            job.ResearchStageOutputJson = json;
        }
        else if (profile == WeddingPlannerCuratorWorkerProfiles.EvidenceV1)
        {
            job.EvidenceStageOutputJson = json;
        }
        else if (profile == WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1)
        {
            job.SynthesisRiskStageOutputJson = json;
        }
    }

    private static WeddingPlannerAgentRun ResolveProducingRun(
        string logicalRole,
        IReadOnlyDictionary<string, WeddingPlannerAgentRun> stageRuns)
    {
        if (logicalRole is WeddingPlannerCuratorLogicalRoles.MarketLandscapeResearcher
            or WeddingPlannerCuratorLogicalRoles.AudienceContextResearcher
            or WeddingPlannerCuratorLogicalRoles.CompetitorSignalsResearcher
            or WeddingPlannerCuratorLogicalRoles.ChannelFormatResearcher)
        {
            return stageRuns[WeddingPlannerCuratorWorkerProfiles.ResearchV1];
        }

        if (logicalRole is WeddingPlannerCuratorLogicalRoles.EvidenceAnalyst
            or WeddingPlannerCuratorLogicalRoles.SourceVerifier)
        {
            return stageRuns[WeddingPlannerCuratorWorkerProfiles.EvidenceV1];
        }

        return stageRuns[WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1];
    }

    private static (string ExecutiveSummary, IReadOnlyList<string> OpenQuestions, IReadOnlyList<string> Risks)
        ExtractSynthesisHints(CanonicalStageOutput synthesisStage, CanonicalResearchBrief brief)
    {
        var synthesizer = synthesisStage.Contributions
            .Single(x => x.LogicalRole == WeddingPlannerCuratorLogicalRoles.ResearchSynthesizer);
        var reviewer = synthesisStage.Contributions
            .Single(x => x.LogicalRole == WeddingPlannerCuratorLogicalRoles.ClaimsRiskReviewer);

        var openQuestions = synthesizer.Findings
            .Where(f => f.Type == WeddingPlannerFindingTypes.Gap)
            .Select(f => f.Statement)
            .DefaultIfEmpty($"What live evidence would confirm findings about {brief.Topic}?")
            .Take(WeddingPlannerCuratorValidation.MaxOpenQuestions)
            .ToList();

        var risks = reviewer.Findings
            .Where(f => f.Type == WeddingPlannerFindingTypes.Risk)
            .Select(f => f.Statement)
            .DefaultIfEmpty("Research approval is not creative, legal, or matching approval.")
            .Take(WeddingPlannerCuratorValidation.MaxRisks)
            .ToList();

        return (synthesizer.Summary, openQuestions, risks);
    }

    private async Task<WeddingPlannerResearchReportVersionResult> ToReportResultAsync(
        WeddingPlannerResearchReportVersion version,
        bool isCurrentApproved,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var job = await _db.WeddingPlannerResearchJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == version.ProducingResearchJobId, cancellationToken);
        decimal? total = null;
        if (job is not null)
        {
            var runIds = new[] { job.ResearchAgentRunId, job.EvidenceAgentRunId, job.SynthesisRiskAgentRunId }
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .ToList();
            var runCost = await _db.WeddingPlannerAgentRuns
                .AsNoTracking()
                .Where(x => runIds.Contains(x.Id))
                .SumAsync(x => x.EstimatedCostUsd ?? 0m, cancellationToken);
            total = (job.ResearchEstimatedCostUsd ?? 0m) + runCost;
        }

        return new WeddingPlannerResearchReportVersionResult(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.DocumentJson,
            version.Summary,
            version.ProducingResearchJobId,
            version.ProducingAgentRunId,
            version.ApprovedBrandDnaVersionId,
            version.ApprovedColorProfileVersionId,
            version.Status,
            version.SourceSystem,
            version.IdempotencyKey,
            version.ActorType,
            version.ActorLabel,
            version.CreatedAt,
            isCurrentApproved,
            total,
            isReplay);
    }

    private static WeddingPlannerResearchJobResult ToJobResult(WeddingPlannerResearchJob job, bool isReplay) =>
        new(
            job.Id,
            job.AdvertiserId,
            job.WorkspaceId,
            job.Topic,
            job.Objective,
            JsonSerializer.Deserialize<string[]>(job.QuestionsJson, JsonOptions) ?? Array.Empty<string>(),
            job.Geography,
            job.Language,
            JsonSerializer.Deserialize<string[]>(job.AllowedDomainsJson, JsonOptions) ?? Array.Empty<string>(),
            job.InputJson,
            job.InputSha256,
            job.ApprovedBrandDnaVersionId,
            job.ApprovedColorProfileVersionId,
            job.ResearchProviderKey,
            job.ResearchAdapterVersion,
            job.ResearchProviderRequestId,
            job.ResearchWorkerKey,
            job.ResearchEstimatedCostUsd,
            job.SourceCatalogJson,
            job.ResearchAgentRunId,
            job.EvidenceAgentRunId,
            job.SynthesisRiskAgentRunId,
            job.OutputResearchReportVersionId,
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
