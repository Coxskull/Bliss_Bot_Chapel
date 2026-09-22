using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase9PersistenceTests
{
    [Fact]
    public async Task Happy_path_exactly_two_runs_three_contributions_and_no_upstream_mutation()
    {
        await using var db = await SeedBlissPlacementGraphAsync();
        var seeded = await SeedHandshakeAsync(db);
        var ai = new LocalDeterministicWeddingPlannerAiProvider();
        var ml = CreateMl(db, ai);

        var beforePlacement = await db.CampaignPlacements.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.PlacementId);
        var beforeHandshake = await db.WeddingPlannerCampaignReadinessHandshakeVersions.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.HandshakeId);

        var end = DateTime.UtcNow.AddDays(-1);
        var start = end.AddDays(-7);
        var job = await ml.CreateMeasurementLearningJobAsync(
            seeded.WorkspaceId,
            seeded.HandshakeId,
            start,
            end,
            "Operator spreadsheet",
            "OPS_SHEET",
            true,
            1000,
            40,
            4,
            80m,
            200m,
            "USD",
            "Human attested aggregates only.",
            null,
            "TEST",
            "ml-happy",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-ml-happy");

        Assert.Equal(WeddingPlannerMeasurementLearningJobStatuses.Succeeded, job.Status);
        Assert.NotNull(job.OutputMeasurementLearningReportVersionId);
        Assert.Equal(2, ai.InvokeCount);
        Assert.Equal(WeddingPlannerMeasurementLearningFindingSeverities.Pass, job.RulesOverallSeverity);

        var runs = await db.WeddingPlannerAgentRuns
            .Where(x => x.WorkspaceId == seeded.WorkspaceId
                        && WeddingPlannerMeasurementLearningWorkerProfiles.All.Contains(x.WorkerProfileVersion!))
            .OrderBy(x => x.StartedAt)
            .ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal(WeddingPlannerAgentRunStatuses.Succeeded, r.Status));
        Assert.Equal(1, runs.Count(r => r.OutputMeasurementLearningReportVersionId is not null));

        var contributions = await db.WeddingPlannerMeasurementLearningRoleContributions
            .Where(x => x.MeasurementLearningReportVersionId == job.OutputMeasurementLearningReportVersionId)
            .ToListAsync();
        Assert.Equal(3, contributions.Count);
        Assert.Equal(
            WeddingPlannerMeasurementLearningLogicalRoles.AllInOrder.ToArray(),
            contributions.OrderBy(c => Array.IndexOf(WeddingPlannerMeasurementLearningLogicalRoles.AllInOrder.ToArray(), c.LogicalRole))
                .Select(c => c.LogicalRole)
                .ToArray());

        var analyst = contributions.Single(c => c.LogicalRole == WeddingPlannerMeasurementLearningLogicalRoles.PerformanceAnalyst);
        var synthesizer = contributions.Single(c => c.LogicalRole == WeddingPlannerMeasurementLearningLogicalRoles.LearningSynthesizer);
        var advisor = contributions.Single(c => c.LogicalRole == WeddingPlannerMeasurementLearningLogicalRoles.OptimizationAdvisor);
        Assert.Equal(job.PerformanceAnalysisAgentRunId, analyst.ProducingAgentRunId);
        Assert.Equal(job.LearningSynthesisAgentRunId, synthesizer.ProducingAgentRunId);
        Assert.Equal(job.LearningSynthesisAgentRunId, advisor.ProducingAgentRunId);
        Assert.NotEqual(analyst.ProducingAgentRunId, synthesizer.ProducingAgentRunId);

        var report = await db.WeddingPlannerMeasurementLearningReportVersions
            .SingleAsync(x => x.Id == job.OutputMeasurementLearningReportVersionId);
        Assert.Equal(WeddingPlannerMeasurementLearningReportStatuses.Proposed, report.Status);
        Assert.Contains(WeddingPlannerMeasurementLearningMarkers.SyntheticDevelopmentMeasurementLearning, report.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerMeasurementLearningLabels.AssociationNotCausation, report.DocumentJson, StringComparison.Ordinal);
        Assert.Contains("\"ctr\":", report.MetricsJson, StringComparison.Ordinal);
        Assert.Contains("0.04", report.MetricsJson, StringComparison.Ordinal);

        var afterPlacement = await db.CampaignPlacements.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.PlacementId);
        var afterHandshake = await db.WeddingPlannerCampaignReadinessHandshakeVersions.AsNoTracking()
            .SingleAsync(x => x.Id == seeded.HandshakeId);
        Assert.Equal(beforePlacement.Status, afterPlacement.Status);
        Assert.Equal(EntityStatuses.Planned, afterPlacement.Status);
        Assert.Equal(beforeHandshake.Status, afterHandshake.Status);
        Assert.Equal(beforeHandshake.DocumentJson, afterHandshake.DocumentJson);
    }

    [Fact]
    public async Task Exact_replay_writes_nothing_and_invokes_no_ai()
    {
        await using var db = await SeedBlissPlacementGraphAsync();
        var seeded = await SeedHandshakeAsync(db);
        var ai = new LocalDeterministicWeddingPlannerAiProvider();
        var ml = CreateMl(db, ai);
        var end = DateTime.UtcNow.AddDays(-1);
        var start = end.AddDays(-3);

        var first = await CreateJobAsync(ml, seeded, start, end, "ml-replay");
        var invokeAfterFirst = ai.InvokeCount;
        var auditCount = await db.WeddingPlannerAuditEvents.CountAsync();
        var jobCount = await db.WeddingPlannerMeasurementLearningJobs.CountAsync();
        var reportCount = await db.WeddingPlannerMeasurementLearningReportVersions.CountAsync();
        var runCount = await db.WeddingPlannerAgentRuns.CountAsync();

        var replay = await CreateJobAsync(ml, seeded, start, end, "ml-replay");
        Assert.True(replay.IsReplay);
        Assert.Equal(first.MeasurementLearningJobId, replay.MeasurementLearningJobId);
        Assert.Equal(invokeAfterFirst, ai.InvokeCount);
        Assert.Equal(jobCount, await db.WeddingPlannerMeasurementLearningJobs.CountAsync());
        Assert.Equal(reportCount, await db.WeddingPlannerMeasurementLearningReportVersions.CountAsync());
        Assert.Equal(runCount, await db.WeddingPlannerAgentRuns.CountAsync());
        Assert.Equal(auditCount, await db.WeddingPlannerAuditEvents.CountAsync());
    }

    [Fact]
    public async Task Deterministic_block_fails_job_before_any_ai_call()
    {
        await using var db = await SeedBlissPlacementGraphAsync();
        var seeded = await SeedHandshakeAsync(db);
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == seeded.PlacementId);
        placement.Status = "ACTIVE";
        await db.SaveChangesAsync();

        var ai = new LocalDeterministicWeddingPlannerAiProvider();
        var ml = CreateMl(db, ai);
        var end = DateTime.UtcNow.AddDays(-1);
        var start = end.AddDays(-3);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJobAsync(ml, seeded, start, end, "ml-block"));
        Assert.Contains("ML_PLACEMENT_STILL_PLANNED", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, ai.InvokeCount);

        var job = await db.WeddingPlannerMeasurementLearningJobs.SingleAsync(x => x.IdempotencyKey == "ml-block");
        Assert.Equal(WeddingPlannerMeasurementLearningJobStatuses.Failed, job.Status);
        Assert.Null(job.OutputMeasurementLearningReportVersionId);
        Assert.Null(job.PerformanceAnalysisAgentRunId);
        Assert.Contains("ML_PLACEMENT_STILL_PLANNED", job.RulesFindingsJson, StringComparison.Ordinal);
        Assert.Equal(0, await db.WeddingPlannerMeasurementLearningReportVersions.CountAsync());
        Assert.Equal(0, await db.WeddingPlannerMeasurementLearningRoleContributions.CountAsync());
    }

    [Fact]
    public async Task Accept_sets_pointer_reject_does_not_and_later_accept_supersedes()
    {
        await using var db = await SeedBlissPlacementGraphAsync();
        var seeded = await SeedHandshakeAsync(db);
        var ai = new LocalDeterministicWeddingPlannerAiProvider();
        var ml = CreateMl(db, ai);
        var end = DateTime.UtcNow.AddDays(-1);
        var start = end.AddDays(-2);

        var rejectedJob = await CreateJobAsync(ml, seeded, start, end, "ml-reject");
        var reject = await ml.DecideMeasurementLearningReportAsync(
            rejectedJob.OutputMeasurementLearningReportVersionId!.Value,
            WeddingPlannerMeasurementLearningDecisions.Reject,
            "Not useful",
            null,
            "TEST",
            "ml-reject-dec",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-reject");
        Assert.Equal(WeddingPlannerMeasurementLearningReportStatuses.Rejected, reject.Version.Status);
        var ws = await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == seeded.WorkspaceId);
        Assert.Null(ws.CurrentAcceptedMeasurementLearningReportVersionId);

        var acceptedJob = await CreateJobAsync(ml, seeded, start, end, "ml-accept-1");
        var accept1 = await ml.DecideMeasurementLearningReportAsync(
            acceptedJob.OutputMeasurementLearningReportVersionId!.Value,
            WeddingPlannerMeasurementLearningDecisions.Accept,
            "Useful advisory report",
            null,
            "TEST",
            "ml-accept-1-dec",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-accept-1");
        Assert.Equal(WeddingPlannerMeasurementLearningReportStatuses.Accepted, accept1.Version.Status);
        await db.Entry(ws).ReloadAsync();
        Assert.Equal(acceptedJob.OutputMeasurementLearningReportVersionId, ws.CurrentAcceptedMeasurementLearningReportVersionId);

        var acceptedJob2 = await CreateJobAsync(ml, seeded, start, end, "ml-accept-2");
        var accept2 = await ml.DecideMeasurementLearningReportAsync(
            acceptedJob2.OutputMeasurementLearningReportVersionId!.Value,
            WeddingPlannerMeasurementLearningDecisions.Accept,
            "Newer advisory report",
            null,
            "TEST",
            "ml-accept-2-dec",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-accept-2");
        Assert.Equal(WeddingPlannerMeasurementLearningReportStatuses.Accepted, accept2.Version.Status);
        await db.Entry(ws).ReloadAsync();
        Assert.Equal(acceptedJob2.OutputMeasurementLearningReportVersionId, ws.CurrentAcceptedMeasurementLearningReportVersionId);

        var superseded = await db.WeddingPlannerMeasurementLearningReportVersions
            .SingleAsync(x => x.Id == acceptedJob.OutputMeasurementLearningReportVersionId);
        Assert.Equal(WeddingPlannerMeasurementLearningReportStatuses.Superseded, superseded.Status);
        Assert.Contains(WeddingPlannerMeasurementLearningReportDisclaimer.Text, superseded.DocumentJson, StringComparison.Ordinal);
    }

    private static async Task<WeddingPlannerMeasurementLearningJobResult> CreateJobAsync(
        WeddingPlannerMeasurementLearningOrchestrationService ml,
        SeededHandshake seeded,
        DateTime start,
        DateTime end,
        string key) =>
        await ml.CreateMeasurementLearningJobAsync(
            seeded.WorkspaceId,
            seeded.HandshakeId,
            start,
            end,
            "Operator spreadsheet",
            "OPS_SHEET",
            true,
            500,
            20,
            2,
            40m,
            100m,
            "USD",
            null,
            null,
            "TEST",
            key,
            true,
            null,
            "OPERATOR",
            "tester",
            "req-" + key);

    private static WeddingPlannerMeasurementLearningOrchestrationService CreateMl(
        BlissDbContext db,
        IWeddingPlannerAiProvider ai) =>
        new(
            db,
            new WeddingPlannerService(db),
            ai,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }));

    private static async Task<BlissDbContext> SeedBlissPlacementGraphAsync()
    {
        var postgresConnection = Environment.GetEnvironmentVariable("BLISS_PHASE9_POSTGRES");
        var db = string.IsNullOrWhiteSpace(postgresConnection)
            ? TestDb.CreateContext()
            : new BlissDbContext(
                new DbContextOptionsBuilder<BlissDbContext>()
                    .UseNpgsql(postgresConnection)
                    .Options);
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        return db;
    }

    private static async Task<SeededHandshake> SeedHandshakeAsync(BlissDbContext db, string keyPrefix = "p9")
    {
        // Build full Phase 1–8 eligible graph for measurement-learning prerequisites.
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"Phase9 Adv {keyPrefix}", CreatedAt = now };
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
        var cr = new WeddingPlannerCampaignReadinessOrchestrationService(
            db,
            planner,
            new CampaignPlacementService(db),
            new ExplicitWeddingPlannerCampaignReadinessHostEnvironment(true));

        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", $"{keyPrefix}-open", true, null, "OPERATOR", "tester", "req-open");
        var session = await planner.CreateSessionAsync(
            workspace.WorkspaceId, "TEST", $"{keyPrefix}-session", true, null, "OPERATOR", "tester", "req-session");
        await orchestration.ExecuteConciergeTurnAsync(
            session.SessionId, "voice: calm. audience: couples.", "TEST", $"{keyPrefix}-turn",
            true, null, "OPERATOR", "tester", "req-turn");
        var dna = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", $"{keyPrefix}-dna", true, null, "OPERATOR", "tester", "req-dna");
        await orchestration.DecideBrandDnaAsync(
            dna.BrandDnaVersionId, "APPROVE", "ok", "TEST", $"{keyPrefix}-dna-dec",
            true, null, "OPERATOR", "tester", "req-dna-dec");
        var color = await colorService.ComputeAsync(
            workspace.WorkspaceId, "#336699", null, null, null, null, null,
            "TEST", $"{keyPrefix}-color", true, null, "OPERATOR", "tester", "req-color");
        await colorService.DecideAsync(
            color.ColorProfileVersionId, "APPROVE", "ok", "TEST", $"{keyPrefix}-color-dec",
            true, null, "OPERATOR", "tester", "req-color-dec");
        var researchJob = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "Topic", "Objective", ["Q1"], "Manila", "en", null,
            "TEST", $"{keyPrefix}-research", true, null, "OPERATOR", "tester", "req-research");
        await curator.DecideAsync(
            researchJob.OutputResearchReportVersionId!.Value, "APPROVE", "ok", "TEST", $"{keyPrefix}-research-dec",
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
            $"{keyPrefix}-workshop",
            true, null, "OPERATOR", "tester", "req-workshop");
        await workshop.DecideAsync(
            workshopJob.OutputConceptPackageVersionId!.Value,
            "APPROVE",
            "Concept 1 fits",
            "concept_1",
            "TEST",
            $"{keyPrefix}-concept-dec",
            true, null, "OPERATOR", "tester", "req-concept-dec");
        var creativeJob = await creative.CreateCreativeProductionJobAsync(
            workspace.WorkspaceId,
            WeddingPlannerCreativeProductionJobKinds.Initial,
            "Draft calm square",
            [WeddingPlannerChannelFormats.StaticSocialSquare],
            1,
            null, null, null, null, null,
            "TEST",
            $"{keyPrefix}-creative",
            true, null, "OPERATOR", "tester", "req-creative");
        await creative.DecideAsync(
            creativeJob.OutputCreativePackageVersionId!.Value,
            WeddingPlannerCreativePackageDecisions.Approve,
            "Variant 1",
            "variant_1",
            "TEST",
            $"{keyPrefix}-creative-dec",
            true, null, "OPERATOR", "tester", "req-creative-dec");
        var qaJob = await qa.CreateQaReviewJobAsync(
            workspace.WorkspaceId,
            "Control review selected variant",
            [WeddingPlannerQaFocusAreas.Copy, WeddingPlannerQaFocusAreas.Visual, WeddingPlannerQaFocusAreas.Provenance],
            null, null,
            "TEST",
            $"{keyPrefix}-qa",
            true, null, "OPERATOR", "tester", "req-qa");
        await qa.DecideQaReviewReportAsync(
            qaJob.OutputQaReviewReportVersionId!.Value,
            WeddingPlannerQaReviewDecisions.Accept,
            "Looks good",
            "variant_1",
            true, true, true, true,
            null, null,
            "TEST",
            $"{keyPrefix}-qa-accept",
            true, null, true,
            "OPERATOR", "tester", "req-qa-accept");

        var creator = new Creator { Id = Guid.NewGuid(), Name = $"Phase9 Creator {keyPrefix}", CreatedAt = now, UpdatedAt = now };
        var program = new AdvertiserProgram { Id = Guid.NewGuid(), AdvertiserId = advertiser.Id, Name = $"Phase9 Program {keyPrefix}", Status = EntityStatuses.Active };
        var opportunity = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id, Name = $"Phase9 Opp {keyPrefix}", Status = EntityStatuses.Active };
        var rule = new RuleVersion { Id = Guid.NewGuid(), Version = $"{keyPrefix}-v1", Name = $"Phase9 Rules {keyPrefix}", IsActive = true, CreatedAt = now };
        var match = new BlissMatch
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            AdvertiserOpportunityId = opportunity.Id,
            RuleVersionId = rule.Id,
            Status = EntityStatuses.Approved,
            OverallScore = 0.91m,
            CreatedAt = now
        };
        var campaign = new Campaign { Id = Guid.NewGuid(), Name = $"Phase9 Draft {keyPrefix}", Status = EntityStatuses.Draft, AdvertiserOpportunityId = null, CreatedAt = now };
        var content = new ContentItem { Id = Guid.NewGuid(), CreatorId = creator.Id, ContentType = "VIDEO", Title = $"Phase9 Content {keyPrefix}", CreatedAt = now };
        var slot = new AdInventorySlot
        {
            Id = Guid.NewGuid(),
            ContentItemId = content.Id,
            SlotType = InventorySlotTypes.PreRoll,
            StartSecond = 5,
            DurationSeconds = 30,
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

        var handshake = await cr.CommitAsync(
            workspace.WorkspaceId,
            match.Id,
            campaign.Id,
            content.Id,
            slot.Id,
            "Clean ACCEPTED QA bound to approved match inventory.",
            true,
            true,
            null,
            "TEST",
            $"{keyPrefix}-handshake",
            true,
            true,
            null,
            "OPERATOR",
            "tester",
            "req-handshake");

        return new SeededHandshake(
            workspace.WorkspaceId,
            advertiser.Id,
            handshake.CampaignReadinessHandshakeVersionId,
            handshake.CampaignPlacementId);
    }

    private sealed record SeededHandshake(
        Guid WorkspaceId,
        Guid AdvertiserId,
        Guid HandshakeId,
        Guid PlacementId);
}
