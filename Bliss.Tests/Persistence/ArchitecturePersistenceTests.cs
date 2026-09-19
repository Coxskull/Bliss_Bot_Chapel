using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class CreatorContentArchitectureTests
{
    [Fact]
    public async Task Creator_supports_multiple_content_items()
    {
        await using var db = TestDb.CreateContext();
        var creator = new Creator
        {
            Id = Guid.NewGuid(),
            Name = "Creator A",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Creators.Add(creator);
        db.ContentItems.AddRange(
            new ContentItem { Id = Guid.NewGuid(), CreatorId = creator.Id, ContentType = "PODCAST", Title = "Content 1", CreatedAt = DateTime.UtcNow },
            new ContentItem { Id = Guid.NewGuid(), CreatorId = creator.Id, ContentType = "PODCAST", Title = "Content 2", CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();

        var loaded = await db.ContentItems.CountAsync(x => x.CreatorId == creator.Id);
        Assert.Equal(2, loaded);
    }
}

public sealed class InventoryArchitectureTests
{
    [Fact]
    public async Task Content_item_supports_multiple_ad_inventory_slots()
    {
        await using var db = TestDb.CreateContext();
        var creator = new Creator { Id = Guid.NewGuid(), Name = "Creator A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var content = new ContentItem { Id = Guid.NewGuid(), CreatorId = creator.Id, ContentType = "PODCAST", Title = "Content 1", CreatedAt = DateTime.UtcNow };

        db.Creators.Add(creator);
        db.ContentItems.Add(content);
        db.AdInventorySlots.AddRange(
            new AdInventorySlot { Id = Guid.NewGuid(), ContentItemId = content.Id, SlotType = InventorySlotTypes.PreRoll },
            new AdInventorySlot { Id = Guid.NewGuid(), ContentItemId = content.Id, SlotType = InventorySlotTypes.MidRoll },
            new AdInventorySlot { Id = Guid.NewGuid(), ContentItemId = content.Id, SlotType = InventorySlotTypes.LowerThird },
            new AdInventorySlot { Id = Guid.NewGuid(), ContentItemId = content.Id, SlotType = InventorySlotTypes.PostRoll });

        await db.SaveChangesAsync();

        var slots = await db.AdInventorySlots.Where(x => x.ContentItemId == content.Id).Select(x => x.SlotType).ToListAsync();
        Assert.Equal(4, slots.Count);
        Assert.Contains(InventorySlotTypes.PreRoll, slots);
        Assert.Contains(InventorySlotTypes.MidRoll, slots);
        Assert.Contains(InventorySlotTypes.LowerThird, slots);
        Assert.Contains(InventorySlotTypes.PostRoll, slots);
    }
}

public sealed class BlissMatchArchitectureTests
{
    [Fact]
    public async Task Creator_supports_multiple_simultaneous_bliss_matches_without_uniqueness_violation()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var creator = new Creator { Id = Guid.NewGuid(), Name = "Creator A", CreatedAt = now, UpdatedAt = now };
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "Advertiser", CreatedAt = now };
        var program = new AdvertiserProgram { Id = Guid.NewGuid(), AdvertiserId = advertiser.Id, Name = "Program" };
        var oppA = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id, Name = "Opportunity A" };
        var oppB = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id, Name = "Opportunity B" };
        var oppC = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id, Name = "Opportunity C" };
        var rule = new RuleVersion { Id = Guid.NewGuid(), Version = "1", Name = "Rules v1", CreatedAt = now };

        db.AddRange(creator, advertiser, program, oppA, oppB, oppC, rule);
        db.BlissMatches.AddRange(
            new BlissMatch { Id = Guid.NewGuid(), CreatorId = creator.Id, AdvertiserOpportunityId = oppA.Id, RuleVersionId = rule.Id, CreatedAt = now },
            new BlissMatch { Id = Guid.NewGuid(), CreatorId = creator.Id, AdvertiserOpportunityId = oppB.Id, RuleVersionId = rule.Id, CreatedAt = now },
            new BlissMatch { Id = Guid.NewGuid(), CreatorId = creator.Id, AdvertiserOpportunityId = oppC.Id, RuleVersionId = rule.Id, CreatedAt = now });

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        Assert.Null(exception);
        Assert.Equal(3, await db.BlissMatches.CountAsync(x => x.CreatorId == creator.Id));
    }

    [Fact]
    public async Task New_bliss_match_does_not_overwrite_another_valid_match()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var creator = new Creator { Id = Guid.NewGuid(), Name = "Creator A", CreatedAt = now, UpdatedAt = now };
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "Advertiser", CreatedAt = now };
        var program = new AdvertiserProgram { Id = Guid.NewGuid(), AdvertiserId = advertiser.Id, Name = "Program" };
        var oppA = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id, Name = "A" };
        var oppB = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id, Name = "B" };
        var rule = new RuleVersion { Id = Guid.NewGuid(), Version = "1", Name = "Rules v1", CreatedAt = now };
        var matchA = new BlissMatch { Id = Guid.NewGuid(), CreatorId = creator.Id, AdvertiserOpportunityId = oppA.Id, RuleVersionId = rule.Id, CreatedAt = now };

        db.AddRange(creator, advertiser, program, oppA, oppB, rule, matchA);
        await db.SaveChangesAsync();

        db.BlissMatches.Add(new BlissMatch
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            AdvertiserOpportunityId = oppB.Id,
            RuleVersionId = rule.Id,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        var persistedA = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == matchA.Id);
        Assert.Equal(oppA.Id, persistedA.AdvertiserOpportunityId);
        Assert.Equal(2, await db.BlissMatches.CountAsync(x => x.CreatorId == creator.Id));
    }
}

public sealed class CampaignPlacementArchitectureTests
{
    [Fact]
    public async Task Multiple_campaign_placements_can_reference_the_same_content_item()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var creator = new Creator { Id = Guid.NewGuid(), Name = "Creator A", CreatedAt = now, UpdatedAt = now };
        var content = new ContentItem { Id = Guid.NewGuid(), CreatorId = creator.Id, ContentType = "PODCAST", Title = "Content 1", CreatedAt = now };
        var slot = new AdInventorySlot { Id = Guid.NewGuid(), ContentItemId = content.Id, SlotType = InventorySlotTypes.PreRoll };
        var campaign = new Campaign { Id = Guid.NewGuid(), Name = "Campaign", CreatedAt = now };

        db.AddRange(creator, content, slot, campaign);
        db.CampaignPlacements.AddRange(
            new CampaignPlacement { Id = Guid.NewGuid(), CampaignId = campaign.Id, ContentItemId = content.Id, AdInventorySlotId = slot.Id },
            new CampaignPlacement { Id = Guid.NewGuid(), CampaignId = campaign.Id, ContentItemId = content.Id, AdInventorySlotId = slot.Id },
            new CampaignPlacement { Id = Guid.NewGuid(), CampaignId = campaign.Id, ContentItemId = content.Id, AdInventorySlotId = slot.Id });

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());
        Assert.Null(exception);
        Assert.Equal(3, await db.CampaignPlacements.CountAsync(x => x.ContentItemId == content.Id));
    }
}

public sealed class HistoricalRuleVersionTests
{
    [Fact]
    public async Task Historical_bliss_match_preserves_original_rule_version()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var creator = new Creator { Id = Guid.NewGuid(), Name = "Creator A", CreatedAt = now, UpdatedAt = now };
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "Advertiser", CreatedAt = now };
        var program = new AdvertiserProgram { Id = Guid.NewGuid(), AdvertiserId = advertiser.Id, Name = "Program" };
        var opportunity = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id, Name = "Opportunity" };
        var rule1 = new RuleVersion { Id = Guid.NewGuid(), Version = "1", Name = "RuleVersion 1", CreatedAt = now };
        var matchA = new BlissMatch
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            AdvertiserOpportunityId = opportunity.Id,
            RuleVersionId = rule1.Id,
            CreatedAt = now
        };

        db.AddRange(creator, advertiser, program, opportunity, rule1, matchA);
        await db.SaveChangesAsync();

        var rule2 = new RuleVersion { Id = Guid.NewGuid(), Version = "2", Name = "RuleVersion 2", IsActive = true, CreatedAt = now.AddDays(1) };
        db.RuleVersions.Add(rule2);
        await db.SaveChangesAsync();

        var persisted = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == matchA.Id);
        Assert.Equal(rule1.Id, persisted.RuleVersionId);
        Assert.NotEqual(rule2.Id, persisted.RuleVersionId);
    }
}

public sealed class AccessIndependenceTests
{
    [Fact]
    public async Task Network_access_and_program_access_are_independent()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "Advertiser", CreatedAt = now };
        var program = new AdvertiserProgram { Id = Guid.NewGuid(), AdvertiserId = advertiser.Id, Name = "Program" };
        var network = new AffiliateNetwork { Id = Guid.NewGuid(), Name = "Network" };

        db.AddRange(advertiser, program, network);
        db.NetworkAccesses.Add(new NetworkAccess
        {
            Id = Guid.NewGuid(),
            AdvertiserId = advertiser.Id,
            AffiliateNetworkId = network.Id,
            Status = EntityStatuses.Approved,
            ApprovedAt = now
        });
        db.ProgramAccesses.Add(new ProgramAccess
        {
            Id = Guid.NewGuid(),
            AdvertiserProgramId = program.Id,
            Status = EntityStatuses.Unknown
        });

        await db.SaveChangesAsync();

        var networkAccess = await db.NetworkAccesses.SingleAsync();
        var programAccess = await db.ProgramAccesses.SingleAsync();
        Assert.Equal(EntityStatuses.Approved, networkAccess.Status);
        Assert.Equal(EntityStatuses.Unknown, programAccess.Status);
        Assert.NotEqual(networkAccess.Status, programAccess.Status);
    }
}

public sealed class DataProvenanceTests
{
    [Fact]
    public async Task Data_provenance_retains_source_confidence_and_collected_at()
    {
        await using var db = TestDb.CreateContext();
        var collectedAt = new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc);
        var provenance = new DataProvenance
        {
            Id = Guid.NewGuid(),
            EntityType = nameof(Creator),
            EntityId = Guid.NewGuid(),
            FieldName = nameof(Creator.FemalePercentage),
            SourceType = "PLATFORM",
            SourceName = "Instagram",
            SourceUrl = "https://example.test/ig",
            ConfidenceLevel = "HIGH",
            CollectedAt = collectedAt,
            Notes = "85%"
        };

        db.DataProvenances.Add(provenance);
        await db.SaveChangesAsync();

        var loaded = await db.DataProvenances.AsNoTracking().SingleAsync();
        Assert.Equal("Instagram", loaded.SourceName);
        Assert.Equal("HIGH", loaded.ConfidenceLevel);
        Assert.Equal(collectedAt, loaded.CollectedAt);
        Assert.Equal("PLATFORM", loaded.SourceType);
    }
}

public sealed class UnknownDemographicTests
{
    [Fact]
    public async Task Unknown_female_percentage_remains_null_and_is_not_converted_to_zero()
    {
        await using var db = TestDb.CreateContext();
        var creator = new Creator
        {
            Id = Guid.NewGuid(),
            Name = "Unknown Demographics",
            FemalePercentage = null,
            MalePercentage = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Creators.Add(creator);
        await db.SaveChangesAsync();

        var loaded = await db.Creators.AsNoTracking().SingleAsync();
        Assert.Null(loaded.FemalePercentage);
        Assert.Null(loaded.MalePercentage);
        Assert.NotEqual(0m, loaded.FemalePercentage);
    }
}

public sealed class SeedDataTests
{
    [Fact]
    public async Task Phase1_seed_creates_required_graph()
    {
        await using var db = TestDb.CreateContext();
        var seeder = new Phase1DataSeeder(db);
        await seeder.SeedAsync();

        Assert.True(await db.Creators.CountAsync() >= 1);
        Assert.True(await db.ContentItems.CountAsync(x => x.CreatorId == Phase1DataSeeder.CreatorId) >= 2);
        Assert.Equal(4, await db.AdInventorySlots.CountAsync(x => x.ContentItemId == Phase1DataSeeder.ContentItem1Id));
        Assert.True(await db.Advertisers.AnyAsync());
        Assert.True(await db.AdvertiserPrograms.AnyAsync());
        Assert.True(await db.AdvertiserOpportunities.CountAsync() >= 3);
        Assert.Equal(3, await db.BlissMatches.CountAsync(x => x.CreatorId == Phase1DataSeeder.CreatorId));
    }
}
