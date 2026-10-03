using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class ClosedModelsService(BlissDbContext database)
{
    public async Task<ModelBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var snapshot = await ReadSnapshotAsync(cancellationToken);
        var history = await database.ClosedModelReadings.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new ModelBoard(snapshot, history);
    }

    public async Task<ModelWrite> StoreAsync(
        string? readingKey,
        bool configureModel,
        int? engagementCount,
        bool authorizedTraffic,
        CancellationToken cancellationToken)
    {
        var snapshot = await ReadSnapshotAsync(cancellationToken);
        var stored = await database.ClosedModelReadings.AsNoTracking().ToListAsync(cancellationToken);
        var existing = stored.Select(item => new ModelRecord(
            item.ReadingKey,
            item.ModelCalls,
            item.NoteCount,
            item.PassedCount,
            item.ScenarioCount)).ToList();
        var decision = ClosedModels.Store(
            readingKey,
            snapshot.ModelCalls,
            snapshot.NoteCount,
            snapshot.LaboratoryPassed,
            snapshot.PassedCount,
            snapshot.ScenarioCount,
            configureModel,
            engagementCount,
            authorizedTraffic,
            existing);
        if (decision.Duplicate)
        {
            var prior = stored.First(item => item.ReadingKey == decision.ReadingKey);
            var after = await ReadSnapshotAsync(cancellationToken);
            return new ModelWrite(prior, true, false, after);
        }

        var row = new ClosedModelRow
        {
            Id = Guid.NewGuid(),
            ReadingKey = decision.ReadingKey,
            ConfiguredModels = 0,
            ModelCalls = decision.ModelCalls,
            NoteCount = decision.NoteCount,
            LaboratoryPassed = decision.LaboratoryPassed,
            PassedCount = decision.PassedCount,
            ScenarioCount = decision.ScenarioCount,
            ModelsConfigured = false,
            AuthorizedTraffic = false,
            ProductionChanged = false,
            Notice = decision.Notice,
            Delivery = decision.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.ClosedModelReadings.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        var unchanged = await ReadSnapshotAsync(cancellationToken);
        return new ModelWrite(row, false, true, unchanged);
    }

    private async Task<ModelSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var notes = await database.LearningNotes.AsNoTracking().ToListAsync(cancellationToken);
        var modelCalls = notes.Sum(item => item.ModelCalls);
        var laboratory = ConversationLaboratory.Run();
        return ClosedModels.Read(modelCalls, notes.Count, laboratory.Passed, laboratory.PassedCount, laboratory.ScenarioCount);
    }
}

public sealed record ModelBoard(ModelSnapshot Snapshot, IReadOnlyList<ClosedModelRow> History);

public sealed record ModelWrite(ClosedModelRow Reading, bool Duplicate, bool Written, ModelSnapshot Snapshot);
