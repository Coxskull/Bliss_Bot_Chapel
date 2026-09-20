using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Controlled fictional Phase 1 seed data. Does not call external APIs or scoring engines.
/// </summary>
public sealed class Phase1DataSeeder
{
    public static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ContentItem1Id = Guid.Parse("22222222-2222-2222-2222-222222222221");
    public static readonly Guid ContentItem2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid AdvertiserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ProgramId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid OpportunityAId = Guid.Parse("55555555-5555-5555-5555-555555555551");
    public static readonly Guid OpportunityBId = Guid.Parse("55555555-5555-5555-5555-555555555552");
    public static readonly Guid OpportunityCId = Guid.Parse("55555555-5555-5555-5555-555555555553");
    public static readonly Guid RuleVersion1Id = Guid.Parse("66666666-6666-6666-6666-666666666661");
    public static readonly Guid MatchAId = Guid.Parse("77777777-7777-7777-7777-777777777771");
    public static readonly Guid MatchBId = Guid.Parse("77777777-7777-7777-7777-777777777772");
    public static readonly Guid MatchCId = Guid.Parse("77777777-7777-7777-7777-777777777773");

    private readonly BlissDbContext _db;

    public Phase1DataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Creators.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

        var creator = new Creator
        {
            Id = CreatorId,
            Name = "Test Creator",
            CountryCode = "PH",
            PrimaryLanguage = "English / Tagalog",
            AudienceSize = 100000,
            FemalePercentage = 70m,
            MalePercentage = 30m,
            PrimaryAgeRange = "18-34",
            PrimaryGeography = "Metro Manila",
            EngagementLevel = "STRONG",
            CreatedAt = now,
            UpdatedAt = now
        };

        var platform = new CreatorPlatform
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888881"),
            CreatorId = CreatorId,
            Platform = "Podcast",
            ExternalProfileId = "CRT-TEST-001",
            IdentityKey = CreatorIngestionService.BuildIdentityKey("Podcast", "CRT-TEST-001"),
            ProfileUrl = "https://example.test/creators/crt-test-001",
            Followers = 100000,
            LastCollectedAt = now
        };

        var content1 = new ContentItem
        {
            Id = ContentItem1Id,
            CreatorId = CreatorId,
            ContentType = "PODCAST_EPISODE",
            Title = "Chapel Conversations Episode 1",
            ExternalContentId = "CNT-TEST-001",
            Url = "https://example.test/content/cnt-test-001",
            PublishedAt = now,
            CreatedAt = now
        };

        var content2 = new ContentItem
        {
            Id = ContentItem2Id,
            CreatorId = CreatorId,
            ContentType = "PODCAST_EPISODE",
            Title = "Chapel Conversations Episode 2",
            ExternalContentId = "CNT-TEST-002",
            Url = "https://example.test/content/cnt-test-002",
            PublishedAt = now,
            CreatedAt = now
        };

        var slots = new[]
        {
            new AdInventorySlot { Id = Guid.Parse("99999999-9999-9999-9999-999999999991"), ContentItemId = ContentItem1Id, SlotType = InventorySlotTypes.PreRoll, StartSecond = 0, DurationSeconds = 30, IsAvailable = true },
            new AdInventorySlot { Id = Guid.Parse("99999999-9999-9999-9999-999999999992"), ContentItemId = ContentItem1Id, SlotType = InventorySlotTypes.MidRoll, StartSecond = 600, DurationSeconds = 30, IsAvailable = true },
            new AdInventorySlot { Id = Guid.Parse("99999999-9999-9999-9999-999999999993"), ContentItemId = ContentItem1Id, SlotType = InventorySlotTypes.LowerThird, StartSecond = 120, DurationSeconds = 10, IsAvailable = true },
            new AdInventorySlot { Id = Guid.Parse("99999999-9999-9999-9999-999999999994"), ContentItemId = ContentItem1Id, SlotType = InventorySlotTypes.PostRoll, StartSecond = 1800, DurationSeconds = 15, IsAvailable = true }
        };

        var advertiser = new Advertiser
        {
            Id = AdvertiserId,
            Name = "Sunrise Wellness Co.",
            Website = "https://example.test/sunrise",
            CountryCode = "PH",
            Description = "Fictional wellness advertiser for Phase 1 tests.",
            CreatedAt = now
        };

        var program = new AdvertiserProgram
        {
            Id = ProgramId,
            AdvertiserId = AdvertiserId,
            Name = "Sunrise Affiliate Program",
            ExternalProgramId = "PRG-TEST-001",
            Status = EntityStatuses.Active
        };

        var opportunityA = new AdvertiserOpportunity
        {
            Id = OpportunityAId,
            AdvertiserProgramId = ProgramId,
            Name = "Opportunity A - Morning Tonic",
            ProductName = "Morning Tonic",
            Category = "Wellness",
            Description = "Fictional product opportunity A.",
            MarketCountryCode = "PH",
            Language = "English",
            CommissionPercentage = 12.5m,
            CommissionType = "PERCENTAGE",
            ExternalOpportunityId = "OPP-TEST-A",
            Status = EntityStatuses.Active
        };

        var opportunityB = new AdvertiserOpportunity
        {
            Id = OpportunityBId,
            AdvertiserProgramId = ProgramId,
            Name = "Opportunity B - Sleep Tea",
            ProductName = "Sleep Tea",
            Category = "Wellness",
            MarketCountryCode = "PH",
            Language = "English",
            CommissionPercentage = 10m,
            CommissionType = "PERCENTAGE",
            ExternalOpportunityId = "OPP-TEST-B",
            Status = EntityStatuses.Active
        };

        var opportunityC = new AdvertiserOpportunity
        {
            Id = OpportunityCId,
            AdvertiserProgramId = ProgramId,
            Name = "Opportunity C - Studio Mic Bundle",
            ProductName = "Studio Mic Bundle",
            Category = "Creator Tools",
            MarketCountryCode = "PH",
            Language = "English",
            FixedFee = 250m,
            CommissionType = "FIXED",
            ExternalOpportunityId = "OPP-TEST-C",
            Status = EntityStatuses.Active
        };

        var network = new AffiliateNetwork
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
            Name = "Example Affiliate Network",
            Website = "https://example.test/network",
            Status = EntityStatuses.Active
        };

        var networkAccess = new NetworkAccess
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
            AdvertiserId = AdvertiserId,
            AffiliateNetworkId = network.Id,
            Status = EntityStatuses.Approved,
            ExternalAccountId = "NET-ACC-001",
            ApprovedAt = now
        };

        var programAccess = new ProgramAccess
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3"),
            AdvertiserProgramId = ProgramId,
            Status = EntityStatuses.Unknown
        };

        var rule1 = new RuleVersion
        {
            Id = RuleVersion1Id,
            Version = "1.0.0",
            Name = "Phase 1 Baseline Rules",
            Description = "Historical rule snapshot used by seeded matches.",
            IsActive = true,
            CreatedAt = now
        };

        var matchA = new BlissMatch
        {
            Id = MatchAId,
            CreatorId = CreatorId,
            AdvertiserOpportunityId = OpportunityAId,
            RuleVersionId = RuleVersion1Id,
            Status = EntityStatuses.Created,
            OverallScore = null,
            ConfidenceScore = null,
            CreatedAt = now
        };

        var matchB = new BlissMatch
        {
            Id = MatchBId,
            CreatorId = CreatorId,
            AdvertiserOpportunityId = OpportunityBId,
            RuleVersionId = RuleVersion1Id,
            Status = EntityStatuses.Created,
            CreatedAt = now
        };

        var matchC = new BlissMatch
        {
            Id = MatchCId,
            CreatorId = CreatorId,
            AdvertiserOpportunityId = OpportunityCId,
            RuleVersionId = RuleVersion1Id,
            Status = EntityStatuses.Created,
            CreatedAt = now
        };

        var scoreComponent = new MatchScoreComponent
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1"),
            BlissMatchId = MatchAId,
            ComponentName = "PLACEHOLDER_AUDIENCE_FIT",
            Score = null,
            Weight = null,
            Explanation = "Persisted for future scoring; Phase 1 does not calculate scores."
        };

        var eligibilityCheck = new EligibilityCheck
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2"),
            BlissMatchId = MatchAId,
            CheckType = "PLACEHOLDER_NETWORK_ACCESS",
            Result = EntityStatuses.Unknown,
            ReasonCode = "NOT_EVALUATED",
            Explanation = "Persisted for future Chaperone checks; Phase 1 does not evaluate eligibility."
        };

        var provenance = new DataProvenance
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"),
            EntityType = nameof(Creator),
            EntityId = CreatorId,
            FieldName = nameof(Creator.FemalePercentage),
            SourceType = "PLATFORM",
            SourceName = "Podcast",
            SourceUrl = "https://example.test/creators/crt-test-001",
            ConfidenceLevel = "HIGH",
            CollectedAt = now,
            Notes = "CRT-TEST-001 fictional demographic capture."
        };

        var campaign = new Campaign
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd1"),
            Name = "Phase 1 Foundation Campaign",
            Status = EntityStatuses.Draft,
            CreatedAt = now
        };

        var placementA = new CampaignPlacement
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd2"),
            CampaignId = campaign.Id,
            ContentItemId = ContentItem1Id,
            AdInventorySlotId = slots[0].Id,
            Status = EntityStatuses.Created
        };

        var placementB = new CampaignPlacement
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd3"),
            CampaignId = campaign.Id,
            ContentItemId = ContentItem1Id,
            AdInventorySlotId = slots[1].Id,
            Status = EntityStatuses.Created
        };

        var unknownCreator = new Creator
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111112"),
            Name = "Unknown Demographics Creator",
            CountryCode = "PH",
            CreatedAt = now,
            UpdatedAt = now,
            FemalePercentage = null,
            MalePercentage = null
        };

        _db.Creators.AddRange(creator, unknownCreator);
        _db.CreatorPlatforms.Add(platform);
        _db.ContentItems.AddRange(content1, content2);
        _db.AdInventorySlots.AddRange(slots);
        _db.Advertisers.Add(advertiser);
        _db.AdvertiserPrograms.Add(program);
        _db.AdvertiserOpportunities.AddRange(opportunityA, opportunityB, opportunityC);
        _db.AffiliateNetworks.Add(network);
        _db.NetworkAccesses.Add(networkAccess);
        _db.ProgramAccesses.Add(programAccess);
        _db.RuleVersions.Add(rule1);
        _db.BlissMatches.AddRange(matchA, matchB, matchC);
        _db.MatchScoreComponents.Add(scoreComponent);
        _db.EligibilityChecks.Add(eligibilityCheck);
        _db.DataProvenances.Add(provenance);
        _db.Campaigns.Add(campaign);
        _db.CampaignPlacements.AddRange(placementA, placementB);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
