using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase5PersistenceTests
{
    [Fact]
    public async Task Happy_path_creates_exactly_three_runs_and_four_contributions()
    {
        await using var db = CreateDb();
        var (workspace, workshop, ai) = await SeedWithPrerequisitesAsync(db);

        var job = await workshop.CreateWorkshopJobAsync(
            workspace.WorkspaceId,
            "Objective for calm social",
            "Grow venue inquiries",
            "Engaged couples",
            WeddingPlannerChannelFormats.StaticSocialSquare,
            ["Static square creative"],
            "Start planning",
            Array.Empty<string>(),
            null,
            null,
            null,
            "TEST",
            "job-happy-1",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-job");

        Assert.Equal(WeddingPlannerWorkshopJobStatuses.Succeeded, job.Status);
        Assert.False(job.IsReplay);
        Assert.NotNull(job.OutputConceptPackageVersionId);
        Assert.Equal(3, ai.InvokeCount);

        var runs = await db.WeddingPlannerAgentRuns
            .Where(x => x.WorkspaceId == workspace.WorkspaceId
                        && (x.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1
                            || x.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1
                            || x.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1))
            .OrderBy(x => x.StartedAt)
            .ToListAsync();

        Assert.Equal(3, runs.Count);
        Assert.All(runs, r => Assert.Equal(WeddingPlannerAgentRunStatuses.Succeeded, r.Status));
        Assert.Equal(1, runs.Count(r => r.OutputConceptPackageVersionId is not null));
        Assert.Equal(job.OutputConceptPackageVersionId, runs.Single(r => r.OutputConceptPackageVersionId is not null).OutputConceptPackageVersionId);

        var contributions = await db.WeddingPlannerConceptRoleContributions
            .Where(x => x.ConceptPackageVersionId == job.OutputConceptPackageVersionId)
            .ToListAsync();
        Assert.Equal(4, contributions.Count);
        Assert.Equal(
            WeddingPlannerConceptWorkshopLogicalRoles.AllInOrder.ToHashSet(StringComparer.Ordinal),
            contributions.Select(x => x.LogicalRole).ToHashSet(StringComparer.Ordinal));

        var package = await db.WeddingPlannerConceptPackageVersions.SingleAsync(x => x.Id == job.OutputConceptPackageVersionId);
        Assert.Equal(WeddingPlannerConceptPackageStatuses.Proposed, package.Status);
        Assert.Contains(WeddingPlannerConceptPackageDisclaimer.Text, package.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype, package.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(runs.Single(r => r.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1).Id, package.ProducingAgentRunId);
    }

    [Fact]
    public async Task Prerequisites_require_dna_color_and_research()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "No prereq", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();
        var planner = new WeddingPlannerService(db);
        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open", true, null, "OPERATOR", "tester", "req");
        var workshop = CreateWorkshop(db, new LocalDeterministicWeddingPlannerAiProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshop.CreateWorkshopJobAsync(
                workspace.WorkspaceId,
                "O", "G", "A", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["d"], "cta", null, null, null, null,
                "TEST", "no-prereq", true, null, "OPERATOR", "tester", "req"));
    }

    [Fact]
    public async Task Succeeded_and_failed_replay_do_not_retry_ai()
    {
        await using var db = CreateDb();
        var (workspace, workshop, ai) = await SeedWithPrerequisitesAsync(db);
        var first = await CreateJobAsync(workshop, workspace.WorkspaceId, "replay-ok");
        Assert.Equal(3, ai.InvokeCount);

        var replay = await CreateJobAsync(workshop, workspace.WorkspaceId, "replay-ok");
        Assert.True(replay.IsReplay);
        Assert.Equal(first.WorkshopJobId, replay.WorkshopJobId);
        Assert.Equal(3, ai.InvokeCount);

        await using var failDb = CreateDb();
        var failSeed = await SeedWithPrerequisitesAsync(failDb, ai: new StageFailingAiProvider(2));
        await Assert.ThrowsAsync<WeddingPlannerProviderException>(() =>
            CreateJobAsync(failSeed.Workshop, failSeed.Workspace.WorkspaceId, "replay-fail"));
        Assert.Equal(0, await failDb.WeddingPlannerConceptPackageVersions.CountAsync());
        Assert.Equal("FAILED", (await failDb.WeddingPlannerWorkshopJobs.SingleAsync()).Status);

        var failedReplay = await CreateJobAsync(failSeed.Workshop, failSeed.Workspace.WorkspaceId, "replay-fail");
        Assert.True(failedReplay.IsReplay);
        Assert.Equal(WeddingPlannerWorkshopJobStatuses.Failed, failedReplay.Status);
        Assert.Equal(0, await failDb.WeddingPlannerConceptPackageVersions.CountAsync());
    }

    [Fact]
    public async Task Mid_stage_failure_keeps_prior_runs_and_creates_no_package()
    {
        await using var db = CreateDb();
        var seeded = await SeedWithPrerequisitesAsync(db, ai: new StageFailingAiProvider(2));
        await Assert.ThrowsAsync<WeddingPlannerProviderException>(() =>
            CreateJobAsync(seeded.Workshop, seeded.Workspace.WorkspaceId, "mid-fail"));

        var runs = await db.WeddingPlannerAgentRuns
            .Where(x => x.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1
                        || x.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1
                        || x.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1)
            .ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.Contains(runs, r => r.Status == WeddingPlannerAgentRunStatuses.Succeeded);
        Assert.Contains(runs, r => r.Status == WeddingPlannerAgentRunStatuses.Failed);
        Assert.Equal(0, await db.WeddingPlannerConceptPackageVersions.CountAsync());
        Assert.Equal(0, await db.WeddingPlannerConceptRoleContributions.CountAsync());
    }

    [Fact]
    public async Task Approve_sets_pointer_and_selection_only_on_decision_reject_forbids_selection()
    {
        await using var db = CreateDb();
        var (workspace, workshop, _) = await SeedWithPrerequisitesAsync(db);
        var job = await CreateJobAsync(workshop, workspace.WorkspaceId, "decide-1");
        var packageId = job.OutputConceptPackageVersionId!.Value;
        var before = (await db.WeddingPlannerConceptPackageVersions.SingleAsync(x => x.Id == packageId)).DocumentJson;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshop.DecideAsync(packageId, "APPROVE", "ok", null, "TEST", "bad-ap",
                true, null, "OPERATOR", "tester", "req"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshop.DecideAsync(packageId, "REJECT", "nope", "concept_1", "TEST", "bad-rj",
                true, null, "OPERATOR", "tester", "req"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshop.DecideAsync(packageId, "APPROVE", " ", "concept_1", "TEST", "empty",
                true, null, "OPERATOR", "tester", "req"));

        var approved = await workshop.DecideAsync(
            packageId, "APPROVE", "Direction 1 fits", "concept_1", "TEST", "ap-1",
            true, null, "OPERATOR", "tester", "req-ap");
        Assert.Equal("concept_1", approved.SelectedConceptId);
        Assert.True(approved.Version.IsCurrentApproved);

        var package = await db.WeddingPlannerConceptPackageVersions.SingleAsync(x => x.Id == packageId);
        Assert.Equal(before, package.DocumentJson);
        Assert.Equal(WeddingPlannerConceptPackageStatuses.Approved, package.Status);
        Assert.Equal(packageId, (await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == workspace.WorkspaceId))
            .CurrentApprovedConceptPackageVersionId);

        var job2 = await CreateJobAsync(workshop, workspace.WorkspaceId, "decide-2");
        var approved2 = await workshop.DecideAsync(
            job2.OutputConceptPackageVersionId!.Value, "APPROVE", "Next", "concept_2", "TEST", "ap-2",
            true, null, "OPERATOR", "tester", "req-ap2");
        Assert.True(approved2.Version.IsCurrentApproved);
        package = await db.WeddingPlannerConceptPackageVersions.SingleAsync(x => x.Id == packageId);
        Assert.Equal(WeddingPlannerConceptPackageStatuses.Superseded, package.Status);
        Assert.Equal(before, package.DocumentJson);

        var job3 = await CreateJobAsync(workshop, workspace.WorkspaceId, "decide-3");
        var rejected = await workshop.DecideAsync(
            job3.OutputConceptPackageVersionId!.Value, "REJECT", "None fit", null, "TEST", "rj-1",
            true, null, "OPERATOR", "tester", "req-rj");
        Assert.Null(rejected.SelectedConceptId);
        Assert.False(rejected.Version.IsCurrentApproved);
        Assert.Equal(job2.OutputConceptPackageVersionId,
            (await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == workspace.WorkspaceId))
            .CurrentApprovedConceptPackageVersionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workshop.DecideAsync(packageId, "APPROVE", "again", "concept_1", "TEST", "again",
                true, null, "OPERATOR", "tester", "req"));
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        await using var db = CreateDb();
        var a = await SeedWithPrerequisitesAsync(db, keyPrefix: "a");
        var b = await SeedWithPrerequisitesAsync(db, keyPrefix: "b");
        var job = await CreateJobAsync(a.Workshop, a.Workspace.WorkspaceId, "iso-1");

        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            b.Workshop.GetWorkshopJobAsync(job.WorkshopJobId, false, b.Workspace.AdvertiserId));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            b.Workshop.GetConceptPackageAsync(job.OutputConceptPackageVersionId!.Value, false, b.Workspace.AdvertiserId));
    }

    [Fact]
    public async Task Production_guard_rejects_local_when_require_remote_enabled()
    {
        await using var db = CreateDb();
        var seeded = await SeedWithPrerequisitesAsync(db);
        var guarded = CreateWorkshop(
            db,
            new LocalDeterministicWeddingPlannerAiProvider(),
            new WeddingPlannerAiOptions
            {
                Provider = WeddingPlannerAiProviderKinds.Local,
                RequireRemoteAiProvider = true
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateJobAsync(guarded, seeded.Workspace.WorkspaceId, "guarded"));
    }

    [Fact]
    public void Ef_model_indexes_and_restrict_delete_for_phase5()
    {
        using var db = CreateDb();
        var contribution = db.Model.FindEntityType(typeof(WeddingPlannerConceptRoleContribution));
        Assert.Contains(contribution!.GetIndexes(), i =>
            i.IsUnique
            && i.Properties.Count == 2
            && i.Properties[0].Name == nameof(WeddingPlannerConceptRoleContribution.ConceptPackageVersionId)
            && i.Properties[1].Name == nameof(WeddingPlannerConceptRoleContribution.LogicalRole));

        var workspace = db.Model.FindEntityType(typeof(WeddingPlannerWorkspace));
        var pointerFk = workspace!.GetForeignKeys()
            .Single(x => x.Properties.Any(p => p.Name == nameof(WeddingPlannerWorkspace.CurrentApprovedConceptPackageVersionId)));
        Assert.Equal(DeleteBehavior.Restrict, pointerFk.DeleteBehavior);

        var agentRun = db.Model.FindEntityType(typeof(WeddingPlannerAgentRun));
        Assert.NotNull(agentRun!.FindProperty(nameof(WeddingPlannerAgentRun.OutputConceptPackageVersionId)));
    }

    private static BlissDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BlissDbContext(options);
    }

    private static WeddingPlannerConceptWorkshopOrchestrationService CreateWorkshop(
        BlissDbContext db,
        IWeddingPlannerAiProvider ai,
        WeddingPlannerAiOptions? aiOptions = null) =>
        new(
            db,
            new WeddingPlannerService(db),
            ai,
            Options.Create(aiOptions ?? new WeddingPlannerAiOptions
            {
                Provider = WeddingPlannerAiProviderKinds.Local
            }));

    private static async Task<WeddingPlannerWorkshopJobResult> CreateJobAsync(
        WeddingPlannerConceptWorkshopOrchestrationService workshop,
        Guid workspaceId,
        string key) =>
        await workshop.CreateWorkshopJobAsync(
            workspaceId,
            "Objective",
            "Campaign goal text",
            "Audience",
            WeddingPlannerChannelFormats.StaticSocialSquare,
            ["Deliverable one"],
            "Start planning",
            Array.Empty<string>(),
            null,
            null,
            null,
            "TEST",
            key,
            true,
            null,
            "OPERATOR",
            "tester",
            "req");

    private static async Task<(
        WeddingPlannerWorkspaceResult Workspace,
        WeddingPlannerConceptWorkshopOrchestrationService Workshop,
        LocalDeterministicWeddingPlannerAiProvider Ai)> SeedWithPrerequisitesAsync(
        BlissDbContext db,
        IWeddingPlannerAiProvider? ai = null,
        string keyPrefix = "p5")
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"Workshop Adv {keyPrefix}", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var setupAi = new LocalDeterministicWeddingPlannerAiProvider();
        var workshopAi = ai ?? new LocalDeterministicWeddingPlannerAiProvider();
        var localAi = workshopAi as LocalDeterministicWeddingPlannerAiProvider
            ?? new LocalDeterministicWeddingPlannerAiProvider();

        var planner = new WeddingPlannerService(db);
        var orchestration = new WeddingPlannerOrchestrationService(db, planner, setupAi);
        var colorService = new WeddingPlannerColorIntelligenceService(db, planner);
        var curator = new WeddingPlannerCuratorOrchestrationService(
            db,
            planner,
            new LocalDeterministicWeddingPlannerResearchProvider(),
            setupAi,
            Options.Create(new WeddingPlannerResearchOptions { Provider = WeddingPlannerResearchProviderKinds.Local }));
        var workshop = CreateWorkshop(db, workshopAi);

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
            workspace.WorkspaceId,
            "Topic",
            "Objective",
            ["Q1"],
            "Manila",
            "en",
            null,
            "TEST",
            $"{keyPrefix}-research",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-research");
        await curator.DecideAsync(
            researchJob.OutputResearchReportVersionId!.Value,
            "APPROVE",
            "ok",
            "TEST",
            $"{keyPrefix}-research-dec",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-research-dec");

        return (workspace, workshop, localAi);
    }

    private sealed class StageFailingAiProvider : IWeddingPlannerAiProvider
    {
        private readonly LocalDeterministicWeddingPlannerAiProvider _inner = new();
        private readonly int _failOnCall;
        private int _calls;

        public StageFailingAiProvider(int failOnCall) => _failOnCall = failOnCall;
        public string WorkerKey => _inner.WorkerKey;

        public async Task<WeddingPlannerAiCompletionResult> CompleteAsync(
            WeddingPlannerAiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.WorkerProfileVersion is WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1
                or WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1
                or WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1)
            {
                _calls++;
                if (_calls >= _failOnCall)
                {
                    throw new WeddingPlannerAiProviderException("simulated workshop stage failure", "PROVIDER_TRANSPORT");
                }
            }

            return await _inner.CompleteAsync(request, cancellationToken);
        }
    }
}
