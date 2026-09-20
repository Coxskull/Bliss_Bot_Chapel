using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Additive Phase 3 history: persist evaluation runs for Phase 2 matches, then re-evaluate Brazil once.
/// Does not delete Phase 1/2 rows or prior evaluation runs.
/// </summary>
public sealed class Phase3DataSeeder
{
    public static readonly Guid RunGeoFailId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1");
    public static readonly Guid RunBrazilFirstId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee2");
    public static readonly Guid RunUnknownId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee3");
    public static readonly Guid RunSecondAdvertiserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee4");
    public static readonly Guid RunBrazilSecondId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee5");

    private readonly MatchRuleEvaluationService _evaluator;
    private readonly BlissDbContext _db;

    public Phase3DataSeeder(BlissDbContext db, MatchRuleEvaluationService evaluator)
    {
        _db = db;
        _evaluator = evaluator;
    }

    public Phase3DataSeeder(BlissDbContext db)
        : this(db, new MatchRuleEvaluationService(db))
    {
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.MatchEvaluationRuns.AnyAsync(x => x.Id == RunGeoFailId, cancellationToken))
        {
            return;
        }

        if (!await _db.BlissMatches.AnyAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId, cancellationToken))
        {
            throw new InvalidOperationException("Phase 3 seed requires Phase 2 seed data.");
        }

        await _evaluator.EvaluateAsync(Phase2DataSeeder.MatchGeoFailId, RunGeoFailId, cancellationToken);
        await _evaluator.EvaluateAsync(Phase2DataSeeder.MatchBrazilApprovedId, RunBrazilFirstId, cancellationToken);
        await _evaluator.EvaluateAsync(Phase2DataSeeder.MatchUnknownReviewId, RunUnknownId, cancellationToken);
        await _evaluator.EvaluateAsync(Phase2DataSeeder.MatchSecondAdvertiserId, RunSecondAdvertiserId, cancellationToken);
        await _evaluator.EvaluateAsync(Phase2DataSeeder.MatchBrazilApprovedId, RunBrazilSecondId, cancellationToken);
    }
}
