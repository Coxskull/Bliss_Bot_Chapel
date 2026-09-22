using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerAgentRunResult(
    Guid AgentRunId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid? SessionId,
    string LogicalRole,
    string WorkerKey,
    string PromptPackVersion,
    string? ProviderKey,
    string? ModelId,
    string? AdapterVersion,
    Guid? TriggerMessageId,
    Guid? OutputMessageId,
    Guid? OutputBrandDnaVersionId,
    string? RequestId,
    string? ProviderRequestId,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string? Outcome,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    decimal? EstimatedCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerTurnResult(
    Guid AgentRunId,
    Guid SessionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    WeddingPlannerMessageResult HumanMessage,
    WeddingPlannerMessageResult? PlannerMessage,
    WeddingPlannerAgentRunResult AgentRun,
    bool IsReplay);

public sealed record WeddingPlannerBrandDnaVersionResult(
    Guid BrandDnaVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingAgentRunId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    bool IsReplay);

public sealed record WeddingPlannerBrandDnaListResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedBrandDnaVersionId,
    IReadOnlyList<WeddingPlannerBrandDnaVersionResult> Versions);

public sealed record WeddingPlannerBrandDnaDecisionResult(
    Guid DecisionId,
    Guid BrandDnaVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string ActorType,
    string ActorLabel,
    string? Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerBrandDnaVersionResult Version,
    bool IsReplay);

public sealed class WeddingPlannerOrchestrationService
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly BlissDbContext _db;
    private readonly WeddingPlannerService _planner;
    private readonly IWeddingPlannerAiProvider _ai;

    public WeddingPlannerOrchestrationService(
        BlissDbContext db,
        WeddingPlannerService planner,
        IWeddingPlannerAiProvider ai)
    {
        _db = db;
        _planner = planner;
        _ai = ai;
    }

    public async Task<WeddingPlannerTurnResult> ExecuteConciergeTurnAsync(
        Guid sessionId,
        string body,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var session = await _planner.RequireSessionForOrchestrationAsync(
            sessionId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var text = Required(body, nameof(body), 8000);

        var existingRun = await _db.WeddingPlannerAgentRuns
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existingRun is not null)
        {
            if (existingRun.SessionId != session.Id || existingRun.AdvertiserId != session.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Agent run was not found.");
            }

            return await BuildTurnReplayAsync(existingRun, actorType, actorLabel, requestId, cancellationToken);
        }

        var humanActor = isChapelStaff ? WeddingPlannerActorTypes.Operator : WeddingPlannerActorTypes.Advertiser;
        var human = await _planner.AppendClientMessageForOrchestrationAsync(
            session.Id,
            humanActor,
            text,
            source,
            $"{key}:human",
            isChapelStaff,
            boundAdvertiserId,
            actorType,
            actorLabel,
            requestId,
            cancellationToken);

        var startedAt = DateTime.UtcNow;
        var run = new WeddingPlannerAgentRun
        {
            Id = Guid.NewGuid(),
            AdvertiserId = session.AdvertiserId,
            WorkspaceId = session.WorkspaceId,
            SessionId = session.Id,
            LogicalRole = WeddingPlannerAgentRoles.Concierge,
            WorkerKey = _ai.WorkerKey,
            PromptPackVersion = WeddingPlannerPromptPacks.ConciergeV1,
            TriggerMessageId = human.MessageId,
            RequestId = requestId,
            SourceSystem = source,
            IdempotencyKey = key,
            Status = WeddingPlannerAgentRunStatuses.Running,
            StartedAt = startedAt
        };
        _db.WeddingPlannerAgentRuns.Add(run);
        _planner.AddAuditForOrchestration(
            session.AdvertiserId,
            session.WorkspaceId,
            session.Id,
            human.MessageId,
            WeddingPlannerAuditActions.AgentRunStarted,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            $"Concierge run {run.Id} started.");
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var contextMessages = await _db.WeddingPlannerConversationMessages
                .AsNoTracking()
                .Where(x => x.SessionId == session.Id)
                .OrderBy(x => x.SequenceNumber)
                .Select(x => new WeddingPlannerAiMessage(x.ActorType, x.Body))
                .ToListAsync(cancellationToken);

            var completion = await _ai.CompleteAsync(
                new WeddingPlannerAiCompletionRequest(
                    WeddingPlannerAgentRoles.Concierge,
                    WeddingPlannerPromptPacks.ConciergeV1,
                    contextMessages,
                    WeddingPlannerResponseFormats.Text,
                    512),
                cancellationToken);

            var plannerMessage = await _planner.AppendPlannerMessageForOrchestrationAsync(
                session.Id,
                completion.Content,
                source,
                $"{key}:planner",
                "Wedding Planner Concierge",
                requestId,
                cancellationToken);

            run.ProviderKey = completion.ProviderKey;
            run.ModelId = completion.ModelId;
            run.AdapterVersion = completion.AdapterVersion;
            run.ProviderRequestId = completion.ProviderRequestId;
            run.WorkerKey = completion.WorkerKey;
            run.OutputMessageId = plannerMessage.MessageId;
            run.PromptTokens = completion.PromptTokens;
            run.CompletionTokens = completion.CompletionTokens;
            run.TotalTokens = completion.TotalTokens;
            run.EstimatedCostUsd = completion.EstimatedCostUsd;
            run.Status = WeddingPlannerAgentRunStatuses.Succeeded;
            run.Outcome = WeddingPlannerOutcomes.Succeeded;
            run.CompletedAt = DateTime.UtcNow;
            _planner.AddAuditForOrchestration(
                session.AdvertiserId,
                session.WorkspaceId,
                session.Id,
                plannerMessage.MessageId,
                WeddingPlannerAuditActions.AgentRunSucceeded,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Succeeded,
                requestId,
                $"Concierge run {run.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);

            return new WeddingPlannerTurnResult(
                run.Id,
                session.Id,
                session.WorkspaceId,
                session.AdvertiserId,
                human,
                plannerMessage,
                ToAgentRunResult(run, false),
                false);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailRunAsync(run, session.AdvertiserId, session.WorkspaceId, session.Id, actorType, actorLabel, requestId, ex, cancellationToken);
            throw new WeddingPlannerProviderException(run.Id, "Wedding Planner Concierge provider failed.", ex);
        }
    }

    public async Task<IReadOnlyList<WeddingPlannerAgentRunResult>> ListSessionAgentRunsAsync(
        Guid sessionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var session = await _planner.RequireSessionForOrchestrationAsync(
            sessionId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var runs = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return runs.Select(x => ToAgentRunResult(x, false)).ToList();
    }

    public async Task<WeddingPlannerAgentRunResult> GetAgentRunAsync(
        Guid agentRunId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.WeddingPlannerAgentRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == agentRunId, cancellationToken);
        if (run is null)
        {
            throw new WeddingPlannerNotFoundException("Agent run was not found.");
        }

        _planner.EnsureAdvertiserAccessForOrchestration(run.AdvertiserId, isChapelStaff, boundAdvertiserId, "agent run");
        return ToAgentRunResult(run, false);
    }

    public async Task<WeddingPlannerBrandDnaVersionResult> InterpretBrandDnaAsync(
        Guid workspaceId,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);

        var existingRun = await _db.WeddingPlannerAgentRuns
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existingRun is not null)
        {
            if (existingRun.WorkspaceId != workspace.Id || existingRun.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Agent run was not found.");
            }

            _planner.AddAuditForOrchestration(
                workspace.AdvertiserId,
                workspace.Id,
                null,
                null,
                WeddingPlannerAuditActions.BrandDnaReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                $"Brand DNA interpret replay for run {existingRun.Id}.");
            await _db.SaveChangesAsync(cancellationToken);

            if (existingRun.OutputBrandDnaVersionId is null)
            {
                throw new WeddingPlannerNotFoundException("Brand DNA version was not found.");
            }

            return await GetBrandDnaVersionInternalAsync(
                existingRun.OutputBrandDnaVersionId.Value, workspace, true, cancellationToken);
        }

        var existingVersion = await _db.WeddingPlannerBrandDnaVersions
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existingVersion is not null)
        {
            if (existingVersion.WorkspaceId != workspace.Id || existingVersion.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Brand DNA version was not found.");
            }

            return await GetBrandDnaVersionInternalAsync(existingVersion.Id, workspace, true, cancellationToken);
        }

        var startedAt = DateTime.UtcNow;
        var run = new WeddingPlannerAgentRun
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            SessionId = null,
            LogicalRole = WeddingPlannerAgentRoles.BrandDnaInterpreter,
            WorkerKey = _ai.WorkerKey,
            PromptPackVersion = WeddingPlannerPromptPacks.BrandDnaV1,
            RequestId = requestId,
            SourceSystem = source,
            IdempotencyKey = key,
            Status = WeddingPlannerAgentRunStatuses.Running,
            StartedAt = startedAt
        };
        _db.WeddingPlannerAgentRuns.Add(run);
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
            $"Brand DNA interpreter run {run.Id} started.");
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var approved = workspace.CurrentApprovedBrandDnaVersionId is null
                ? null
                : await _db.WeddingPlannerBrandDnaVersions
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == workspace.CurrentApprovedBrandDnaVersionId, cancellationToken);

            var messages = await _db.WeddingPlannerConversationMessages
                .AsNoTracking()
                .Where(x => x.WorkspaceId == workspace.Id)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.SequenceNumber)
                .Select(x => new WeddingPlannerAiMessage(x.ActorType, x.Body))
                .ToListAsync(cancellationToken);

            if (approved is not null)
            {
                messages = messages
                    .Append(new WeddingPlannerAiMessage(
                        WeddingPlannerActorTypes.System,
                        $"Prior approved Brand DNA summary: {approved.Summary}"))
                    .ToList();
            }

            var completion = await _ai.CompleteAsync(
                new WeddingPlannerAiCompletionRequest(
                    WeddingPlannerAgentRoles.BrandDnaInterpreter,
                    WeddingPlannerPromptPacks.BrandDnaV1,
                    messages,
                    WeddingPlannerResponseFormats.Json,
                    2048),
                cancellationToken);

            var (documentJson, summary) = CanonicalizeBrandDnaJson(completion.Content);
            var nextVersion = await _db.WeddingPlannerBrandDnaVersions
                .Where(x => x.WorkspaceId == workspace.Id)
                .Select(x => (int?)x.VersionNumber)
                .MaxAsync(cancellationToken) ?? 0;

            var version = new WeddingPlannerBrandDnaVersion
            {
                Id = Guid.NewGuid(),
                AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.Id,
                VersionNumber = nextVersion + 1,
                SchemaVersion = WeddingPlannerSchemaVersions.BrandDnaV1,
                DocumentJson = documentJson,
                Summary = summary,
                ProducingAgentRunId = run.Id,
                Status = WeddingPlannerBrandDnaStatuses.Proposed,
                SourceSystem = source,
                IdempotencyKey = key,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeddingPlannerBrandDnaVersions.Add(version);

            run.ProviderKey = completion.ProviderKey;
            run.ModelId = completion.ModelId;
            run.AdapterVersion = completion.AdapterVersion;
            run.ProviderRequestId = completion.ProviderRequestId;
            run.WorkerKey = completion.WorkerKey;
            run.OutputBrandDnaVersionId = version.Id;
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
                WeddingPlannerAuditActions.BrandDnaProposed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Proposed,
                requestId,
                $"Brand DNA v{version.VersionNumber} proposed by run {run.Id}.");
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
                $"Brand DNA interpreter run {run.Id} succeeded.");
            await _db.SaveChangesAsync(cancellationToken);

            return new WeddingPlannerBrandDnaVersionResult(
                version.Id,
                version.AdvertiserId,
                version.WorkspaceId,
                version.VersionNumber,
                version.SchemaVersion,
                version.DocumentJson,
                version.Summary,
                version.ProducingAgentRunId,
                version.Status,
                version.SourceSystem,
                version.IdempotencyKey,
                version.CreatedAt,
                false,
                false);
        }
        catch (Exception ex) when (ex is not WeddingPlannerNotFoundException and not WeddingPlannerForbiddenException)
        {
            await FailRunAsync(run, workspace.AdvertiserId, workspace.Id, null, actorType, actorLabel, requestId, ex, cancellationToken);
            throw new WeddingPlannerProviderException(run.Id, "Wedding Planner Brand DNA interpreter provider failed.", ex);
        }
    }

    public async Task<WeddingPlannerBrandDnaListResult> ListBrandDnaAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var versions = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderBy(x => x.VersionNumber)
            .ToListAsync(cancellationToken);

        return new WeddingPlannerBrandDnaListResult(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.CurrentApprovedBrandDnaVersionId,
            versions.Select(x => ToBrandDnaResult(x, workspace.CurrentApprovedBrandDnaVersionId == x.Id, false)).ToList());
    }

    public async Task<WeddingPlannerBrandDnaVersionResult> GetBrandDnaAsync(
        Guid brandDnaVersionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var version = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == brandDnaVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Brand DNA version was not found.");
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
        return ToBrandDnaResult(version, workspace.CurrentApprovedBrandDnaVersionId == version.Id, false);
    }

    public async Task<WeddingPlannerBrandDnaDecisionResult> DecideBrandDnaAsync(
        Guid brandDnaVersionId,
        string decision,
        string? rationale,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var normalizedDecision = Required(decision, nameof(decision), 32).ToUpperInvariant();
        if (normalizedDecision is not (WeddingPlannerBrandDnaDecisions.Approve or WeddingPlannerBrandDnaDecisions.Reject))
        {
            throw new InvalidOperationException("Decision must be APPROVE or REJECT.");
        }

        var existing = await _db.WeddingPlannerBrandDnaDecisions
            .Include(x => x.BrandDnaVersion)
            .SingleOrDefaultAsync(x => x.SourceSystem == source && x.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            var workspaceReplay = await _planner.RequireWorkspaceForOrchestrationAsync(
                existing.WorkspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);
            if (existing.BrandDnaVersionId != brandDnaVersionId)
            {
                throw new WeddingPlannerNotFoundException("Brand DNA decision was not found.");
            }

            _planner.AddAuditForOrchestration(
                existing.AdvertiserId,
                existing.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.BrandDnaReplayed,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Replayed,
                requestId,
                $"Brand DNA decision replay {existing.Id}.");
            await _db.SaveChangesAsync(cancellationToken);

            return new WeddingPlannerBrandDnaDecisionResult(
                existing.Id,
                existing.BrandDnaVersionId,
                existing.WorkspaceId,
                existing.AdvertiserId,
                existing.Decision,
                existing.ActorType,
                existing.ActorLabel,
                existing.Rationale,
                existing.SourceSystem,
                existing.IdempotencyKey,
                existing.OccurredAt,
                ToBrandDnaResult(
                    existing.BrandDnaVersion,
                    workspaceReplay.CurrentApprovedBrandDnaVersionId == existing.BrandDnaVersionId,
                    true),
                true);
        }

        var version = await _db.WeddingPlannerBrandDnaVersions
            .SingleOrDefaultAsync(x => x.Id == brandDnaVersionId, cancellationToken);
        if (version is null)
        {
            throw new WeddingPlannerNotFoundException("Brand DNA version was not found.");
        }

        var workspace = await _planner.RequireWorkspaceForOrchestrationAsync(
            version.WorkspaceId, isChapelStaff, boundAdvertiserId, cancellationToken);

        if (!string.Equals(version.Status, WeddingPlannerBrandDnaStatuses.Proposed, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Only PROPOSED Brand DNA versions can receive a decision. Current status is {version.Status}.");
        }

        var originalDocument = version.DocumentJson;
        var originalSummary = version.Summary;
        var now = DateTime.UtcNow;
        var row = new WeddingPlannerBrandDnaDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = version.AdvertiserId,
            WorkspaceId = version.WorkspaceId,
            BrandDnaVersionId = version.Id,
            Decision = normalizedDecision,
            ActorType = actorType,
            ActorLabel = actorLabel,
            Rationale = string.IsNullOrWhiteSpace(rationale) ? null : Required(rationale, nameof(rationale), 2000),
            SourceSystem = source,
            IdempotencyKey = key,
            OccurredAt = now
        };
        _db.WeddingPlannerBrandDnaDecisions.Add(row);

        if (normalizedDecision == WeddingPlannerBrandDnaDecisions.Approve)
        {
            if (workspace.CurrentApprovedBrandDnaVersionId is Guid previousId && previousId != version.Id)
            {
                var previous = await _db.WeddingPlannerBrandDnaVersions
                    .SingleOrDefaultAsync(x => x.Id == previousId, cancellationToken);
                if (previous is not null
                    && string.Equals(previous.Status, WeddingPlannerBrandDnaStatuses.Approved, StringComparison.Ordinal))
                {
                    previous.Status = WeddingPlannerBrandDnaStatuses.Superseded;
                }
            }

            version.Status = WeddingPlannerBrandDnaStatuses.Approved;
            workspace.CurrentApprovedBrandDnaVersionId = version.Id;
            workspace.UpdatedAt = now;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.BrandDnaApproved,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Approved,
                requestId,
                $"Brand DNA v{version.VersionNumber} approved.");
        }
        else
        {
            version.Status = WeddingPlannerBrandDnaStatuses.Rejected;
            _planner.AddAuditForOrchestration(
                version.AdvertiserId,
                version.WorkspaceId,
                null,
                null,
                WeddingPlannerAuditActions.BrandDnaRejected,
                actorType,
                actorLabel,
                WeddingPlannerOutcomes.Rejected,
                requestId,
                $"Brand DNA v{version.VersionNumber} rejected.");
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (!string.Equals(version.DocumentJson, originalDocument, StringComparison.Ordinal)
            || !string.Equals(version.Summary, originalSummary, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Brand DNA DocumentJson and Summary are immutable.");
        }

        return new WeddingPlannerBrandDnaDecisionResult(
            row.Id,
            row.BrandDnaVersionId,
            row.WorkspaceId,
            row.AdvertiserId,
            row.Decision,
            row.ActorType,
            row.ActorLabel,
            row.Rationale,
            row.SourceSystem,
            row.IdempotencyKey,
            row.OccurredAt,
            ToBrandDnaResult(version, workspace.CurrentApprovedBrandDnaVersionId == version.Id, false),
            false);
    }

    private async Task<WeddingPlannerTurnResult> BuildTurnReplayAsync(
        WeddingPlannerAgentRun run,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        if (run.TriggerMessageId is null)
        {
            throw new WeddingPlannerNotFoundException("Conversation message was not found.");
        }

        var human = await _db.WeddingPlannerConversationMessages
            .AsNoTracking()
            .SingleAsync(x => x.Id == run.TriggerMessageId.Value, cancellationToken);
        WeddingPlannerConversationMessage? planner = null;
        if (run.OutputMessageId is not null)
        {
            planner = await _db.WeddingPlannerConversationMessages
                .AsNoTracking()
                .SingleAsync(x => x.Id == run.OutputMessageId.Value, cancellationToken);
        }

        _planner.AddAuditForOrchestration(
            run.AdvertiserId,
            run.WorkspaceId,
            run.SessionId,
            run.TriggerMessageId,
            WeddingPlannerAuditActions.AgentRunReplayed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Replayed,
            requestId,
            $"Concierge run {run.Id} replayed.");
        await _db.SaveChangesAsync(cancellationToken);

        return new WeddingPlannerTurnResult(
            run.Id,
            run.SessionId ?? Guid.Empty,
            run.WorkspaceId,
            run.AdvertiserId,
            _planner.ToMessageResultForOrchestration(human, true),
            planner is null ? null : _planner.ToMessageResultForOrchestration(planner, true),
            ToAgentRunResult(run, true),
            true);
    }

    private async Task<WeddingPlannerBrandDnaVersionResult> GetBrandDnaVersionInternalAsync(
        Guid versionId,
        WeddingPlannerWorkspace workspace,
        bool isReplay,
        CancellationToken cancellationToken)
    {
        var version = await _db.WeddingPlannerBrandDnaVersions
            .AsNoTracking()
            .SingleAsync(x => x.Id == versionId, cancellationToken);
        return ToBrandDnaResult(version, workspace.CurrentApprovedBrandDnaVersionId == version.Id, isReplay);
    }

    private async Task FailRunAsync(
        WeddingPlannerAgentRun run,
        Guid advertiserId,
        Guid workspaceId,
        Guid? sessionId,
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
            sessionId,
            null,
            WeddingPlannerAuditActions.AgentRunFailed,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Failed,
            requestId,
            $"Agent run {run.Id} failed: {run.ErrorCode}");
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static (string DocumentJson, string Summary) CanonicalizeBrandDnaJson(string content)
    {
        BrandDnaDocumentV1? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<BrandDnaDocumentV1>(content, CanonicalJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Brand DNA interpreter returned invalid JSON.", ex);
        }

        if (parsed is null)
        {
            throw new InvalidOperationException("Brand DNA interpreter must return a JSON object.");
        }

        if (!string.Equals(parsed.SchemaVersion, WeddingPlannerSchemaVersions.BrandDnaV1, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Brand DNA schemaVersion must be {WeddingPlannerSchemaVersions.BrandDnaV1}.");
        }

        var canonical = new BrandDnaDocumentV1(
            SchemaVersion: WeddingPlannerSchemaVersions.BrandDnaV1,
            BrandVoice: parsed.BrandVoice ?? string.Empty,
            Audience: parsed.Audience ?? string.Empty,
            OffersAndServices: parsed.OffersAndServices ?? Array.Empty<string>(),
            Markets: parsed.Markets ?? Array.Empty<string>(),
            Tone: parsed.Tone ?? string.Empty,
            LanguageDo: parsed.LanguageDo ?? Array.Empty<string>(),
            LanguageDont: parsed.LanguageDont ?? Array.Empty<string>(),
            ComplianceNotes: parsed.ComplianceNotes ?? Array.Empty<string>(),
            OpenQuestions: parsed.OpenQuestions ?? Array.Empty<string>());

        var json = JsonSerializer.Serialize(canonical, CanonicalJsonOptions);
        var summary = Truncate(
            $"Brand DNA proposal: voice={canonical.BrandVoice}; audience={canonical.Audience}; tone={canonical.Tone}.",
            2000);
        return (json, summary);
    }

    private sealed record BrandDnaDocumentV1(
        string SchemaVersion,
        string? BrandVoice,
        string? Audience,
        IReadOnlyList<string>? OffersAndServices,
        IReadOnlyList<string>? Markets,
        string? Tone,
        IReadOnlyList<string>? LanguageDo,
        IReadOnlyList<string>? LanguageDont,
        IReadOnlyList<string>? ComplianceNotes,
        IReadOnlyList<string>? OpenQuestions);

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

    private static WeddingPlannerBrandDnaVersionResult ToBrandDnaResult(
        WeddingPlannerBrandDnaVersion version,
        bool isCurrentApproved,
        bool isReplay) =>
        new(
            version.Id,
            version.AdvertiserId,
            version.WorkspaceId,
            version.VersionNumber,
            version.SchemaVersion,
            version.DocumentJson,
            version.Summary,
            version.ProducingAgentRunId,
            version.Status,
            version.SourceSystem,
            version.IdempotencyKey,
            version.CreatedAt,
            isCurrentApproved,
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
