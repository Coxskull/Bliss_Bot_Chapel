using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed class EconomicsPhase4DataSeeder
{
    public static readonly Guid RuleVersionId =
        Guid.Parse("ec000011-0000-0000-0000-000000000001");

    private readonly BlissDbContext _db;

    public EconomicsPhase4DataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.PricingRuleVersions.AnyAsync(x => x.Id == RuleVersionId, cancellationToken))
        {
            return;
        }

        var document = new PricingRuleDocument
        {
            ReachBelow10kMultiplier = 0.80m,
            Reach10kTo49kMultiplier = 1.00m,
            Reach50kTo99kMultiplier = 1.15m,
            Reach100kPlusMultiplier = 1.30m,
            EngagementBelow4PercentMultiplier = 0.90m,
            Engagement4To699PercentMultiplier = 1.00m,
            Engagement7PercentPlusMultiplier = 1.10m
        };

        _db.PricingRuleVersions.Add(new PricingRuleVersion
        {
            Id = RuleVersionId,
            Version = "economics-rate/1.0.0",
            Name = "Deterministic bootstrap range v1",
            DocumentJson = JsonSerializer.Serialize(document),
            IsActive = true,
            CreatedAt = new DateTime(2026, 9, 23, 3, 0, 0, DateTimeKind.Utc)
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
