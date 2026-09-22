using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record WeddingPlannerWorkspaceResult(
    Guid WorkspaceId,
    Guid AdvertiserId,
    string AdvertiserName,
    bool IsPrimary,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsReplay);

public sealed record WeddingPlannerSessionResult(
    Guid SessionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    bool IsReplay);

public sealed record WeddingPlannerMessageResult(
    Guid MessageId,
    Guid SessionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    int SequenceNumber,
    string ActorType,
    string ActorLabel,
    string Body,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    bool IsReplay);

public sealed record WeddingPlannerAuditResult(
    Guid Id,
    Guid AdvertiserId,
    Guid? WorkspaceId,
    Guid? SessionId,
    Guid? MessageId,
    string Action,
    string ActorType,
    string ActorLabel,
    string Outcome,
    string? RequestId,
    string? Detail,
    DateTime OccurredAt);

public sealed class WeddingPlannerService
{
    private static readonly HashSet<string> AllowedMessageActors = new(StringComparer.OrdinalIgnoreCase)
    {
        WeddingPlannerActorTypes.Advertiser,
        WeddingPlannerActorTypes.Operator,
        WeddingPlannerActorTypes.System
    };

    private readonly BlissDbContext _db;

    public WeddingPlannerService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WeddingPlannerWorkspaceResult>> ListWorkspacesAsync(
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.WeddingPlannerWorkspaces.AsNoTracking().AsQueryable();
        if (!isChapelStaff)
        {
            if (!boundAdvertiserId.HasValue)
            {
                return [];
            }

            query = query.Where(x => x.AdvertiserId == boundAdvertiserId.Value);
        }

        return await query
            .OrderBy(x => x.CreatedAt)
            .Select(x => new WeddingPlannerWorkspaceResult(
                x.Id,
                x.AdvertiserId,
                x.Advertiser.Name,
                x.IsPrimary,
                x.Status,
                x.SourceSystem,
                x.IdempotencyKey,
                x.CreatedAt,
                x.UpdatedAt,
                false))
            .ToListAsync(cancellationToken);
    }

    public async Task<WeddingPlannerWorkspaceResult> GetWorkspaceAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _db.WeddingPlannerWorkspaces
            .AsNoTracking()
            .Include(x => x.Advertiser)
            .SingleOrDefaultAsync(x => x.Id == workspaceId, cancellationToken);

        if (workspace is null)
        {
            throw new WeddingPlannerNotFoundException("Wedding Planner workspace was not found.");
        }

        EnsureAdvertiserAccess(workspace.AdvertiserId, isChapelStaff, boundAdvertiserId, "workspace");
        return ToWorkspaceResult(workspace, false);
    }

    public async Task<WeddingPlannerWorkspaceResult> OpenPrimaryWorkspaceAsync(
        Guid requestedAdvertiserId,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var advertiserId = ResolveTargetAdvertiser(requestedAdvertiserId, isChapelStaff, boundAdvertiserId);
        EnsureAdvertiserAccess(advertiserId, isChapelStaff, boundAdvertiserId, "workspace");

        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);

        var replayByKey = await _db.WeddingPlannerWorkspaces
            .Include(x => x.Advertiser)
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replayByKey is not null)
        {
            if (replayByKey.AdvertiserId != advertiserId)
            {
                throw new WeddingPlannerNotFoundException("Wedding Planner workspace was not found.");
            }

            return ToWorkspaceResult(replayByKey, true);
        }

        var existing = await _db.WeddingPlannerWorkspaces
            .Include(x => x.Advertiser)
            .SingleOrDefaultAsync(x => x.AdvertiserId == advertiserId, cancellationToken);
        if (existing is not null)
        {
            return ToWorkspaceResult(existing, true);
        }

        var advertiser = await _db.Advertisers
            .SingleOrDefaultAsync(x => x.Id == advertiserId, cancellationToken);
        if (advertiser is null)
        {
            throw new WeddingPlannerNotFoundException("AdvertiserId does not reference an existing advertiser.");
        }

        var now = DateTime.UtcNow;
        var workspace = new WeddingPlannerWorkspace
        {
            Id = Guid.NewGuid(),
            AdvertiserId = advertiserId,
            IsPrimary = true,
            Status = WeddingPlannerStatuses.Active,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now,
            UpdatedAt = now,
            Advertiser = advertiser
        };
        _db.WeddingPlannerWorkspaces.Add(workspace);
        AddAudit(
            advertiserId,
            workspace.Id,
            null,
            null,
            WeddingPlannerAuditActions.WorkspaceOpened,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            "Primary workspace opened.");
        await _db.SaveChangesAsync(cancellationToken);
        return ToWorkspaceResult(workspace, false);
    }

    public async Task<IReadOnlyList<WeddingPlannerSessionResult>> ListSessionsAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await RequireWorkspaceAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, actorType, actorLabel, requestId, cancellationToken);
        return await _db.WeddingPlannerPlanningSessions
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new WeddingPlannerSessionResult(
                x.Id,
                x.WorkspaceId,
                x.AdvertiserId,
                x.Status,
                x.SourceSystem,
                x.IdempotencyKey,
                x.CreatedAt,
                x.UpdatedAt,
                x.Messages.Count,
                false))
            .ToListAsync(cancellationToken);
    }

    public async Task<WeddingPlannerSessionResult> GetSessionAsync(
        Guid sessionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.WeddingPlannerPlanningSessions
            .AsNoTracking()
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            throw new WeddingPlannerNotFoundException("Planning session was not found.");
        }

        EnsureAdvertiserAccess(session.AdvertiserId, isChapelStaff, boundAdvertiserId, "session");
        return ToSessionResult(session, false);
    }

    public async Task<WeddingPlannerSessionResult> CreateSessionAsync(
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
        var workspace = await RequireWorkspaceAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, actorType, actorLabel, requestId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);

        var replay = await _db.WeddingPlannerPlanningSessions
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            if (replay.WorkspaceId != workspace.Id || replay.AdvertiserId != workspace.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Planning session was not found.");
            }

            return ToSessionResult(replay, true);
        }

        var now = DateTime.UtcNow;
        var session = new WeddingPlannerPlanningSession
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            AdvertiserId = workspace.AdvertiserId,
            Status = WeddingPlannerStatuses.Open,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.WeddingPlannerPlanningSessions.Add(session);
        AddAudit(
            workspace.AdvertiserId,
            workspace.Id,
            session.Id,
            null,
            WeddingPlannerAuditActions.SessionCreated,
            actorType,
            actorLabel,
            WeddingPlannerOutcomes.Created,
            requestId,
            "Planning session created.");
        await _db.SaveChangesAsync(cancellationToken);
        session.Messages = [];
        return ToSessionResult(session, false);
    }

    public async Task<IReadOnlyList<WeddingPlannerMessageResult>> ListMessagesAsync(
        Guid sessionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(
            sessionId, isChapelStaff, boundAdvertiserId, actorType, actorLabel, requestId, cancellationToken);
        return await _db.WeddingPlannerConversationMessages
            .AsNoTracking()
            .Where(x => x.SessionId == session.Id)
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new WeddingPlannerMessageResult(
                x.Id,
                x.SessionId,
                x.WorkspaceId,
                x.AdvertiserId,
                x.SequenceNumber,
                x.ActorType,
                x.ActorLabel,
                x.Body,
                x.SourceSystem,
                x.IdempotencyKey,
                x.CreatedAt,
                false))
            .ToListAsync(cancellationToken);
    }

    public async Task<WeddingPlannerMessageResult> GetMessageAsync(
        Guid messageId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var message = await _db.WeddingPlannerConversationMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == messageId, cancellationToken);
        if (message is null)
        {
            throw new WeddingPlannerNotFoundException("Conversation message was not found.");
        }

        EnsureAdvertiserAccess(message.AdvertiserId, isChapelStaff, boundAdvertiserId, "message");
        return ToMessageResult(message, false);
    }

    public async Task<WeddingPlannerMessageResult> AppendMessageAsync(
        Guid sessionId,
        string actorTypeRequested,
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
        var session = await RequireSessionAsync(
            sessionId, isChapelStaff, boundAdvertiserId, actorType, actorLabel, requestId, cancellationToken);
        var source = Required(sourceSystem, nameof(sourceSystem), 64);
        var key = Required(idempotencyKey, nameof(idempotencyKey), 128);
        var text = Required(body, nameof(body), 8000);
        var requestedActor = Required(actorTypeRequested, "actorType", 32);
        if (!AllowedMessageActors.Contains(requestedActor))
        {
            throw new InvalidOperationException(
                "Client conversation messages may only use ADVERTISER, OPERATOR, or SYSTEM actors. AI planner messages are out of scope.");
        }

        if (!isChapelStaff && !requestedActor.Equals(WeddingPlannerActorTypes.Advertiser, StringComparison.OrdinalIgnoreCase))
        {
            throw new WeddingPlannerForbiddenException("Advertisers may only append ADVERTISER messages.");
        }

        return await AppendMessageCoreAsync(
            session,
            requestedActor.ToUpperInvariant(),
            actorLabel,
            text,
            source,
            key,
            actorType,
            actorLabel,
            requestId,
            cancellationToken);
    }

    /// <summary>
    /// Server-owned PLANNER append used only by orchestration. Not exposed through the client message API.
    /// </summary>
    internal async Task<WeddingPlannerMessageResult> AppendPlannerMessageForOrchestrationAsync(
        Guid sessionId,
        string body,
        string sourceSystem,
        string idempotencyKey,
        string plannerLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.WeddingPlannerPlanningSessions
            .SingleAsync(x => x.Id == sessionId, cancellationToken);
        return await AppendMessageCoreAsync(
            session,
            WeddingPlannerActorTypes.Planner,
            plannerLabel,
            Required(body, nameof(body), 8000),
            Required(sourceSystem, nameof(sourceSystem), 64),
            Required(idempotencyKey, nameof(idempotencyKey), 128),
            WeddingPlannerActorTypes.System,
            plannerLabel,
            requestId,
            cancellationToken);
    }

    internal async Task<WeddingPlannerMessageResult> AppendClientMessageForOrchestrationAsync(
        Guid sessionId,
        string actorTypeRequested,
        string body,
        string sourceSystem,
        string idempotencyKey,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default) =>
        await AppendMessageAsync(
            sessionId,
            actorTypeRequested,
            body,
            sourceSystem,
            idempotencyKey,
            isChapelStaff,
            boundAdvertiserId,
            actorType,
            actorLabel,
            requestId,
            cancellationToken);

    internal async Task<WeddingPlannerWorkspace> RequireWorkspaceForOrchestrationAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default) =>
        await RequireWorkspaceAsync(workspaceId, isChapelStaff, boundAdvertiserId, "SYSTEM", "orchestration", null, cancellationToken);

    internal async Task<WeddingPlannerPlanningSession> RequireSessionForOrchestrationAsync(
        Guid sessionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default) =>
        await RequireSessionAsync(sessionId, isChapelStaff, boundAdvertiserId, "SYSTEM", "orchestration", null, cancellationToken);

    internal void EnsureAdvertiserAccessForOrchestration(
        Guid advertiserId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string resourceKind) =>
        EnsureAdvertiserAccess(advertiserId, isChapelStaff, boundAdvertiserId, resourceKind);

    internal void AddAuditForOrchestration(
        Guid advertiserId,
        Guid? workspaceId,
        Guid? sessionId,
        Guid? messageId,
        string action,
        string actorType,
        string actorLabel,
        string outcome,
        string? requestId,
        string? detail) =>
        AddAudit(advertiserId, workspaceId, sessionId, messageId, action, actorType, actorLabel, outcome, requestId, detail);

    internal WeddingPlannerMessageResult ToMessageResultForOrchestration(
        WeddingPlannerConversationMessage message,
        bool isReplay) =>
        ToMessageResult(message, isReplay);

    private async Task<WeddingPlannerMessageResult> AppendMessageCoreAsync(
        WeddingPlannerPlanningSession session,
        string actorTypeValue,
        string messageActorLabel,
        string text,
        string source,
        string key,
        string auditActorType,
        string auditActorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var replay = await _db.WeddingPlannerConversationMessages
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            if (replay.SessionId != session.Id || replay.AdvertiserId != session.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException("Conversation message was not found.");
            }

            return ToMessageResult(replay, true);
        }

        var nextSequence = await _db.WeddingPlannerConversationMessages
            .Where(x => x.SessionId == session.Id)
            .Select(x => (int?)x.SequenceNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var now = DateTime.UtcNow;
        var message = new WeddingPlannerConversationMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            WorkspaceId = session.WorkspaceId,
            AdvertiserId = session.AdvertiserId,
            SequenceNumber = nextSequence + 1,
            ActorType = actorTypeValue,
            ActorLabel = messageActorLabel,
            Body = text,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now
        };
        session.UpdatedAt = now;
        _db.WeddingPlannerConversationMessages.Add(message);
        AddAudit(
            session.AdvertiserId,
            session.WorkspaceId,
            session.Id,
            message.Id,
            WeddingPlannerAuditActions.MessageAppended,
            auditActorType,
            auditActorLabel,
            WeddingPlannerOutcomes.Appended,
            requestId,
            $"Sequence {message.SequenceNumber}.");
        await _db.SaveChangesAsync(cancellationToken);
        return ToMessageResult(message, false);
    }

    public async Task<IReadOnlyList<WeddingPlannerAuditResult>> ListAuditAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await RequireWorkspaceAsync(
            workspaceId, isChapelStaff, boundAdvertiserId, actorType, actorLabel, requestId, cancellationToken);
        return await _db.WeddingPlannerAuditEvents
            .AsNoTracking()
            .Where(x => x.WorkspaceId == workspace.Id)
            .OrderBy(x => x.OccurredAt)
            .Select(x => new WeddingPlannerAuditResult(
                x.Id,
                x.AdvertiserId,
                x.WorkspaceId,
                x.SessionId,
                x.MessageId,
                x.Action,
                x.ActorType,
                x.ActorLabel,
                x.Outcome,
                x.RequestId,
                x.Detail,
                x.OccurredAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<WeddingPlannerWorkspace> RequireWorkspaceAsync(
        Guid workspaceId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var workspace = await _db.WeddingPlannerWorkspaces
            .SingleOrDefaultAsync(x => x.Id == workspaceId, cancellationToken);
        if (workspace is null)
        {
            throw new WeddingPlannerNotFoundException("Wedding Planner workspace was not found.");
        }

        EnsureAdvertiserAccess(workspace.AdvertiserId, isChapelStaff, boundAdvertiserId, "workspace");
        return workspace;
    }

    private async Task<WeddingPlannerPlanningSession> RequireSessionAsync(
        Guid sessionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? requestId,
        CancellationToken cancellationToken)
    {
        var session = await _db.WeddingPlannerPlanningSessions
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            throw new WeddingPlannerNotFoundException("Planning session was not found.");
        }

        EnsureAdvertiserAccess(session.AdvertiserId, isChapelStaff, boundAdvertiserId, "session");
        return session;
    }

    private static Guid ResolveTargetAdvertiser(Guid requestedAdvertiserId, bool isChapelStaff, Guid? boundAdvertiserId)
    {
        if (!isChapelStaff)
        {
            if (!boundAdvertiserId.HasValue)
            {
                throw new WeddingPlannerForbiddenException("The authenticated identity is not bound to an advertiser.");
            }

            if (requestedAdvertiserId != Guid.Empty && requestedAdvertiserId != boundAdvertiserId.Value)
            {
                throw new WeddingPlannerForbiddenException("Advertiser A cannot open Advertiser B's Wedding Planner workspace.");
            }

            return boundAdvertiserId.Value;
        }

        if (requestedAdvertiserId == Guid.Empty)
        {
            throw new InvalidOperationException("AdvertiserId is required.");
        }

        return requestedAdvertiserId;
    }

    private static void EnsureAdvertiserAccess(
        Guid advertiserId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string resourceKind)
    {
        if (isChapelStaff || boundAdvertiserId == advertiserId)
        {
            return;
        }

        throw new WeddingPlannerNotFoundException($"{resourceKind} was not found.");
    }

    private void AddAudit(
        Guid advertiserId,
        Guid? workspaceId,
        Guid? sessionId,
        Guid? messageId,
        string action,
        string actorType,
        string actorLabel,
        string outcome,
        string? requestId,
        string? detail)
    {
        _db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent
        {
            Id = Guid.NewGuid(),
            AdvertiserId = advertiserId,
            WorkspaceId = workspaceId,
            SessionId = sessionId,
            MessageId = messageId,
            Action = action,
            ActorType = actorType,
            ActorLabel = actorLabel,
            Outcome = outcome,
            RequestId = requestId,
            Detail = detail,
            OccurredAt = DateTime.UtcNow
        });
    }

    private static WeddingPlannerWorkspaceResult ToWorkspaceResult(WeddingPlannerWorkspace workspace, bool isReplay) =>
        new(
            workspace.Id,
            workspace.AdvertiserId,
            workspace.Advertiser.Name,
            workspace.IsPrimary,
            workspace.Status,
            workspace.SourceSystem,
            workspace.IdempotencyKey,
            workspace.CreatedAt,
            workspace.UpdatedAt,
            isReplay);

    private static WeddingPlannerSessionResult ToSessionResult(WeddingPlannerPlanningSession session, bool isReplay) =>
        new(
            session.Id,
            session.WorkspaceId,
            session.AdvertiserId,
            session.Status,
            session.SourceSystem,
            session.IdempotencyKey,
            session.CreatedAt,
            session.UpdatedAt,
            session.Messages.Count,
            isReplay);

    private static WeddingPlannerMessageResult ToMessageResult(WeddingPlannerConversationMessage message, bool isReplay) =>
        new(
            message.Id,
            message.SessionId,
            message.WorkspaceId,
            message.AdvertiserId,
            message.SequenceNumber,
            message.ActorType,
            message.ActorLabel,
            message.Body,
            message.SourceSystem,
            message.IdempotencyKey,
            message.CreatedAt,
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
}
