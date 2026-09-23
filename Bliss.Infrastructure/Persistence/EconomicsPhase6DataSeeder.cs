using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed class EconomicsPhase6DataSeeder
{
    public static readonly Guid RuleVersionId =
        Guid.Parse("ec000012-0000-0000-0000-000000000001");

    private readonly BlissDbContext _db;

    public EconomicsPhase6DataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.CompensationRuleVersions.AnyAsync(
            x => x.Id == RuleVersionId, cancellationToken))
        {
            return;
        }

        var createdAt = new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc);
        var version = new CompensationRuleVersion
        {
            Id = RuleVersionId,
            Version = "economics-compensation/1.0.0-test",
            Name = "TEST three-part percentage illustration",
            DocumentJson = JsonSerializer.Serialize(new
            {
                method = "PERCENTAGE",
                purpose = "TEST_ONLY",
                createsSettlement = false,
                note = "Configurable evidence policy; not a universal split."
            }),
            IsActive = true,
            EffectiveAt = createdAt,
            CreatedAt = createdAt
        };
        version.Allocations.Add(Allocation(
            "ec000013-0000-0000-0000-000000000001",
            version.Id, CompensationParticipantRoles.Alpha,
            "Alpha platform illustration", 18m, 1));
        version.Allocations.Add(Allocation(
            "ec000013-0000-0000-0000-000000000002",
            version.Id, CompensationParticipantRoles.Creator,
            "Creator illustration", 72m, 2));
        version.Allocations.Add(Allocation(
            "ec000013-0000-0000-0000-000000000003",
            version.Id, CompensationParticipantRoles.OtherAuthorized,
            "Other authorized participant illustration", 10m, 3));

        _db.CompensationRuleVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static CompensationRuleAllocation Allocation(
        string id,
        Guid ruleId,
        string role,
        string label,
        decimal percentage,
        int sortOrder) =>
        new()
        {
            Id = Guid.Parse(id),
            CompensationRuleVersionId = ruleId,
            ParticipantRole = role,
            ParticipantLabel = label,
            Percentage = percentage,
            SortOrder = sortOrder
        };
}
