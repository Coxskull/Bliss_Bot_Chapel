using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Demonstrations;

/// <summary>
/// Loads an existing opportunity, the active rule document, and creators
/// already stored in Bliss, then asks <see cref="BlissRematch"/> to choose.
/// </summary>
public sealed class BlissRematchGate(BlissDbContext database)
{
    public async Task<BlissRematchDecision> SelectAsync(
        Guid? opportunityId,
        Guid? currentCreatorId,
        CancellationToken cancellationToken)
    {
        if (opportunityId is null)
        {
            return BlissRematch.Select(currentCreatorId, null, null, Array.Empty<Creator>());
        }

        var opportunity = await database.AdvertiserOpportunities
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == opportunityId.Value, cancellationToken);
        var ruleRow = await database.RuleVersions
            .AsNoTracking()
            .Where(item => item.IsActive && item.DocumentJson != null && item.DocumentJson != "")
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        RuleDocument? rules = null;
        if (ruleRow?.DocumentJson is not null)
        {
            try
            {
                rules = DeterministicRuleEvaluator.ParseDocument(ruleRow.DocumentJson);
            }
            catch (InvalidOperationException)
            {
                rules = null;
            }
        }

        IReadOnlyList<Creator> creators = opportunity is null || rules is null
            ? Array.Empty<Creator>()
            : await database.Creators.AsNoTracking().ToListAsync(cancellationToken);
        return BlissRematch.Select(currentCreatorId, opportunity, rules, creators);
    }
}
