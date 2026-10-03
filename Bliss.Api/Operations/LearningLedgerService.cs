using Bliss.Api.Demonstrations;
using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class LearningLedgerService(ProspectDemonstrationService demonstrations, BlissDbContext database)
{
    public async Task<LearningBoard> ReadAsync(string? prospectSlug, CancellationToken cancellationToken)
    {
        var report = ConversationLaboratory.Run();
        var library = demonstrations.Library().Demonstrations
            .OrderBy(item => item.BusinessName)
            .Select(item => new LearningProspect(item.Slug, item.BusinessName, item.ProspectState))
            .ToList();
        var slug = (prospectSlug ?? string.Empty).Trim();
        var match = library.FirstOrDefault(item => string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase));
        var notes = match is null
            ? new List<LearningNoteRow>()
            : await database.LearningNotes.AsNoTracking()
                .Where(item => item.ProspectSlug == match.Slug)
                .OrderBy(item => item.RecordedAt)
                .ToListAsync(cancellationToken);
        return new LearningBoard(report, library, match?.Slug ?? string.Empty, notes);
    }

    public async Task<LearningWrite> AppendAsync(
        string? prospectSlug,
        bool applyToProduction,
        bool authorizedTraffic,
        int? engagementCount,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var slug = (prospectSlug ?? string.Empty).Trim();
        var prospect = demonstrations.Library().Demonstrations
            .FirstOrDefault(item => string.Equals(item.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (prospect is null)
        {
            throw new InvalidOperationException("A stored prospect is required. None was invented.");
        }

        var report = ConversationLaboratory.Run();
        var stored = await database.LearningNotes.AsNoTracking()
            .Where(item => item.ProspectSlug == prospect.Slug)
            .ToListAsync(cancellationToken);
        var existing = stored.Select(item => new LearningRecord(item.ProspectSlug, item.IdempotencyKey, item.Body)).ToList();
        var note = LearningLedger.Append(
            prospect.Slug,
            report.Passed,
            applyToProduction,
            authorizedTraffic,
            engagementCount,
            idempotencyKey,
            existing);
        if (note.Duplicate)
        {
            var prior = stored.First(item => item.IdempotencyKey == idempotencyKey!.Trim());
            return new LearningWrite(prior, true, false, prospect.ProspectState);
        }

        var row = new LearningNoteRow
        {
            Id = Guid.NewGuid(),
            ProspectSlug = prospect.Slug,
            IdempotencyKey = idempotencyKey!.Trim(),
            Body = note.Body,
            LaboratoryGraduated = note.LaboratoryGraduated,
            AuthorizedTraffic = false,
            ProductionChanged = false,
            BehaviorChanged = false,
            ModelCalls = 0,
            Notice = note.Notice,
            Delivery = note.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.LearningNotes.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        var after = demonstrations.Library().Demonstrations
            .First(item => item.Slug == prospect.Slug);
        return new LearningWrite(row, false, true, after.ProspectState);
    }
}

public sealed record LearningProspect(string Slug, string BusinessName, string ProspectState);

public sealed record LearningBoard(
    ConversationLaboratoryReport Laboratory,
    IReadOnlyList<LearningProspect> Prospects,
    string ProspectSlug,
    IReadOnlyList<LearningNoteRow> Notes);

public sealed record LearningWrite(LearningNoteRow Note, bool Duplicate, bool Written, string ProspectState);
