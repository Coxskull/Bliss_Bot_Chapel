using Bliss.Api.Demonstrations;
using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class MarketplaceHandoffService(ProspectDemonstrationService demonstrations, BlissDbContext database)
{
    public async Task<HandoffBoard> ReadAsync(string? tenant, CancellationToken cancellationToken)
    {
        var library = demonstrations.Library();
        var creators = await database.Creators.AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var rule = await ActiveRuleAsync(cancellationToken);
        var rows = await database.MarketplaceHandoffs.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        var key = (tenant ?? string.Empty).Trim();
        IReadOnlyList<MarketplaceHandoffRow> visible = key.Length < 3
            ? []
            : rows.Where(item => string.Equals(item.TenantKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
        return new HandoffBoard(key, rule, Advertisers(library.Demonstrations), creators, visible);
    }

    public async Task<HandoffWrite> HandAsync(string? tenant, Guid creatorId, CancellationToken cancellationToken)
    {
        var library = demonstrations.Library();
        var match = library.Demonstrations.FirstOrDefault(item =>
            string.Equals(item.Slug, (tenant ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
        var advertiser = match is null
            ? new HandoffAdvertiser(tenant, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 0, string.Empty, false)
            : new HandoffAdvertiser(
                match.Slug,
                match.BusinessName,
                match.PublicSourceUrl,
                match.Country,
                match.Language,
                match.Niche,
                match.OpportunityScore,
                match.ProspectState,
                match.Suppressed);
        var creatorRow = await database.Creators.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == creatorId, cancellationToken);
        if (creatorRow is null)
        {
            throw new InvalidOperationException("A stored creator is required. None is invented.");
        }

        var creator = new HandoffCreator(
            creatorRow.Id,
            creatorRow.Name,
            creatorRow.CountryCode,
            creatorRow.PrimaryLanguage,
            creatorRow.AudienceSize);
        var stored = await database.MarketplaceHandoffs.AsNoTracking().ToListAsync(cancellationToken);
        var existing = stored.Select(item => new HandoffRecord(item.TenantKey, item.CreatorId, item.SourceUrl)).ToList();
        var duplicate = existing.FirstOrDefault(item =>
            item.CreatorId == creator.Id
            && string.Equals(item.TenantKey, (advertiser.TenantKey ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            && SameSource(item.SourceUrl, advertiser.PublicSourceUrl));
        if (duplicate is not null)
        {
            var row = stored.First(item =>
                item.CreatorId == duplicate.CreatorId
                && string.Equals(item.TenantKey, duplicate.TenantKey, StringComparison.OrdinalIgnoreCase)
                && SameSource(item.SourceUrl, duplicate.SourceUrl));
            return new HandoffWrite(row, true, false);
        }

        var rule = await ActiveRuleAsync(cancellationToken);
        RuleDocument? rules = null;
        if (rule?.DocumentJson is not null)
        {
            rules = DeterministicRuleEvaluator.ParseDocument(rule.DocumentJson);
        }

        var reading = MarketplaceHandoff.Hand(advertiser, creator, rules, existing);
        if (!reading.Qualified || !reading.EvaluatorInvoked)
        {
            return new HandoffWrite(null, false, false, reading);
        }

        var saved = new MarketplaceHandoffRow
        {
            Id = Guid.NewGuid(),
            TenantKey = reading.TenantKey,
            BusinessName = reading.BusinessName,
            SourceUrl = reading.SourceUrl,
            CreatorId = creator.Id,
            CreatorName = reading.CreatorName,
            RuleVersionId = rule!.Id,
            RuleName = rule.Name,
            MatchStatus = reading.MatchStatus,
            OverallScore = reading.OverallScore,
            EvaluatorInvoked = true,
            WinClaimed = false,
            Notice = reading.Notice,
            Delivery = reading.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.MarketplaceHandoffs.Add(saved);
        await database.SaveChangesAsync(cancellationToken);
        return new HandoffWrite(saved, false, true, reading);
    }

    private async Task<RuleVersion?> ActiveRuleAsync(CancellationToken cancellationToken) =>
        await database.RuleVersions.AsNoTracking()
            .Where(item => item.IsActive && item.DocumentJson != null && item.DocumentJson != "")
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);

    private static List<HandoffAdvertiser> Advertisers(IEnumerable<DemonstrationRecord> demonstrations) =>
        demonstrations
            .OrderBy(item => item.BusinessName)
            .Select(item => new HandoffAdvertiser(
                item.Slug,
                item.BusinessName,
                item.PublicSourceUrl,
                item.Country,
                item.Language,
                item.Niche,
                item.OpportunityScore,
                item.ProspectState,
                item.Suppressed))
            .ToList();

    private static bool SameSource(string left, string? right)
    {
        static string Key(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            while (text.EndsWith('/'))
            {
                text = text[..^1];
            }

            return text;
        }

        return string.Equals(Key(left), Key(right), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record HandoffBoard(
    string Tenant,
    RuleVersion? Rule,
    IReadOnlyList<HandoffAdvertiser> Advertisers,
    IReadOnlyList<Creator> Creators,
    IReadOnlyList<MarketplaceHandoffRow> Handoffs);

public sealed record HandoffWrite(
    MarketplaceHandoffRow? Row,
    bool Duplicate,
    bool Written,
    HandoffReading? Reading = null);
