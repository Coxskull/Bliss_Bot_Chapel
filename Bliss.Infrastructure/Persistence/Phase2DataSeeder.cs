using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Additive fictional Phase 2 graph. Does not delete Phase 1 rows or call external APIs.
/// </summary>
public sealed class Phase2DataSeeder
{
    public static readonly Guid Advertiser2Id = Guid.Parse("33333333-3333-3333-3333-333333333334");
    public static readonly Guid Program2Id = Guid.Parse("44444444-4444-4444-4444-444444444446");
    public static readonly Guid OpportunityBrId = Guid.Parse("55555555-5555-5555-5555-555555555554");
    public static readonly Guid OpportunityPhId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid BrazilCreatorId = Guid.Parse("11111111-1111-1111-1111-111111111113");
    public static readonly Guid BrazilContentId = Guid.Parse("22222222-2222-2222-2222-222222222223");
    public static readonly Guid RuleVersion2Id = Guid.Parse("66666666-6666-6666-6666-666666666662");
    public static readonly Guid MatchGeoFailId = Guid.Parse("77777777-7777-7777-7777-777777777775");
    public static readonly Guid MatchBrazilApprovedId = Guid.Parse("77777777-7777-7777-7777-777777777776");
    public static readonly Guid MatchUnknownReviewId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid MatchSecondAdvertiserId = Guid.Parse("77777777-7777-7777-7777-777777777778");
    public static readonly Guid Campaign2Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd4");

    public static readonly string RuleDocumentJson = """
        {
          "minAudienceSize": 10000,
          "requireOpportunityMarketCountry": true,
          "requireLanguageOverlap": true,
          "prohibitedCategories": [ "Alcohol" ],
          "weights": {
            "geography": 0.4,
            "audienceSize": 0.3,
            "language": 0.3
          }
        }
        """;

    private readonly BlissDbContext _db;

    public Phase2DataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Advertisers.AnyAsync(x => x.Id == Advertiser2Id, cancellationToken))
        {
            return;
        }

        if (!await _db.Creators.AnyAsync(x => x.Id == Phase1DataSeeder.CreatorId, cancellationToken))
        {
            throw new InvalidOperationException("Phase 2 seed requires Phase 1 seed data.");
        }

        var now = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var rules = DeterministicRuleEvaluator.ParseDocument(RuleDocumentJson);

        var advertiser2 = new Advertiser
        {
            Id = Advertiser2Id,
            Name = "Harbor Audio Labs",
            Website = "https://example.test/harbor",
            CountryCode = "BR",
            Description = "Fictional Brazilian audio advertiser for Phase 2 global test data.",
            CreatedAt = now
        };

        var program2 = new AdvertiserProgram
        {
            Id = Program2Id,
            AdvertiserId = Advertiser2Id,
            Name = "Harbor Creator Studio",
            ExternalProgramId = "PRG-TEST-BR-001",
            Status = EntityStatuses.Active
        };

        var opportunityBr = new AdvertiserOpportunity
        {
            Id = OpportunityBrId,
            AdvertiserProgramId = Program2Id,
            Name = "Opportunity BR - Studio Kit",
            ProductName = "Studio Kit",
            Category = "Creator Tools",
            MarketCountryCode = "BR",
            Language = "Portuguese",
            CommissionPercentage = 8m,
            CommissionType = "PERCENTAGE",
            ExternalOpportunityId = "OPP-TEST-BR",
            Status = EntityStatuses.Active
        };

        var opportunityPhFromAdvertiser2 = new AdvertiserOpportunity
        {
            Id = OpportunityPhId,
            AdvertiserProgramId = Program2Id,
            Name = "Opportunity PH window - Clip Mic",
            ProductName = "Clip Mic",
            Category = "Creator Tools",
            MarketCountryCode = "PH",
            Language = "English",
            CommissionPercentage = 9m,
            CommissionType = "PERCENTAGE",
            ExternalOpportunityId = "OPP-TEST-PH-HARBOR",
            Status = EntityStatuses.Active
        };

        var brazilCreator = new Creator
        {
            Id = BrazilCreatorId,
            Name = "Test Creator Brazil",
            CountryCode = "BR",
            PrimaryLanguage = "Portuguese",
            AudienceSize = 80000,
            FemalePercentage = 55m,
            MalePercentage = 45m,
            PrimaryAgeRange = "18-34",
            PrimaryGeography = "São Paulo",
            EngagementLevel = "STRONG",
            CreatedAt = now,
            UpdatedAt = now
        };

        var brazilPlatform = new CreatorPlatform
        {
            Id = Guid.Parse("88888888-8888-8888-8888-888888888882"),
            CreatorId = BrazilCreatorId,
            Platform = "YouTube",
            ExternalProfileId = "CRT-TEST-BR-001",
            ProfileUrl = "https://example.test/creators/crt-test-br-001",
            Followers = 80000,
            LastCollectedAt = now
        };

        var brazilContent = new ContentItem
        {
            Id = BrazilContentId,
            CreatorId = BrazilCreatorId,
            ContentType = "VIDEO",
            Title = "Chapel Studio Diary 1",
            ExternalContentId = "CNT-TEST-BR-001",
            Url = "https://example.test/content/cnt-test-br-001",
            PublishedAt = now,
            CreatedAt = now
        };

        var overlaySlots = new[]
        {
            new AdInventorySlot
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999995"),
                ContentItemId = BrazilContentId,
                SlotType = InventorySlotTypes.PerimeterOverlay,
                StartSecond = 0,
                DurationSeconds = 60,
                IsAvailable = true
            },
            new AdInventorySlot
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999996"),
                ContentItemId = BrazilContentId,
                SlotType = InventorySlotTypes.CornerOverlay,
                StartSecond = 15,
                DurationSeconds = 20,
                IsAvailable = true
            },
            new AdInventorySlot
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999997"),
                ContentItemId = BrazilContentId,
                SlotType = InventorySlotTypes.RotatingOverlay,
                StartSecond = 30,
                DurationSeconds = 15,
                IsAvailable = true
            },
            new AdInventorySlot
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999998"),
                ContentItemId = BrazilContentId,
                SlotType = InventorySlotTypes.SponsoredSegment,
                StartSecond = 120,
                DurationSeconds = 90,
                IsAvailable = true
            }
        };

        var network2 = new AffiliateNetwork
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4"),
            Name = "Example Global Network",
            Website = "https://example.test/global-network",
            Status = EntityStatuses.Active
        };

        var networkAccessUnknown = new NetworkAccess
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5"),
            AdvertiserId = Advertiser2Id,
            AffiliateNetworkId = network2.Id,
            Status = EntityStatuses.Unknown
        };

        var programAccessApproved = new ProgramAccess
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6"),
            AdvertiserProgramId = Program2Id,
            Status = EntityStatuses.Approved,
            ApprovedAt = now
        };

        var rule2 = new RuleVersion
        {
            Id = RuleVersion2Id,
            Version = "2.0.0",
            Name = "Phase 2 Explicit Rules",
            Description = "Deterministic document used only by Phase 2 matches.",
            DocumentJson = RuleDocumentJson,
            IsActive = true,
            CreatedAt = now
        };

        var campaign2 = new Campaign
        {
            Id = Campaign2Id,
            Name = "Phase 2 Overlay Inventory Campaign",
            Status = EntityStatuses.Draft,
            CreatedAt = now
        };

        var placements = new[]
        {
            new CampaignPlacement
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd5"),
                CampaignId = Campaign2Id,
                ContentItemId = BrazilContentId,
                AdInventorySlotId = overlaySlots[0].Id,
                Status = EntityStatuses.Created
            },
            new CampaignPlacement
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd6"),
                CampaignId = Campaign2Id,
                ContentItemId = BrazilContentId,
                AdInventorySlotId = overlaySlots[1].Id,
                Status = EntityStatuses.Created
            }
        };

        var provenance = new DataProvenance
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"),
            EntityType = nameof(Creator),
            EntityId = BrazilCreatorId,
            FieldName = nameof(Creator.AudienceSize),
            SourceType = "PLATFORM",
            SourceName = "YouTube",
            SourceUrl = "https://example.test/creators/crt-test-br-001",
            ConfidenceLevel = "HIGH",
            CollectedAt = now,
            Notes = "CRT-TEST-BR-001 fictional audience capture."
        };

        var unknownCreatorId = Guid.Parse("11111111-1111-1111-1111-111111111112");
        var testCreatorId = Phase1DataSeeder.CreatorId;

        var matchGeoFail = new BlissMatch
        {
            Id = MatchGeoFailId,
            CreatorId = testCreatorId,
            AdvertiserOpportunityId = OpportunityBrId,
            RuleVersionId = RuleVersion2Id,
            CreatedAt = now
        };

        var matchBrazil = new BlissMatch
        {
            Id = MatchBrazilApprovedId,
            CreatorId = BrazilCreatorId,
            AdvertiserOpportunityId = OpportunityBrId,
            RuleVersionId = RuleVersion2Id,
            CreatedAt = now
        };

        var matchUnknown = new BlissMatch
        {
            Id = MatchUnknownReviewId,
            CreatorId = unknownCreatorId,
            AdvertiserOpportunityId = OpportunityPhId,
            RuleVersionId = RuleVersion2Id,
            CreatedAt = now
        };

        var matchSecondAdvertiser = new BlissMatch
        {
            Id = MatchSecondAdvertiserId,
            CreatorId = testCreatorId,
            AdvertiserOpportunityId = OpportunityPhId,
            RuleVersionId = RuleVersion2Id,
            CreatedAt = now
        };

        _db.Advertisers.Add(advertiser2);
        _db.AdvertiserPrograms.Add(program2);
        _db.AdvertiserOpportunities.AddRange(opportunityBr, opportunityPhFromAdvertiser2);
        _db.Creators.Add(brazilCreator);
        _db.CreatorPlatforms.Add(brazilPlatform);
        _db.ContentItems.Add(brazilContent);
        _db.AdInventorySlots.AddRange(overlaySlots);
        _db.AffiliateNetworks.Add(network2);
        _db.NetworkAccesses.Add(networkAccessUnknown);
        _db.ProgramAccesses.Add(programAccessApproved);
        _db.RuleVersions.Add(rule2);
        _db.Campaigns.Add(campaign2);
        _db.CampaignPlacements.AddRange(placements);
        _db.DataProvenances.Add(provenance);
        _db.BlissMatches.AddRange(matchGeoFail, matchBrazil, matchUnknown, matchSecondAdvertiser);
        await _db.SaveChangesAsync(cancellationToken);

        var testCreator = await LoadCreator(testCreatorId, cancellationToken);
        var unknownCreator = await LoadCreator(unknownCreatorId, cancellationToken);

        PersistEvaluation(matchGeoFail, testCreator, opportunityBr, rules);
        PersistEvaluation(matchBrazil, brazilCreator, opportunityBr, rules);
        PersistEvaluation(matchUnknown, unknownCreator, opportunityPhFromAdvertiser2, rules);
        PersistEvaluation(matchSecondAdvertiser, testCreator, opportunityPhFromAdvertiser2, rules);

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Creator> LoadCreator(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Creators.SingleAsync(x => x.Id == id, cancellationToken);
    }

    private void PersistEvaluation(
        BlissMatch match,
        Creator creator,
        AdvertiserOpportunity opportunity,
        RuleDocument rules)
    {
        var result = DeterministicRuleEvaluator.Evaluate(creator, opportunity, rules);
        match.Status = result.MatchStatus;
        match.OverallScore = result.OverallScore;
        match.ConfidenceScore = result.ConfidenceScore;
        _db.EligibilityChecks.AddRange(BuildEligibility(match.Id, result));
        _db.MatchScoreComponents.AddRange(BuildScores(match.Id, result));
    }

    public static IEnumerable<EligibilityCheck> BuildEligibility(Guid matchId, RuleEvaluationResult result)
    {
        return result.Checks.Select(c => new EligibilityCheck
        {
            Id = Guid.NewGuid(),
            BlissMatchId = matchId,
            CheckType = c.CheckType,
            Result = c.Result,
            ReasonCode = c.ReasonCode,
            Explanation = c.Explanation
        });
    }

    public static IEnumerable<MatchScoreComponent> BuildScores(Guid matchId, RuleEvaluationResult result)
    {
        return result.Scores.Select(s => new MatchScoreComponent
        {
            Id = Guid.NewGuid(),
            BlissMatchId = matchId,
            ComponentName = s.ComponentName,
            Score = s.Score,
            Weight = s.Weight,
            Explanation = s.Explanation
        });
    }
}
