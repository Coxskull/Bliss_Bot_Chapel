using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class MarketplaceMetricsService(BlissDbContext database)
{
    public async Task<MetricBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await ReadSnapshotAsync(cancellationToken);
        var history = await database.MarketplaceMetricReadings.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new MetricBoard(snapshot, history);
    }

    public async Task<MetricWrite> StoreAsync(
        string? metricKey,
        decimal? revenueAmount,
        bool addSlot,
        CancellationToken cancellationToken)
    {
        var snapshot = await ReadSnapshotAsync(cancellationToken);
        var stored = await database.MarketplaceMetricReadings.AsNoTracking().ToListAsync(cancellationToken);
        var existing = stored.Select(item => new MetricRecord(
            item.MetricKey,
            item.AdvertiserCount,
            item.CreatorCount,
            item.SlotCount,
            item.RevenueRowCount)).ToList();
        var decision = MarketplaceMetrics.Store(
            metricKey,
            snapshot.Advertisers,
            snapshot.Creators,
            snapshot.SlotCount,
            snapshot.RevenueRowCount,
            revenueAmount,
            addSlot,
            existing);
        if (decision.Duplicate)
        {
            var prior = stored.First(item => item.MetricKey == decision.MetricKey);
            var after = await ReadSnapshotAsync(cancellationToken);
            return new MetricWrite(prior, true, false, after);
        }

        var row = new MarketplaceMetricRow
        {
            Id = Guid.NewGuid(),
            MetricKey = decision.MetricKey,
            AdvertiserCount = decision.AdvertiserCount,
            CreatorCount = decision.CreatorCount,
            SlotCount = decision.SlotCount,
            RevenueRowCount = decision.RevenueRowCount,
            Pressure = decision.Pressure,
            RevenueLine = decision.RevenueLine,
            CensusClaimed = false,
            RevenueRecorded = false,
            SlotsChanged = false,
            Notice = decision.Notice,
            Delivery = decision.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.MarketplaceMetricReadings.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        var unchanged = await ReadSnapshotAsync(cancellationToken);
        return new MetricWrite(row, false, true, unchanged);
    }

    private async Task<MetricSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var advertisers = await database.Advertisers.AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
        var creators = await database.Creators.AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
        var slotCount = await database.AdInventorySlots.CountAsync(cancellationToken);
        var placementRows = await database.HistoricalPlacementEconomics.CountAsync(cancellationToken);
        var campaignRows = await database.CampaignPerformanceEconomics.CountAsync(cancellationToken);
        return MarketplaceMetrics.Read(advertisers, creators, slotCount, placementRows + campaignRows);
    }
}

public sealed record MetricBoard(MetricSnapshot Snapshot, IReadOnlyList<MarketplaceMetricRow> History);

public sealed record MetricWrite(MarketplaceMetricRow Reading, bool Duplicate, bool Written, MetricSnapshot Snapshot);
