using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class CreativeAcademyService(BlissDbContext database)
{
    public async Task<AcademyBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var lessons = await database.CreativeAcademyLessons.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        var dna = await database.CreativeAcademyDna.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new AcademyBoard(lessons, dna);
    }

    public async Task<AcademyWrite> StoreCurriculumAsync(string? curriculumKey, CancellationToken cancellationToken)
    {
        var key = RequireKey(curriculumKey);
        if (!string.Equals(key, "patisserie-curriculum-1", StringComparison.Ordinal)
            && !string.Equals(key, "creative-academy-2", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A known Creative Academy curriculum key is required. None was invented.");
        }

        var existing = await database.CreativeAcademyLessons.AsNoTracking()
            .Select(item => item.LessonKey)
            .ToListAsync(cancellationToken);
        var existingKeys = new HashSet<string>(existing, StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var written = 0;
        foreach (var lesson in CreativeAcademy.AllLessons)
        {
            if (existingKeys.Contains(lesson.LessonKey))
            {
                continue;
            }

            var inspection = CreativeAcademy.Judge(
                lesson.Role,
                lesson.BrandName,
                lesson.Headline,
                lesson.Body,
                lesson.ProperNouns,
                false,
                null,
                null,
                false,
                false,
                null);
            database.CreativeAcademyLessons.Add(new CreativeAcademyLessonRow
            {
                Id = Guid.NewGuid(),
                LessonKey = lesson.LessonKey,
                Role = lesson.Role,
                Family = lesson.Family,
                BrandName = lesson.BrandName,
                Headline = lesson.Headline,
                Body = lesson.Body,
                ProperNouns = lesson.ProperNouns,
                ImagePath = lesson.ImagePath,
                Status = inspection.Status,
                Score = inspection.Score,
                Critical = inspection.Critical,
                VisualRecorded = inspection.VisualRecorded,
                Defects = Join(inspection.Defects),
                PreserveList = Join(inspection.Preserve),
                RepairList = Join(inspection.Repair),
                ModelCalls = 0,
                CampaignReady = false,
                Notice = inspection.Notice,
                Delivery = "NOT_SENT",
                RecordedAt = now
            });
            written++;
        }

        if (written > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        return new AcademyWrite(written == 0, written > 0, CreativeAcademy.Notice);
    }

    public async Task<AcademyWrite> StoreDnaAsync(
        string? dnaKey,
        string? family,
        string? brandName,
        string? hero,
        string? palette,
        string? cta,
        string? personality,
        bool callModel,
        CancellationToken cancellationToken)
    {
        var key = RequireKey(dnaKey);
        var prior = await database.CreativeAcademyDna.AsNoTracking()
            .FirstOrDefaultAsync(item => item.DnaKey == key, cancellationToken);
        if (prior is not null)
        {
            return new AcademyWrite(true, false, prior.Notice);
        }

        var decision = CreativeAcademy.Choose(family, brandName, hero, palette, cta, personality, callModel);
        database.CreativeAcademyDna.Add(new CreativeAcademyDnaRow
        {
            Id = Guid.NewGuid(),
            DnaKey = key,
            Family = decision.Family,
            BrandName = decision.BrandName,
            Hero = decision.Hero,
            Palette = decision.Palette,
            Cta = decision.Cta,
            Personality = decision.Personality,
            TeacherKey = decision.TeacherKey,
            TeacherOnFile = decision.TeacherOnFile,
            Distinct = decision.Distinct,
            ModelCalls = 0,
            Notice = decision.Notice,
            Delivery = "NOT_SENT",
            RecordedAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return new AcademyWrite(false, true, decision.Notice);
    }

    public async Task<AcademyWrite> RecordVisualAsync(
        string? lessonKey,
        bool? visualBenchmarkMet,
        string? visualNote,
        bool callModel,
        bool campaignReady,
        CancellationToken cancellationToken)
    {
        var key = RequireKey(lessonKey);
        var row = await database.CreativeAcademyLessons.FirstOrDefaultAsync(item => item.LessonKey == key, cancellationToken);
        if (row is null)
        {
            throw new InvalidOperationException("The lesson is not stored. None was invented.");
        }

        if (row.Role is CreativeAcademy.Reference or CreativeAcademy.MasterReference or CreativeAcademy.SuppliedExample)
        {
            throw new InvalidOperationException("The prototype is the teacher. It is not approved as an advertiser.");
        }

        var inspection = CreativeAcademy.Judge(
            row.Role,
            row.BrandName,
            row.Headline,
            row.Body,
            row.ProperNouns,
            false,
            visualBenchmarkMet,
            visualNote,
            callModel,
            campaignReady,
            null);
        row.Status = inspection.Status;
        row.Score = inspection.Score;
        row.Critical = inspection.Critical;
        row.VisualRecorded = inspection.VisualRecorded;
        row.Defects = Join(inspection.Defects);
        row.PreserveList = Join(inspection.Preserve);
        row.RepairList = Join(inspection.Repair);
        row.ModelCalls = 0;
        row.CampaignReady = false;
        row.Notice = inspection.Notice;
        row.Delivery = "NOT_SENT";
        await database.SaveChangesAsync(cancellationToken);
        return new AcademyWrite(false, true, inspection.Notice);
    }

    private static string RequireKey(string? value)
    {
        var key = (value ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("A lesson key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var allowed = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!allowed)
            {
                throw new InvalidOperationException("A lesson key is required. None is invented.");
            }
        }

        return key;
    }

    private static string Join(IReadOnlyList<string> lines) => string.Join("\n", lines);
}

public sealed record AcademyBoard(
    IReadOnlyList<CreativeAcademyLessonRow> Lessons,
    IReadOnlyList<CreativeAcademyDnaRow> Dna);

public sealed record AcademyWrite(bool Duplicate, bool Written, string Notice);
