using System.Text.Json;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record CampaignPlacementCommand(
    string SourceSystem,
    string IdempotencyKey,
    string OperatorLabel,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId);

public sealed record CampaignPlacementResult(
    Guid RunId,
    Guid CampaignPlacementId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    string SourceSystem,
    string IdempotencyKey,
    string OperatorLabel,
    string Status,
    string Outcome,
    DateTime CompletedAt,
    bool IsReplay);

/// <summary>
/// Records explicit campaign planning intent. It does not reserve slots,
/// schedule, deliver, measure, or settle media.
/// Shared bind core is always no-save/no-transaction for Phase 8 outer commits.
/// </summary>
public sealed class CampaignPlacementService
{
    private readonly BlissDbContext _db;

    public CampaignPlacementService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<CampaignPlacementResult> BindAsync(
        CampaignPlacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(command);
        var replay = await _db.CampaignPlacementRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == normalized.SourceSystem
                    && x.IdempotencyKey == normalized.IdempotencyKey,
                cancellationToken);
        if (replay is not null)
        {
            return ToResult(replay, true);
        }

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var result = await BindCoreInternalAsync(normalized, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Internal bind core: validates graph, tracks PLANNED placement + COMPLETED/PLANNED run.
    /// Never begins a transaction and never SaveChanges — caller owns persistence.
    /// </summary>
    internal async Task<CampaignPlacementResult> BindCoreInternalAsync(
        CampaignPlacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(command);

        var match = await _db.BlissMatches
            .Include(x => x.AdvertiserOpportunity)
            .SingleOrDefaultAsync(x => x.Id == normalized.BlissMatchId, cancellationToken);
        if (match is null)
        {
            throw new InvalidOperationException("BlissMatchId does not reference an existing match.");
        }
        if (!string.Equals(match.Status, EntityStatuses.Approved, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only APPROVED matches can be bound to campaign inventory.");
        }
        if (!string.Equals(match.AdvertiserOpportunity.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The match opportunity must remain ACTIVE.");
        }

        var campaign = await _db.Campaigns
            .SingleOrDefaultAsync(x => x.Id == normalized.CampaignId, cancellationToken);
        if (campaign is null)
        {
            throw new InvalidOperationException("CampaignId does not reference an existing campaign.");
        }
        if (!string.Equals(campaign.Status, EntityStatuses.Draft, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CampaignId must reference a DRAFT campaign.");
        }
        if (campaign.AdvertiserOpportunityId is { } campaignOpportunityId
            && campaignOpportunityId != match.AdvertiserOpportunityId)
        {
            throw new InvalidOperationException("Campaign opportunity does not match the approved match opportunity.");
        }

        var content = await _db.ContentItems
            .SingleOrDefaultAsync(x => x.Id == normalized.ContentItemId, cancellationToken);
        if (content is null)
        {
            throw new InvalidOperationException("ContentItemId does not reference existing content.");
        }
        if (content.CreatorId != match.CreatorId)
        {
            throw new InvalidOperationException("ContentItemId must belong to the approved match creator.");
        }

        var slot = await _db.AdInventorySlots
            .SingleOrDefaultAsync(x => x.Id == normalized.AdInventorySlotId, cancellationToken);
        if (slot is null)
        {
            throw new InvalidOperationException("AdInventorySlotId does not reference an existing slot.");
        }
        if (slot.ContentItemId != content.Id)
        {
            throw new InvalidOperationException("AdInventorySlotId must belong to the supplied content item.");
        }

        var startedAt = DateTime.UtcNow;
        campaign.AdvertiserOpportunityId ??= match.AdvertiserOpportunityId;
        var placement = new CampaignPlacement
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            ContentItemId = content.Id,
            AdInventorySlotId = slot.Id,
            BlissMatchId = match.Id,
            Status = EntityStatuses.Planned
        };
        var run = new CampaignPlacementRun
        {
            Id = Guid.NewGuid(),
            CampaignPlacementId = placement.Id,
            BlissMatchId = match.Id,
            CampaignId = campaign.Id,
            CreatorId = match.CreatorId,
            AdvertiserOpportunityId = match.AdvertiserOpportunityId,
            ContentItemId = content.Id,
            AdInventorySlotId = slot.Id,
            SourceSystem = normalized.SourceSystem,
            IdempotencyKey = normalized.IdempotencyKey,
            OperatorLabel = normalized.OperatorLabel,
            Status = EntityStatuses.Completed,
            Outcome = EntityStatuses.Planned,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            InputSnapshot = JsonSerializer.Serialize(normalized, SnapshotOptions)
        };
        _db.CampaignPlacements.Add(placement);
        _db.CampaignPlacementRuns.Add(run);

        return ToResult(run, false);
    }

    private static CampaignPlacementCommand Normalize(CampaignPlacementCommand command)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var operatorLabel = Required(command.OperatorLabel, nameof(command.OperatorLabel), 128);
        if (command.BlissMatchId == Guid.Empty) throw new InvalidOperationException("BlissMatchId is required.");
        if (command.CampaignId == Guid.Empty) throw new InvalidOperationException("CampaignId is required.");
        if (command.ContentItemId == Guid.Empty) throw new InvalidOperationException("ContentItemId is required.");
        if (command.AdInventorySlotId == Guid.Empty) throw new InvalidOperationException("AdInventorySlotId is required.");
        return command with
        {
            SourceSystem = source,
            IdempotencyKey = key,
            OperatorLabel = operatorLabel
        };
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) throw new InvalidOperationException($"{name} is required.");
        if (trimmed.Length > maxLength) throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        return trimmed;
    }

    private static CampaignPlacementResult ToResult(CampaignPlacementRun run, bool isReplay) => new(
        run.Id,
        run.CampaignPlacementId,
        run.BlissMatchId,
        run.CampaignId,
        run.ContentItemId,
        run.AdInventorySlotId,
        run.SourceSystem,
        run.IdempotencyKey,
        run.OperatorLabel,
        run.Status,
        run.Outcome,
        run.CompletedAt,
        isReplay);

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
