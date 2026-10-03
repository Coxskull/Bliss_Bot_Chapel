using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class RotationPeriodService(BlissDbContext database)
{
    public async Task<PeriodBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var names = await database.Advertisers.AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
        var slotCount = await database.AdInventorySlots.CountAsync(cancellationToken);
        var preview = RotationAbundance.Preview(names, slotCount);
        var history = await database.RotationPeriods.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new PeriodBoard(preview, slotCount, names.Count, history);
    }

    public async Task<PeriodWrite> StoreAsync(
        int? pair,
        bool? creatorApproved,
        string? periodKey,
        bool fillOpenSlots,
        decimal? revenueAmount,
        CancellationToken cancellationToken)
    {
        var names = await database.Advertisers.AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
        var slotCount = await database.AdInventorySlots.CountAsync(cancellationToken);
        var stored = await database.RotationPeriods.AsNoTracking().ToListAsync(cancellationToken);
        var existing = stored.Select(item => new PeriodRecord(item.PeriodKey, item.OpenSlots, item.SlotCount, item.CreatorApproved)).ToList();
        var reading = RotationPeriod.Store(pair, names, creatorApproved, slotCount, periodKey, fillOpenSlots, revenueAmount, existing);
        if (reading.Duplicate)
        {
            var prior = stored.First(item => item.PeriodKey == periodKey!.Trim());
            return new PeriodWrite(prior, true, false, slotCount, names.Count);
        }

        var row = new RotationPeriodRow
        {
            Id = Guid.NewGuid(),
            PeriodKey = reading.PeriodKey,
            TheoreticalSlots = reading.TheoreticalSlots,
            PlacedAdvertisers = reading.PlacedAdvertisers,
            OpenSlots = reading.OpenSlots,
            SlotCount = reading.SlotCount,
            CreatorApproved = reading.CreatorApproved,
            RevenueLine = reading.RevenueLine,
            CensusClaimed = false,
            SlotsChanged = false,
            Notice = reading.Notice,
            Delivery = reading.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.RotationPeriods.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        var slotsAfter = await database.AdInventorySlots.CountAsync(cancellationToken);
        var advertisersAfter = await database.Advertisers.CountAsync(cancellationToken);
        return new PeriodWrite(row, false, true, slotsAfter, advertisersAfter);
    }
}

public sealed record PeriodBoard(
    RotationBoard Preview,
    int SlotCount,
    int AdvertiserCount,
    IReadOnlyList<RotationPeriodRow> History);

public sealed record PeriodWrite(
    RotationPeriodRow Period,
    bool Duplicate,
    bool Written,
    int SlotCount,
    int AdvertiserCount);
