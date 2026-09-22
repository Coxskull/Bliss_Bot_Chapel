using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bliss.Api.Contracts;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase8ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase8ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Commit_replay_eligibility_and_revoke_happy_path()
    {
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString("N")[..12];
        var seeded = await SeedEligibleWorkspaceAsync(key);

        var eligibility = await client.GetFromJsonAsync<WeddingPlannerCampaignReadinessEligibilityDto>(
            $"/api/wedding-planner/workspaces/{seeded.WorkspaceId}/campaign-readiness/eligibility");
        Assert.NotNull(eligibility);
        Assert.True(eligibility!.IsCleanAccepted);
        Assert.True(eligibility.HasCleanAcceptDecision);
        Assert.Contains(eligibility.Candidates, c => c.BlissMatchId == seeded.MatchId);
        Assert.DoesNotContain(
            await client.GetStringAsync($"/api/wedding-planner/workspaces/{seeded.WorkspaceId}/campaign-readiness/eligibility"),
            "\"url\"",
            StringComparison.OrdinalIgnoreCase);

        var body = new CommitWeddingPlannerCampaignReadinessHandshakeRequest(
            seeded.MatchId,
            seeded.CampaignId,
            seeded.ContentId,
            seeded.SlotId,
            "Clean ACCEPTED QA bound to approved match inventory.",
            true,
            true,
            "DashboardFixture",
            $"cr-{key}");
        var create = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{seeded.WorkspaceId}/campaign-readiness-handshakes",
            body);
        var created = await create.Content.ReadFromJsonAsync<WeddingPlannerCampaignReadinessHandshakeDto>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.False(created!.IsReplay);
        Assert.Equal("CAMPAIGN_READY", created.Status);
        Assert.Contains(WeddingPlannerCampaignReadinessHandshakeDisclaimer.Text, created.DocumentJson, StringComparison.Ordinal);

        var replay = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{seeded.WorkspaceId}/campaign-readiness-handshakes",
            body);
        var replayed = await replay.Content.ReadFromJsonAsync<WeddingPlannerCampaignReadinessHandshakeDto>();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replayed!.IsReplay);
        Assert.Equal(created.CampaignReadinessHandshakeVersionId, replayed.CampaignReadinessHandshakeVersionId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var runsAfter = await db.WeddingPlannerAgentRuns.CountAsync(x => x.WorkspaceId == seeded.WorkspaceId);
            // Commit must not create any additional agent runs beyond Phase 2–7 seed path.
            Assert.Equal(seeded.AgentRunCountAfterSeed, runsAfter);
            Assert.Equal(1, await db.WeddingPlannerCampaignReadinessHandshakeVersions.CountAsync(x =>
                x.WorkspaceId == seeded.WorkspaceId));
        }

        var revoke = await client.PostAsJsonAsync(
            $"/api/wedding-planner/campaign-readiness-handshakes/{created.CampaignReadinessHandshakeVersionId}/decisions",
            new WeddingPlannerCampaignReadinessDecisionRequest(
                "REVOKE_CAMPAIGN_READY",
                "Planning intent withdrawn.",
                "DashboardFixture",
                $"cr-rev-{key}"));
        var revoked = await revoke.Content.ReadFromJsonAsync<WeddingPlannerCampaignReadinessDecisionDto>();
        Assert.Equal(HttpStatusCode.Created, revoke.StatusCode);
        Assert.Equal("REVOKE_CAMPAIGN_READY", revoked!.Decision);
        Assert.Equal("REVOKED", revoked.Version.Status);
    }

    [Fact]
    public async Task Exception_qa_and_forbidden_fields_return_bad_request()
    {
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString("N")[..12];
        var seeded = await SeedEligibleWorkspaceAsync(key, acceptClean: false);

        var response = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{seeded.WorkspaceId}/campaign-readiness-handshakes",
            new CommitWeddingPlannerCampaignReadinessHandshakeRequest(
                seeded.MatchId,
                seeded.CampaignId,
                seeded.ContentId,
                seeded.SlotId,
                "should fail",
                true,
                true,
                "DashboardFixture",
                $"cr-ex-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var clean = await SeedEligibleWorkspaceAsync(key + "b");
        var forbidden = await client.PostAsync(
            $"/api/wedding-planner/workspaces/{clean.WorkspaceId}/campaign-readiness-handshakes",
            new StringContent(
                """{"blissMatchId":"00000000-0000-0000-0000-000000000001","campaignId":"00000000-0000-0000-0000-000000000002","contentItemId":"00000000-0000-0000-0000-000000000003","adInventorySlotId":"00000000-0000-0000-0000-000000000004","rationale":"x","disclaimerAcknowledged":true,"sourceSystem":"TEST","idempotencyKey":"k","isAvailable":false}""",
                Encoding.UTF8,
                "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);
    }

    private async Task<SeededApiGraph> SeedEligibleWorkspaceAsync(string key, bool acceptClean = true)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"P8 API {key}", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var setupAi = new LocalDeterministicWeddingPlannerAiProvider();
        var planner = new WeddingPlannerService(db);
        var orchestration = new WeddingPlannerOrchestrationService(db, planner, setupAi);
        var colorService = new WeddingPlannerColorIntelligenceService(db, planner);
        var curator = new WeddingPlannerCuratorOrchestrationService(
            db,
            planner,
            new LocalDeterministicWeddingPlannerResearchProvider(),
            setupAi,
            Options.Create(new WeddingPlannerResearchOptions { Provider = WeddingPlannerResearchProviderKinds.Local }));
        var workshop = new WeddingPlannerConceptWorkshopOrchestrationService(
            db,
            planner,
            setupAi,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }));
        var creative = new WeddingPlannerCreativeDepartmentOrchestrationService(
            db,
            planner,
            setupAi,
            new LocalDeterministicWeddingPlannerCreativeAssetProvider(),
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }),
            Options.Create(new WeddingPlannerCreativeAssetOptions
            {
                Provider = WeddingPlannerCreativeAssetProviderKinds.Local
            }));
        var qa = new WeddingPlannerQaReviewOrchestrationService(
            db,
            planner,
            setupAi,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }));

        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", $"api-{key}-open", true, null, "OPERATOR", "tester", "req-open");
        var session = await planner.CreateSessionAsync(
            workspace.WorkspaceId, "TEST", $"api-{key}-session", true, null, "OPERATOR", "tester", "req-session");
        await orchestration.ExecuteConciergeTurnAsync(
            session.SessionId, "voice: calm. audience: couples.", "TEST", $"api-{key}-turn",
            true, null, "OPERATOR", "tester", "req-turn");
        var dna = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", $"api-{key}-dna", true, null, "OPERATOR", "tester", "req-dna");
        await orchestration.DecideBrandDnaAsync(
            dna.BrandDnaVersionId, "APPROVE", "ok", "TEST", $"api-{key}-dna-dec",
            true, null, "OPERATOR", "tester", "req-dna-dec");
        var color = await colorService.ComputeAsync(
            workspace.WorkspaceId, "#336699", null, null, null, null, null,
            "TEST", $"api-{key}-color", true, null, "OPERATOR", "tester", "req-color");
        await colorService.DecideAsync(
            color.ColorProfileVersionId, "APPROVE", "ok", "TEST", $"api-{key}-color-dec",
            true, null, "OPERATOR", "tester", "req-color-dec");
        var researchJob = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "Topic", "Objective", ["Q1"], "Manila", "en", null,
            "TEST", $"api-{key}-research", true, null, "OPERATOR", "tester", "req-research");
        await curator.DecideAsync(
            researchJob.OutputResearchReportVersionId!.Value, "APPROVE", "ok", "TEST", $"api-{key}-research-dec",
            true, null, "OPERATOR", "tester", "req-research-dec");
        var workshopJob = await workshop.CreateWorkshopJobAsync(
            workspace.WorkspaceId,
            "Objective for calm social",
            "Grow venue inquiries",
            "Engaged couples",
            WeddingPlannerChannelFormats.StaticSocialSquare,
            ["Static square creative"],
            "Start planning",
            Array.Empty<string>(),
            null, null, null,
            "TEST",
            $"api-{key}-workshop",
            true, null, "OPERATOR", "tester", "req-workshop");
        await workshop.DecideAsync(
            workshopJob.OutputConceptPackageVersionId!.Value,
            "APPROVE",
            "Concept 1 fits",
            "concept_1",
            "TEST",
            $"api-{key}-concept-dec",
            true, null, "OPERATOR", "tester", "req-concept-dec");
        var creativeJob = await creative.CreateCreativeProductionJobAsync(
            workspace.WorkspaceId,
            WeddingPlannerCreativeProductionJobKinds.Initial,
            "Draft calm square",
            [WeddingPlannerChannelFormats.StaticSocialSquare],
            1,
            null, null, null, null, null,
            "TEST",
            $"api-{key}-creative",
            true, null, "OPERATOR", "tester", "req-creative");
        await creative.DecideAsync(
            creativeJob.OutputCreativePackageVersionId!.Value,
            WeddingPlannerCreativePackageDecisions.Approve,
            "Variant 1",
            "variant_1",
            "TEST",
            $"api-{key}-creative-dec",
            true, null, "OPERATOR", "tester", "req-creative-dec");
        var qaJob = await qa.CreateQaReviewJobAsync(
            workspace.WorkspaceId,
            "Control review selected variant",
            [WeddingPlannerQaFocusAreas.Copy, WeddingPlannerQaFocusAreas.Visual, WeddingPlannerQaFocusAreas.Provenance],
            null, null,
            "TEST",
            $"api-{key}-qa",
            true, null, "OPERATOR", "tester", "req-qa");

        if (acceptClean)
        {
            await qa.DecideQaReviewReportAsync(
                qaJob.OutputQaReviewReportVersionId!.Value,
                WeddingPlannerQaReviewDecisions.Accept,
                "Looks good",
                "variant_1",
                true, true, true, true,
                null, null,
                "TEST",
                $"api-{key}-qa-accept",
                true, null, true,
                "OPERATOR", "tester", "req-qa-accept");
        }
        else
        {
            var escalate = await qa.DecideQaReviewReportAsync(
                qaJob.OutputQaReviewReportVersionId!.Value,
                WeddingPlannerQaReviewDecisions.Escalate,
                "Need steward",
                "variant_1",
                null, null, null, null,
                WeddingPlannerQaEscalationCategories.ClaimBoundary,
                null,
                "TEST",
                $"api-{key}-qa-esc",
                true, null, true,
                "OPERATOR", "tester", "req-qa-esc");
            var blockers = WeddingPlannerQaReviewValidation.ExtractBlockerCodes(qaJob.RulesFindingsJson!);
            await qa.ResolveEscalationCaseAsync(
                escalate.EscalationCaseId!.Value,
                WeddingPlannerQaEscalationResolutions.WaiveAndAccept,
                "Operator exception",
                "Documented temporary waiver",
                true,
                blockers.ToArray(),
                null,
                "TEST",
                $"api-{key}-qa-waive",
                true, null, true, true,
                "OPERATOR", "tester", "req-qa-waive");
        }

        var creator = new Creator
        {
            Id = Guid.NewGuid(),
            Name = $"Creator {key}",
            CreatedAt = now,
            UpdatedAt = now
        };
        var program = new AdvertiserProgram
        {
            Id = Guid.NewGuid(),
            AdvertiserId = advertiser.Id,
            Name = "API Program",
            Status = EntityStatuses.Active
        };
        var opportunity = new AdvertiserOpportunity
        {
            Id = Guid.NewGuid(),
            AdvertiserProgramId = program.Id,
            Name = "API Opp",
            Status = EntityStatuses.Active
        };
        var rule = new RuleVersion
        {
            Id = Guid.NewGuid(),
            Version = $"api-{key}",
            Name = "API Rules",
            IsActive = true,
            CreatedAt = now
        };
        var match = new BlissMatch
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            AdvertiserOpportunityId = opportunity.Id,
            RuleVersionId = rule.Id,
            Status = EntityStatuses.Approved,
            CreatedAt = now
        };
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "API Draft",
            Status = EntityStatuses.Draft,
            CreatedAt = now
        };
        var content = new ContentItem
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            ContentType = "VIDEO",
            Title = "API Content",
            CreatedAt = now
        };
        var slot = new AdInventorySlot
        {
            Id = Guid.NewGuid(),
            ContentItemId = content.Id,
            SlotType = InventorySlotTypes.PreRoll,
            IsAvailable = false
        };
        db.Creators.Add(creator);
        db.AdvertiserPrograms.Add(program);
        db.AdvertiserOpportunities.Add(opportunity);
        db.RuleVersions.Add(rule);
        db.BlissMatches.Add(match);
        db.Campaigns.Add(campaign);
        db.ContentItems.Add(content);
        db.AdInventorySlots.Add(slot);
        await db.SaveChangesAsync();

        var agentRuns = await db.WeddingPlannerAgentRuns.CountAsync(x => x.WorkspaceId == workspace.WorkspaceId);
        return new SeededApiGraph(workspace.WorkspaceId, advertiser.Id, match.Id, campaign.Id, content.Id, slot.Id, agentRuns);
    }

    private sealed record SeededApiGraph(
        Guid WorkspaceId,
        Guid AdvertiserId,
        Guid MatchId,
        Guid CampaignId,
        Guid ContentId,
        Guid SlotId,
        int AgentRunCountAfterSeed);
}

public sealed class WeddingPlannerPhase8IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase8IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Operator_commit_advertiser_reviewer_viewer_forbidden_write_and_read_policy()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var key = Guid.NewGuid().ToString("N")[..10];
        var graph = await WeddingPlannerPhase8ApiIsolationSeed.SeedAsync(db, key);

        var operatorClient = await CreateRoleClientAsync("bliss.operator");
        var advertiser = await CreateAdvertiserClientAsync(graph.AdvertiserId, "Adv");
        var reviewer = await CreateRoleClientAsync("bliss.reviewer");
        var viewer = await CreateRoleClientAsync("bliss.viewer");

        var body = new CommitWeddingPlannerCampaignReadinessHandshakeRequest(
            graph.MatchId,
            graph.CampaignId,
            graph.ContentId,
            graph.SlotId,
            "operator commit",
            true,
            true,
            "OIDC",
            $"oidc-{key}");

        Assert.Equal(HttpStatusCode.Forbidden, (await advertiser.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{graph.WorkspaceId}/campaign-readiness-handshakes", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{graph.WorkspaceId}/campaign-readiness-handshakes", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{graph.WorkspaceId}/campaign-readiness-handshakes", body)).StatusCode);

        var created = await operatorClient.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{graph.WorkspaceId}/campaign-readiness-handshakes", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var handshake = await created.Content.ReadFromJsonAsync<WeddingPlannerCampaignReadinessHandshakeDto>();

        Assert.Equal(HttpStatusCode.OK, (await advertiser.GetAsync(
            $"/api/wedding-planner/workspaces/{graph.WorkspaceId}/campaign-readiness/eligibility")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.GetAsync(
            $"/api/wedding-planner/campaign-readiness-handshakes/{handshake!.CampaignReadinessHandshakeVersionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync(
            $"/api/wedding-planner/workspaces/{graph.WorkspaceId}/campaign-readiness/eligibility")).StatusCode);

        var otherAdv = await CreateAdvertiserClientAsync(Guid.NewGuid(), "Other");
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdv.GetAsync(
            $"/api/wedding-planner/workspaces/{graph.WorkspaceId}/campaign-readiness/eligibility")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await advertiser.PostAsJsonAsync(
            $"/api/wedding-planner/campaign-readiness-handshakes/{handshake.CampaignReadinessHandshakeVersionId}/decisions",
            new WeddingPlannerCampaignReadinessDecisionRequest(
                "REVOKE_CAMPAIGN_READY", "no", "OIDC", $"oidc-rev-{key}"))).StatusCode);
    }

    private async Task<HttpClient> CreateAdvertiserClientAsync(Guid advertiserId, string name)
    {
        var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.advertiser");
        client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiserId.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session?.CsrfToken);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
        return client;
    }

    private async Task<HttpClient> CreateRoleClientAsync(string role)
    {
        var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", role);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session?.CsrfToken);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
        return client;
    }
}

internal static class WeddingPlannerPhase8ApiIsolationSeed
{
    public static async Task<(Guid WorkspaceId, Guid AdvertiserId, Guid MatchId, Guid CampaignId, Guid ContentId, Guid SlotId)> SeedAsync(
        BlissDbContext db,
        string key)
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"OIDC {key}", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var setupAi = new LocalDeterministicWeddingPlannerAiProvider();
        var planner = new WeddingPlannerService(db);
        var orchestration = new WeddingPlannerOrchestrationService(db, planner, setupAi);
        var colorService = new WeddingPlannerColorIntelligenceService(db, planner);
        var curator = new WeddingPlannerCuratorOrchestrationService(
            db,
            planner,
            new LocalDeterministicWeddingPlannerResearchProvider(),
            setupAi,
            Options.Create(new WeddingPlannerResearchOptions { Provider = WeddingPlannerResearchProviderKinds.Local }));
        var workshop = new WeddingPlannerConceptWorkshopOrchestrationService(
            db,
            planner,
            setupAi,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }));
        var creative = new WeddingPlannerCreativeDepartmentOrchestrationService(
            db,
            planner,
            setupAi,
            new LocalDeterministicWeddingPlannerCreativeAssetProvider(),
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }),
            Options.Create(new WeddingPlannerCreativeAssetOptions
            {
                Provider = WeddingPlannerCreativeAssetProviderKinds.Local
            }));
        var qa = new WeddingPlannerQaReviewOrchestrationService(
            db,
            planner,
            setupAi,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }));

        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", $"oidc-{key}-open", true, null, "OPERATOR", "tester", "req-open");
        var session = await planner.CreateSessionAsync(
            workspace.WorkspaceId, "TEST", $"oidc-{key}-session", true, null, "OPERATOR", "tester", "req-session");
        await orchestration.ExecuteConciergeTurnAsync(
            session.SessionId, "voice: calm. audience: couples.", "TEST", $"oidc-{key}-turn",
            true, null, "OPERATOR", "tester", "req-turn");
        var dna = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", $"oidc-{key}-dna", true, null, "OPERATOR", "tester", "req-dna");
        await orchestration.DecideBrandDnaAsync(
            dna.BrandDnaVersionId, "APPROVE", "ok", "TEST", $"oidc-{key}-dna-dec",
            true, null, "OPERATOR", "tester", "req-dna-dec");
        var color = await colorService.ComputeAsync(
            workspace.WorkspaceId, "#336699", null, null, null, null, null,
            "TEST", $"oidc-{key}-color", true, null, "OPERATOR", "tester", "req-color");
        await colorService.DecideAsync(
            color.ColorProfileVersionId, "APPROVE", "ok", "TEST", $"oidc-{key}-color-dec",
            true, null, "OPERATOR", "tester", "req-color-dec");
        var researchJob = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "Topic", "Objective", ["Q1"], "Manila", "en", null,
            "TEST", $"oidc-{key}-research", true, null, "OPERATOR", "tester", "req-research");
        await curator.DecideAsync(
            researchJob.OutputResearchReportVersionId!.Value, "APPROVE", "ok", "TEST", $"oidc-{key}-research-dec",
            true, null, "OPERATOR", "tester", "req-research-dec");
        var workshopJob = await workshop.CreateWorkshopJobAsync(
            workspace.WorkspaceId,
            "Objective for calm social",
            "Grow venue inquiries",
            "Engaged couples",
            WeddingPlannerChannelFormats.StaticSocialSquare,
            ["Static square creative"],
            "Start planning",
            Array.Empty<string>(),
            null, null, null,
            "TEST",
            $"oidc-{key}-workshop",
            true, null, "OPERATOR", "tester", "req-workshop");
        await workshop.DecideAsync(
            workshopJob.OutputConceptPackageVersionId!.Value,
            "APPROVE",
            "Concept 1 fits",
            "concept_1",
            "TEST",
            $"oidc-{key}-concept-dec",
            true, null, "OPERATOR", "tester", "req-concept-dec");
        var creativeJob = await creative.CreateCreativeProductionJobAsync(
            workspace.WorkspaceId,
            WeddingPlannerCreativeProductionJobKinds.Initial,
            "Draft calm square",
            [WeddingPlannerChannelFormats.StaticSocialSquare],
            1,
            null, null, null, null, null,
            "TEST",
            $"oidc-{key}-creative",
            true, null, "OPERATOR", "tester", "req-creative");
        await creative.DecideAsync(
            creativeJob.OutputCreativePackageVersionId!.Value,
            WeddingPlannerCreativePackageDecisions.Approve,
            "Variant 1",
            "variant_1",
            "TEST",
            $"oidc-{key}-creative-dec",
            true, null, "OPERATOR", "tester", "req-creative-dec");
        var qaJob = await qa.CreateQaReviewJobAsync(
            workspace.WorkspaceId,
            "Control review selected variant",
            [WeddingPlannerQaFocusAreas.Copy, WeddingPlannerQaFocusAreas.Visual, WeddingPlannerQaFocusAreas.Provenance],
            null, null,
            "TEST",
            $"oidc-{key}-qa",
            true, null, "OPERATOR", "tester", "req-qa");
        await qa.DecideQaReviewReportAsync(
            qaJob.OutputQaReviewReportVersionId!.Value,
            WeddingPlannerQaReviewDecisions.Accept,
            "Looks good",
            "variant_1",
            true, true, true, true,
            null, null,
            "TEST",
            $"oidc-{key}-qa-accept",
            true, null, true,
            "OPERATOR", "tester", "req-qa-accept");

        var creator = new Creator { Id = Guid.NewGuid(), Name = "OIDC Creator", CreatedAt = now, UpdatedAt = now };
        var program = new AdvertiserProgram
        {
            Id = Guid.NewGuid(),
            AdvertiserId = advertiser.Id,
            Name = "OIDC Program",
            Status = EntityStatuses.Active
        };
        var opportunity = new AdvertiserOpportunity
        {
            Id = Guid.NewGuid(),
            AdvertiserProgramId = program.Id,
            Name = "OIDC Opp",
            Status = EntityStatuses.Active
        };
        var rule = new RuleVersion
        {
            Id = Guid.NewGuid(),
            Version = $"oidc-{key}",
            Name = "OIDC Rules",
            IsActive = true,
            CreatedAt = now
        };
        var match = new BlissMatch
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            AdvertiserOpportunityId = opportunity.Id,
            RuleVersionId = rule.Id,
            Status = EntityStatuses.Approved,
            CreatedAt = now
        };
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = "OIDC Draft",
            Status = EntityStatuses.Draft,
            CreatedAt = now
        };
        var content = new ContentItem
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            ContentType = "VIDEO",
            Title = "OIDC Content",
            CreatedAt = now
        };
        var slot = new AdInventorySlot
        {
            Id = Guid.NewGuid(),
            ContentItemId = content.Id,
            SlotType = InventorySlotTypes.PreRoll,
            IsAvailable = true
        };
        db.Creators.Add(creator);
        db.AdvertiserPrograms.Add(program);
        db.AdvertiserOpportunities.Add(opportunity);
        db.RuleVersions.Add(rule);
        db.BlissMatches.Add(match);
        db.Campaigns.Add(campaign);
        db.ContentItems.Add(content);
        db.AdInventorySlots.Add(slot);
        await db.SaveChangesAsync();

        return (workspace.WorkspaceId, advertiser.Id, match.Id, campaign.Id, content.Id, slot.Id);
    }
}

public sealed class WeddingPlannerPhase8BoundaryTests
{
    [Fact]
    public void Phase8_sources_have_zero_ai_provider_references_and_no_agent_run_api()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerCampaignReadinessRulesEngine.cs"),
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerCampaignReadinessValidation.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerCampaignReadinessOrchestrationService.cs")
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("IWeddingPlannerAiProvider", source, StringComparison.Ordinal);
            Assert.DoesNotContain("WeddingPlannerAgentRun", source, StringComparison.Ordinal);
        }

        var controller = File.ReadAllText(Path.Combine(root, "Bliss.Api", "Controllers", "WeddingPlannerController.cs"));
        Assert.Contains("campaign-readiness-handshakes", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("campaign-readiness-agent-runs", controller, StringComparison.Ordinal);
        Assert.Contains("CanCommitCampaignReadiness", controller, StringComparison.Ordinal);
        Assert.Contains("CanReadCampaignReadiness", controller, StringComparison.Ordinal);

        var access = File.ReadAllText(Path.Combine(root, "Bliss.Api", "Security", "WeddingPlannerAccess.cs"));
        Assert.Contains("do not widen generic write", access, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CanCommitCampaignReadiness", access, StringComparison.Ordinal);

        var frontendTouched = Directory.EnumerateFiles(
                Path.Combine(root, "frontend"), "*", SearchOption.AllDirectories)
            .Where(f => f.Contains("campaign-readiness", StringComparison.OrdinalIgnoreCase)
                        || f.Contains("CampaignReadiness", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(frontendTouched);
    }
}
