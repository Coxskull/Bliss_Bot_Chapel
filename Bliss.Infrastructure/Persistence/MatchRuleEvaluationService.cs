using Bliss.Domain.Entities;
using Bliss.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed class MatchRuleEvaluationService
{
    private readonly BlissDbContext _db;

    public MatchRuleEvaluationService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<BlissMatch?> EvaluateAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var match = await _db.BlissMatches
            .Include(x => x.Creator)
            .Include(x => x.AdvertiserOpportunity)
            .Include(x => x.RuleVersion)
            .SingleOrDefaultAsync(x => x.Id == matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var document = DeterministicRuleEvaluator.ParseDocument(match.RuleVersion.DocumentJson);
        var result = DeterministicRuleEvaluator.Evaluate(match.Creator, match.AdvertiserOpportunity, document);
        var ruleVersionId = match.RuleVersionId;

        var existingChecks = await _db.EligibilityChecks
            .Where(x => x.BlissMatchId == matchId)
            .ToListAsync(cancellationToken);
        var existingScores = await _db.MatchScoreComponents
            .Where(x => x.BlissMatchId == matchId)
            .ToListAsync(cancellationToken);
        _db.EligibilityChecks.RemoveRange(existingChecks);
        _db.MatchScoreComponents.RemoveRange(existingScores);
        await _db.SaveChangesAsync(cancellationToken);

        match.Status = result.MatchStatus;
        match.OverallScore = result.OverallScore;
        match.ConfidenceScore = result.ConfidenceScore;
        match.RuleVersionId = ruleVersionId;
        _db.EligibilityChecks.AddRange(Phase2DataSeeder.BuildEligibility(match.Id, result));
        _db.MatchScoreComponents.AddRange(Phase2DataSeeder.BuildScores(match.Id, result));
        await _db.SaveChangesAsync(cancellationToken);
        return match;
    }
}
