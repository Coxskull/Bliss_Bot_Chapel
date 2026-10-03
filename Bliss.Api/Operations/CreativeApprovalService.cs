using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class CreativeApprovalService(BlissDbContext database)
{
    public async Task<CreativeBoard> ReadAsync(Guid? workspaceId, CancellationToken cancellationToken)
    {
        var workspaces = await database.WeddingPlannerWorkspaces.AsNoTracking()
            .OrderBy(item => item.CreatedAt)
            .Select(item => new CreativeWorkspace(item.Id, item.AdvertiserId, item.Advertiser.Name))
            .ToListAsync(cancellationToken);
        if (workspaceId is null || workspaceId == Guid.Empty)
        {
            return new CreativeBoard(null, workspaces, [], [], []);
        }

        var workspace = workspaces.FirstOrDefault(item => item.Id == workspaceId.Value);
        if (workspace is null)
        {
            throw new InvalidOperationException("An open workspace is required. A discovered business is not opened.");
        }

        var decisions = await database.CreativeApprovals.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.Id)
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        var messages = await database.WeddingPlannerConversationMessages.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.Id)
            .OrderBy(item => item.SequenceNumber)
            .Select(item => new CreativeMessage(item.WorkspaceId, item.ActorType, item.Body))
            .ToListAsync(cancellationToken);
        var audits = await database.WeddingPlannerAuditEvents.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.Id && item.Action == WeddingPlannerAuditActions.CreativeDecided)
            .OrderBy(item => item.OccurredAt)
            .Select(item => new CreativeAudit(item.WorkspaceId, item.Action, item.ActorType, item.Outcome))
            .ToListAsync(cancellationToken);
        return new CreativeBoard(workspace, workspaces, decisions, messages, audits);
    }

    public async Task<CreativeWrite> DecideAsync(
        Guid workspaceId,
        string? title,
        string? decision,
        string? actorType,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var workspace = await database.WeddingPlannerWorkspaces.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == workspaceId, cancellationToken);
        if (workspace is null)
        {
            throw new InvalidOperationException("An open workspace is required. A discovered business is not opened.");
        }

        var stored = await database.CreativeApprovals.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.Id)
            .ToListAsync(cancellationToken);
        var existing = stored.Select(item => new CreativeRecord(item.WorkspaceId, item.IdempotencyKey, item.Status, item.Title)).ToList();
        var reading = CreativeApproval.Decide(workspace.Id, title, decision, actorType, idempotencyKey, existing);
        if (reading.Duplicate)
        {
            var prior = stored.First(item => item.IdempotencyKey == idempotencyKey!.Trim());
            return new CreativeWrite(prior, true, false);
        }

        var row = new CreativeApprovalRow
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            AdvertiserId = workspace.AdvertiserId,
            Title = reading.Title,
            Status = reading.Status,
            ActorType = reading.ActorType,
            IdempotencyKey = idempotencyKey!.Trim(),
            CampaignReady = false,
            MatchWritten = false,
            PriceInvented = false,
            ModelCalls = 0,
            Notice = reading.Notice,
            Delivery = reading.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.CreativeApprovals.Add(row);
        database.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent
        {
            Id = Guid.NewGuid(),
            AdvertiserId = workspace.AdvertiserId,
            WorkspaceId = workspace.Id,
            Action = WeddingPlannerAuditActions.CreativeDecided,
            ActorType = reading.ActorType,
            ActorLabel = reading.ActorType == "ADVERTISER" ? "Advertiser" : "Operator",
            Outcome = reading.Status,
            Detail = reading.Notice,
            OccurredAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return new CreativeWrite(row, false, true);
    }
}

public sealed record CreativeWorkspace(Guid Id, Guid AdvertiserId, string AdvertiserName);

public sealed record CreativeMessage(Guid WorkspaceId, string ActorType, string Body);

public sealed record CreativeAudit(Guid? WorkspaceId, string Action, string ActorType, string Outcome);

public sealed record CreativeBoard(
    CreativeWorkspace? Workspace,
    IReadOnlyList<CreativeWorkspace> Workspaces,
    IReadOnlyList<CreativeApprovalRow> Decisions,
    IReadOnlyList<CreativeMessage> Messages,
    IReadOnlyList<CreativeAudit> Audits);

public sealed record CreativeWrite(CreativeApprovalRow Decision, bool Duplicate, bool Written);
