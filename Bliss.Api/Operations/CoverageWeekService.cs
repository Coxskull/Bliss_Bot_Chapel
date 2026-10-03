using Bliss.Api.Demonstrations;
using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class CoverageWeekService(BlissDbContext database, ProspectDemonstrationService demonstrations)
{
    public async Task<WeekBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var reading = ReadLibrary();
        var history = await database.CoverageWeeks.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new WeekBoard(reading, history);
    }

    public async Task<WeekWrite> StoreAsync(string? weekKey, bool addMissingMarket, CancellationToken cancellationToken)
    {
        var reading = ReadLibrary();
        var stored = await database.CoverageWeeks.AsNoTracking().ToListAsync(cancellationToken);
        var existing = stored.Select(item => new WeekRecord(item.WeekKey, item.QualifiedSlices, item.MarketCount, item.FuelStatus)).ToList();
        var names = reading.Markets.Select(item => item.Market).ToList();
        var decision = CoverageWeek.Store(
            weekKey,
            reading.Fuel.QualifiedUnique,
            reading.Fuel.FuelStatus,
            names,
            addMissingMarket,
            existing);
        if (decision.Duplicate)
        {
            var prior = stored.First(item => item.WeekKey == decision.WeekKey);
            return new WeekWrite(prior, true, false, reading);
        }

        var row = new CoverageWeekRow
        {
            Id = Guid.NewGuid(),
            WeekKey = decision.WeekKey,
            QualifiedSlices = decision.QualifiedSlices,
            MarketCount = decision.MarketCount,
            MarketLine = string.Join(", ", decision.Markets),
            FuelStatus = decision.FuelStatus,
            CensusClaimed = false,
            SlicesChanged = false,
            Notice = decision.Notice,
            Delivery = decision.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.CoverageWeeks.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        var after = ReadLibrary();
        return new WeekWrite(row, false, true, after);
    }

    private CoverageReading ReadLibrary()
    {
        var library = demonstrations.Library();
        return SourceMediaCoverage.Read(library.Clips.Select(clip => new StoredSlice(
            clip.Market,
            clip.Country,
            clip.Status,
            clip.QuotaCredit,
            clip.Sha256,
            clip.FrameHash,
            clip.Provenance)));
    }
}

public sealed record WeekBoard(CoverageReading Reading, IReadOnlyList<CoverageWeekRow> History);

public sealed record WeekWrite(CoverageWeekRow Week, bool Duplicate, bool Written, CoverageReading Reading);
