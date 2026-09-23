using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record RequestWeddingPlannerRecommendationCommand(
    Guid BlissMatchId,
    Guid AdInventorySlotId,
    Guid GeographicMarketId,
    string PricingModelCode,
    int? DurationSeconds,
    string? IndustryCategory,
    string? CampaignObjective,
    string RequestedBy,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerEconomicsRequestResult(
    WeddingPlannerEconomicsRequest Request,
    bool IsReplay);

/// <summary>
/// Tenant-safe orchestration only. All pricing remains inside
/// <see cref="RateRecommendationService"/>.
/// </summary>
public sealed class WeddingPlannerEconomicsHandshakeService
{
    private readonly BlissDbContext _db;
    private readonly RateRecommendationService _recommendations;

    public WeddingPlannerEconomicsHandshakeService(
        BlissDbContext db,
        RateRecommendationService recommendations)
    {
        _db = db;
        _recommendations = recommendations;
    }

    public async Task<IReadOnlyList<WeddingPlannerEconomicsRequestResult>> ListAsync(
        Guid sessionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(
            sessionId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var requests = await RequestGraph()
            .Where(x => x.SessionId == session.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return requests.Select(x => new WeddingPlannerEconomicsRequestResult(x, false)).ToList();
    }

    public async Task<WeddingPlannerEconomicsRequestResult> GetAsync(
        Guid requestId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken = default)
    {
        var request = await RequestGraph()
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException(
                "Wedding Planner economics request was not found.");
        EnsureAdvertiserAccess(request.AdvertiserId, isChapelStaff, boundAdvertiserId);
        return new(request, false);
    }

    public async Task<WeddingPlannerEconomicsRequestResult> RequestAsync(
        Guid sessionId,
        RequestWeddingPlannerRecommendationCommand command,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        string actorType,
        string actorLabel,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(
            sessionId, isChapelStaff, boundAdvertiserId, cancellationToken);
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64);
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var requestedBy = Required(command.RequestedBy, nameof(command.RequestedBy), 128);

        var replay = await RequestGraph()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            if (replay.SessionId != session.Id || replay.AdvertiserId != session.AdvertiserId)
            {
                throw new WeddingPlannerNotFoundException(
                    "Wedding Planner economics request was not found.");
            }

            return new(replay, true);
        }

        var match = await _db.BlissMatches
            .AsNoTracking()
            .Include(x => x.AdvertiserOpportunity)
                .ThenInclude(x => x.AdvertiserProgram)
            .SingleOrDefaultAsync(x => x.Id == command.BlissMatchId, cancellationToken)
            ?? throw new InvalidOperationException("BlissMatchId does not reference a match.");
        if (match.Status != EntityStatuses.Approved)
        {
            throw new InvalidOperationException(
                "Wedding Planner can request Economics only for an APPROVED Bliss match.");
        }
        if (match.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId != session.AdvertiserId)
        {
            throw new WeddingPlannerNotFoundException(
                "The selected match is not available in this advertiser workspace.");
        }

        var slot = await _db.AdInventorySlots
            .AsNoTracking()
            .Include(x => x.ContentItem)
            .SingleOrDefaultAsync(x => x.Id == command.AdInventorySlotId, cancellationToken)
            ?? throw new InvalidOperationException(
                "AdInventorySlotId does not reference inventory.");
        if (slot.ContentItem.CreatorId != match.CreatorId)
        {
            throw new InvalidOperationException(
                "Inventory does not belong to the creator in the approved match.");
        }
        if (!slot.IsAvailable)
        {
            throw new InvalidOperationException(
                "Wedding Planner cannot request pricing for unavailable inventory.");
        }

        var market = await _db.GeographicMarkets.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == command.GeographicMarketId && x.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "GeographicMarketId does not reference an active market.");
        var pricingCode = Required(
            command.PricingModelCode, nameof(command.PricingModelCode), 32).ToUpperInvariant();
        var pricingModel = await _db.PricingModels.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == pricingCode && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException(
                "PricingModelCode does not reference an active pricing model.");

        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var recommendationResult = await _recommendations.GenerateAsync(
            new GenerateRateRecommendationCommand(
                match.CreatorId,
                slot.Id,
                market.Id,
                pricingModel.Code,
                command.DurationSeconds,
                match.AdvertiserOpportunityId,
                match.Id,
                Trim(command.IndustryCategory, 128)
                    ?? match.AdvertiserOpportunity.Category,
                Trim(command.CampaignObjective, 256),
                source,
                key),
            cancellationToken);

        var now = DateTime.UtcNow;
        var request = new WeddingPlannerEconomicsRequest
        {
            Id = Guid.NewGuid(),
            WorkspaceId = session.WorkspaceId,
            SessionId = session.Id,
            AdvertiserId = session.AdvertiserId,
            BlissMatchId = match.Id,
            AdInventorySlotId = slot.Id,
            GeographicMarketId = market.Id,
            PricingModelId = pricingModel.Id,
            RateRecommendationId = recommendationResult.Recommendation.Id,
            RequestedDurationSeconds = command.DurationSeconds ?? slot.DurationSeconds,
            IndustryCategory = Trim(command.IndustryCategory, 128)
                ?? match.AdvertiserOpportunity.Category,
            CampaignObjective = Trim(command.CampaignObjective, 256),
            Status = WeddingPlannerStatuses.RecommendationReady,
            RequestedBy = requestedBy,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now
        };
        session.UpdatedAt = now;
        _db.WeddingPlannerEconomicsRequests.Add(request);
        _db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent
        {
            Id = Guid.NewGuid(),
            AdvertiserId = session.AdvertiserId,
            WorkspaceId = session.WorkspaceId,
            SessionId = session.Id,
            Action = WeddingPlannerAuditActions.EconomicsRecommendationRequested,
            ActorType = actorType,
            ActorLabel = actorLabel,
            Outcome = WeddingPlannerOutcomes.RecommendationReady,
            RequestId = correlationId,
            Detail =
                $"Economics request {request.Id}; recommendation {request.RateRecommendationId}.",
            OccurredAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new(
            await RequestGraph().SingleAsync(x => x.Id == request.Id, cancellationToken),
            false);
    }

    public IQueryable<WeddingPlannerEconomicsRequest> RequestGraph() =>
        _db.WeddingPlannerEconomicsRequests
            .Include(x => x.Workspace)
            .Include(x => x.Session)
            .Include(x => x.Advertiser)
            .Include(x => x.BlissMatch)
            .Include(x => x.AdInventorySlot).ThenInclude(x => x.ContentItem)
            .Include(x => x.GeographicMarket)
            .Include(x => x.PricingModel)
            .Include(x => x.RateRecommendation)
                .ThenInclude(x => x.Creator)
            .Include(x => x.RateRecommendation)
                .ThenInclude(x => x.AdInventorySlot)
            .Include(x => x.RateRecommendation)
                .ThenInclude(x => x.GeographicMarket)
            .Include(x => x.RateRecommendation)
                .ThenInclude(x => x.PricingModel)
            .Include(x => x.RateRecommendation)
                .ThenInclude(x => x.PricingRuleVersion)
            .Include(x => x.RateRecommendation)
                .ThenInclude(x => x.Factors)
            .Include(x => x.RateRecommendation)
                .ThenInclude(x => x.Sources)
                    .ThenInclude(x => x.ResearchSource)
            .AsSplitQuery();

    private async Task<WeddingPlannerPlanningSession> RequireSessionAsync(
        Guid sessionId,
        bool isChapelStaff,
        Guid? boundAdvertiserId,
        CancellationToken cancellationToken)
    {
        var session = await _db.WeddingPlannerPlanningSessions
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new WeddingPlannerNotFoundException("Planning session was not found.");
        EnsureAdvertiserAccess(session.AdvertiserId, isChapelStaff, boundAdvertiserId);
        return session;
    }

    private static void EnsureAdvertiserAccess(
        Guid advertiserId,
        bool isChapelStaff,
        Guid? boundAdvertiserId)
    {
        if (!isChapelStaff && boundAdvertiserId != advertiserId)
        {
            throw new WeddingPlannerNotFoundException(
                "Wedding Planner economics request was not found.");
        }
    }

    private static string Required(string? value, string field, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > max)
        {
            throw new InvalidOperationException(
                $"{field} is required and must be at most {max} characters.");
        }

        return result;
    }

    private static string? Trim(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) return null;
        if (result.Length > max)
        {
            throw new InvalidOperationException($"Value must be at most {max} characters.");
        }

        return result;
    }
}
