using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class EconomicsPhaseAcceptanceService(BlissDbContext database)
{
    public async Task<AcceptanceBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var history = await CiteAsync(cancellationToken);
        var row = await database.EconomicsPhaseAcceptances.AsNoTracking()
            .SingleOrDefaultAsync(item => item.PhaseKey == EconomicsPhaseAcceptance.PhaseKey, cancellationToken);
        return new AcceptanceBoard(row, history.PlacementCount, history.CampaignCount, history.Line, history.RecommendationMatches);
    }

    public async Task<AcceptanceWrite> AcceptAsync(
        int? phase,
        bool reprice,
        bool settle,
        CancellationToken cancellationToken)
    {
        var history = await CiteAsync(cancellationToken);
        var stored = await database.EconomicsPhaseAcceptances.AsNoTracking().ToListAsync(cancellationToken);
        var existing = stored.Select(item => new AcceptanceRecord(item.PhaseKey)).ToList();
        var reading = EconomicsPhaseAcceptance.Accept(
            phase,
            reprice,
            settle,
            history.PlacementCount,
            history.CampaignCount,
            history.ContractedAmount,
            history.Currency,
            existing);
        if (reading.Duplicate)
        {
            var prior = stored.First(item => item.PhaseKey == EconomicsPhaseAcceptance.PhaseKey);
            return new AcceptanceWrite(prior, true, false, history.PlacementCount, history.CampaignCount, history.Line, history.RecommendationMatches);
        }

        var row = new EconomicsPhaseAcceptanceRow
        {
            Id = Guid.NewGuid(),
            PhaseKey = reading.PhaseKey,
            Phase = reading.Phase,
            HistoryLine = reading.HistoryLine,
            RepricingAuthorized = false,
            SettlementAuthorized = false,
            RecommendationRewritten = false,
            Notice = reading.Notice,
            Delivery = reading.Delivery,
            AcceptedAt = DateTime.UtcNow
        };
        database.EconomicsPhaseAcceptances.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        var again = await CiteAsync(cancellationToken);
        return new AcceptanceWrite(row, false, true, again.PlacementCount, again.CampaignCount, again.Line, again.RecommendationMatches);
    }

    private async Task<HistoryCitation> CiteAsync(CancellationToken cancellationToken)
    {
        var placementCount = await database.HistoricalPlacementEconomics.CountAsync(cancellationToken);
        var campaignCount = await database.CampaignPerformanceEconomics.CountAsync(cancellationToken);
        if (placementCount == 0 && campaignCount == 0)
        {
            return new HistoryCitation(0, 0, null, null, EconomicsPhaseAcceptance.Unrecorded, true);
        }

        var latest = await database.HistoricalPlacementEconomics.AsNoTracking()
            .OrderByDescending(item => item.RecordedAt)
            .Select(item => new
            {
                item.ContractedAmount,
                item.CurrencyCode,
                item.RateRecommendationId,
                item.RecommendationTarget
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
        {
            var campaign = await database.CampaignPerformanceEconomics.AsNoTracking()
                .OrderByDescending(item => item.RecordedAt)
                .Select(item => new { item.AlphaContractedAmount, item.AlphaContractedCurrencyCode })
                .FirstAsync(cancellationToken);
            if (campaign.AlphaContractedAmount is null || string.IsNullOrWhiteSpace(campaign.AlphaContractedCurrencyCode))
            {
                return new HistoryCitation(placementCount, campaignCount, null, null, EconomicsPhaseAcceptance.Unrecorded, true);
            }

            var line = EconomicsPhaseAcceptance.Cite(
                placementCount,
                campaignCount,
                campaign.AlphaContractedAmount,
                campaign.AlphaContractedCurrencyCode);
            return new HistoryCitation(
                placementCount,
                campaignCount,
                campaign.AlphaContractedAmount,
                campaign.AlphaContractedCurrencyCode,
                line,
                true);
        }

        var current = await database.RateRecommendations.AsNoTracking()
            .Where(item => item.Id == latest.RateRecommendationId)
            .Select(item => item.RangeTarget)
            .SingleAsync(cancellationToken);
        var cited = EconomicsPhaseAcceptance.Cite(placementCount, campaignCount, latest.ContractedAmount, latest.CurrencyCode);
        return new HistoryCitation(
            placementCount,
            campaignCount,
            latest.ContractedAmount,
            latest.CurrencyCode,
            cited,
            current == latest.RecommendationTarget);
    }
}

public sealed record HistoryCitation(
    int PlacementCount,
    int CampaignCount,
    decimal? ContractedAmount,
    string? Currency,
    string Line,
    bool RecommendationMatches);

public sealed record AcceptanceBoard(
    EconomicsPhaseAcceptanceRow? Acceptance,
    int PlacementCount,
    int CampaignCount,
    string HistoryLine,
    bool RecommendationMatches);

public sealed record AcceptanceWrite(
    EconomicsPhaseAcceptanceRow Acceptance,
    bool Duplicate,
    bool Written,
    int PlacementCount,
    int CampaignCount,
    string HistoryLine,
    bool RecommendationMatches);
