using System.Diagnostics;
using Bliss.Api.Demonstrations;
using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class BatchMeasurementService(ProspectDemonstrationService demonstrations, BlissDbContext database)
{
    public async Task<MeasurementBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var rows = await database.BatchMeasurements.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new MeasurementBoard(rows);
    }

    public async Task<MeasurementWrite> MeasureAsync(string? idempotencyKey, int attempt, CancellationToken cancellationToken)
    {
        BatchMeasurement.RequireKey(idempotencyKey);
        var key = idempotencyKey!.Trim();
        var existing = await database.BatchMeasurements.AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken);
        if (existing is not null)
        {
            return new MeasurementWrite(existing, true, false);
        }

        var watch = Stopwatch.StartNew();
        var library = demonstrations.Library();
        watch.Stop();
        var workingSet = Process.GetCurrentProcess().WorkingSet64;
        var reading = BatchMeasurement.Read(
            library.Demonstrations.Select(item => new MeasuredProspect(
                item.BusinessName,
                (item.Messages ?? []).Select(message => message.Text).ToList())).ToList(),
            watch.ElapsedMilliseconds,
            workingSet,
            attempt);
        var failures = string.Join("\n", reading.Failures);
        if (failures.Length > 2000)
        {
            failures = failures.Substring(0, 2000);
        }

        var row = new BatchMeasurementRow
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = key,
            StoredProspects = reading.StoredProspects,
            ElapsedMilliseconds = reading.ElapsedMilliseconds,
            WorkingSetBytes = workingSet > 0 ? workingSet : null,
            ResourceLine = reading.ResourceLine,
            CostLine = reading.CostLine,
            Retries = reading.Retries,
            PartialFailures = reading.PartialFailures,
            Recovered = reading.Recovered,
            Leakage = reading.Leakage,
            HostedAcceptanceClaimed = false,
            FactoryTargetClaimed = false,
            CensusClaimed = false,
            Notice = reading.Notice,
            Failures = failures,
            Delivery = reading.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.BatchMeasurements.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return new MeasurementWrite(row, false, true);
    }
}

public sealed record MeasurementBoard(IReadOnlyList<BatchMeasurementRow> Measurements);

public sealed record MeasurementWrite(BatchMeasurementRow Measurement, bool Duplicate, bool Written);
