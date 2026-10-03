using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;

namespace Bliss.Domain.Demonstrations;

public sealed record BlissRematchDecision(
    Guid? CreatorId,
    string CreatorName,
    string MatchStatus,
    decimal? OverallScore,
    string Notice);

/// <summary>
/// Looks for another creator already stored in Bliss when the current one
/// cannot serve the budget. The accepted evaluator is the only scorer.
/// </summary>
public static class BlissRematch
{
    public const string NoOpportunityNotice =
        "Bliss will not rematch without an active opportunity. The advertiser is kept. Delivery remains NOT_SENT.";

    public const string NoMatchNotice =
        "The current creator cannot serve this budget. Bliss found no other approved match. The advertiser is kept. This is not a win. Delivery remains NOT_SENT.";

    public static string ApprovedNotice(string name) =>
        "The current creator cannot serve this budget. Bliss approved " + name + " through the accepted evaluator. This is not a win. Delivery remains NOT_SENT.";

    public static BlissRematchDecision Select(
        Guid? currentCreatorId,
        AdvertiserOpportunity? opportunity,
        RuleDocument? rules,
        IReadOnlyList<Creator> creators)
    {
        if (opportunity is null
            || rules is null
            || !string.Equals(opportunity.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return new BlissRematchDecision(null, string.Empty, string.Empty, null, NoOpportunityNotice);
        }

        Creator? best = null;
        decimal bestScore = 0m;
        string bestStatus = string.Empty;
        foreach (var creator in creators)
        {
            if (currentCreatorId is Guid current && creator.Id == current)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(creator.Name))
            {
                continue;
            }

            var result = DeterministicRuleEvaluator.Evaluate(creator, opportunity, rules);
            if (!string.Equals(result.MatchStatus, EntityStatuses.Approved, StringComparison.Ordinal))
            {
                continue;
            }

            var better = best is null
                || result.OverallScore > bestScore
                || (result.OverallScore == bestScore
                    && string.Compare(creator.Name, best.Name, StringComparison.Ordinal) < 0);
            if (!better)
            {
                continue;
            }

            best = creator;
            bestScore = result.OverallScore;
            bestStatus = result.MatchStatus;
        }

        if (best is null)
        {
            return new BlissRematchDecision(null, string.Empty, string.Empty, null, NoMatchNotice);
        }

        return new BlissRematchDecision(
            best.Id,
            best.Name,
            bestStatus,
            bestScore,
            ApprovedNotice(best.Name));
    }
}
