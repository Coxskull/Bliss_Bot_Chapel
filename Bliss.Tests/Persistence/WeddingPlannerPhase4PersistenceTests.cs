using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase4PersistenceTests
{
    [Fact]
    public async Task Happy_path_creates_exactly_three_runs_and_eight_contributions()
    {
        await using var db = CreateDb();
        var (workspace, curator, ai, research) = await SeedWithApprovedBrandDnaAsync(db);

        var job = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId,
            "Local wedding demand",
            "Map market signals for Manila couples",
            ["Who books venues?", "Which channels convert?"],
            "Manila",
            "en",
            Array.Empty<string>(),
            "TEST",
            "job-happy-1",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-job");

        Assert.Equal(WeddingPlannerResearchJobStatuses.Succeeded, job.Status);
        Assert.False(job.IsReplay);
        Assert.NotNull(job.OutputResearchReportVersionId);
        Assert.Contains(".invalid", job.SourceCatalogJson!, StringComparison.Ordinal);
        Assert.Contains("SYNTHETIC", job.SourceCatalogJson!, StringComparison.Ordinal);
        Assert.Equal(1, research.InvokeCount);
        Assert.Equal(3, ai.InvokeCount);

        var runs = await db.WeddingPlannerAgentRuns
            .Where(x => x.WorkspaceId == workspace.WorkspaceId
                        && x.WorkerProfileVersion != null)
            .OrderBy(x => x.StartedAt)
            .ToListAsync();
        Assert.Equal(3, runs.Count);
        Assert.All(runs, r => Assert.Equal(WeddingPlannerAgentRunStatuses.Succeeded, r.Status));
        Assert.Equal(
            new[]
            {
                WeddingPlannerCuratorWorkerProfiles.ResearchV1,
                WeddingPlannerCuratorWorkerProfiles.EvidenceV1,
                WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1
            },
            runs.Select(r => r.WorkerProfileVersion).ToArray());
        Assert.Equal(1, runs.Count(r => r.OutputResearchReportVersionId is not null));
        Assert.Equal(job.OutputResearchReportVersionId, runs.Single(r => r.OutputResearchReportVersionId is not null).OutputResearchReportVersionId);

        var contributions = await db.WeddingPlannerResearchRoleContributions
            .Where(x => x.ResearchReportVersionId == job.OutputResearchReportVersionId)
            .ToListAsync();
        Assert.Equal(8, contributions.Count);
        Assert.Equal(
            WeddingPlannerCuratorLogicalRoles.AllInOrder.ToHashSet(StringComparer.Ordinal),
            contributions.Select(x => x.LogicalRole).ToHashSet(StringComparer.Ordinal));

        var report = await db.WeddingPlannerResearchReportVersions.SingleAsync(x => x.Id == job.OutputResearchReportVersionId);
        Assert.Equal(WeddingPlannerResearchReportStatuses.Proposed, report.Status);
        Assert.Contains(WeddingPlannerResearchDisclaimer.Text, report.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(runs.Single(r => r.WorkerProfileVersion == WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1).Id, report.ProducingAgentRunId);
    }

    [Fact]
    public async Task Approved_brand_dna_is_required_and_color_is_optional_provenance()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "No DNA", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();
        var planner = new WeddingPlannerService(db);
        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open", true, null, "OPERATOR", "tester", "req");
        var curator = CreateCurator(db, new LocalDeterministicWeddingPlannerAiProvider(), new LocalDeterministicWeddingPlannerResearchProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            curator.CreateResearchJobAsync(
                workspace.WorkspaceId,
                "T", "O", ["Q"], "G", "en", null, "TEST", "no-dna",
                true, null, "OPERATOR", "tester", "req"));

        var seeded = await SeedWithApprovedBrandDnaAsync(db);
        var color = new WeddingPlannerColorIntelligenceService(db, new WeddingPlannerService(db));
        var profile = await color.ComputeAsync(
            seeded.Workspace.WorkspaceId,
            "#336699", null, null, null, null, null,
            "TEST", "color-1", true, null, "OPERATOR", "tester", "req-color");
        await color.DecideAsync(
            profile.ColorProfileVersionId, "APPROVE", "ok", "TEST", "color-dec",
            true, null, "OPERATOR", "tester", "req-color-dec");

        var job = await seeded.Curator.CreateResearchJobAsync(
            seeded.Workspace.WorkspaceId,
            "Topic", "Objective", ["Q1"], "Manila", "en", null,
            "TEST", "with-color", true, null, "OPERATOR", "tester", "req-job");
        Assert.NotNull(job.ApprovedColorProfileVersionId);
        Assert.Equal(profile.ColorProfileVersionId, job.ApprovedColorProfileVersionId);
    }

    [Fact]
    public async Task Succeeded_and_failed_replay_do_not_retry_providers()
    {
        await using var db = CreateDb();
        var (workspace, curator, ai, research) = await SeedWithApprovedBrandDnaAsync(db);
        var first = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "T", "O", ["Q"], "G", "en", null,
            "TEST", "replay-ok", true, null, "OPERATOR", "tester", "req");
        Assert.Equal(3, ai.InvokeCount);
        Assert.Equal(1, research.InvokeCount);

        var replay = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "ignored", "ignored", ["ignored"], "G", "en", null,
            "TEST", "replay-ok", true, null, "OPERATOR", "tester", "req2");
        Assert.True(replay.IsReplay);
        Assert.Equal(first.ResearchJobId, replay.ResearchJobId);
        Assert.Equal(3, ai.InvokeCount);
        Assert.Equal(1, research.InvokeCount);

        await using var failDb = CreateDb();
        var failingResearch = new FailingResearchProvider();
        var failSeed = await SeedWithApprovedBrandDnaAsync(
            failDb,
            ai: new LocalDeterministicWeddingPlannerAiProvider(),
            research: failingResearch);
        var failed = await Assert.ThrowsAsync<WeddingPlannerResearchJobProviderException>(() =>
            failSeed.Curator.CreateResearchJobAsync(
                failSeed.Workspace.WorkspaceId, "T", "O", ["Q"], "G", "en", null,
                "TEST", "replay-fail", true, null, "OPERATOR", "tester", "req"));
        Assert.Equal(0, await failDb.WeddingPlannerAgentRuns.CountAsync(x => x.WorkerProfileVersion != null));
        Assert.Equal(0, await failDb.WeddingPlannerResearchReportVersions.CountAsync());

        var failedReplay = await failSeed.Curator.CreateResearchJobAsync(
            failSeed.Workspace.WorkspaceId, "T", "O", ["Q"], "G", "en", null,
            "TEST", "replay-fail", true, null, "OPERATOR", "tester", "req2");
        Assert.True(failedReplay.IsReplay);
        Assert.Equal(WeddingPlannerResearchJobStatuses.Failed, failedReplay.Status);
        Assert.Equal(failed.ResearchJobId, failedReplay.ResearchJobId);
        Assert.Equal(1, failingResearch.InvokeCount);
    }

    [Fact]
    public async Task Provider_failure_creates_zero_runs_and_ai_stage_failure_keeps_priors()
    {
        await using var db = CreateDb();
        var seeded = await SeedWithApprovedBrandDnaAsync(
            db,
            ai: new StageFailingAiProvider(failOnCall: 2),
            research: new LocalDeterministicWeddingPlannerResearchProvider());

        var ex = await Assert.ThrowsAsync<WeddingPlannerProviderException>(() =>
            seeded.Curator.CreateResearchJobAsync(
                seeded.Workspace.WorkspaceId, "T", "O", ["Q"], "G", "en", null,
                "TEST", "ai-fail", true, null, "OPERATOR", "tester", "req"));
        Assert.NotEqual(Guid.Empty, ex.AgentRunId);

        var runs = await db.WeddingPlannerAgentRuns
            .Where(x => x.WorkerProfileVersion != null)
            .OrderBy(x => x.StartedAt)
            .ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.Equal(WeddingPlannerAgentRunStatuses.Succeeded, runs[0].Status);
        Assert.Equal(WeddingPlannerAgentRunStatuses.Failed, runs[1].Status);
        Assert.Equal(0, await db.WeddingPlannerResearchReportVersions.CountAsync());
        Assert.Equal(0, await db.WeddingPlannerResearchRoleContributions.CountAsync());
        var job = await db.WeddingPlannerResearchJobs.SingleAsync();
        Assert.Equal(WeddingPlannerResearchJobStatuses.Failed, job.Status);
        Assert.NotNull(job.ResearchStageOutputJson);
        Assert.Null(job.EvidenceStageOutputJson);
    }

    [Fact]
    public async Task Dangling_citation_fails_without_partial_report()
    {
        await using var db = CreateDb();
        var seeded = await SeedWithApprovedBrandDnaAsync(
            db,
            ai: new DanglingCitationAiProvider(),
            research: new LocalDeterministicWeddingPlannerResearchProvider());

        await Assert.ThrowsAsync<WeddingPlannerProviderException>(() =>
            seeded.Curator.CreateResearchJobAsync(
                seeded.Workspace.WorkspaceId, "T", "O", ["Q"], "G", "en", null,
                "TEST", "dangling", true, null, "OPERATOR", "tester", "req"));

        Assert.Equal(0, await db.WeddingPlannerResearchReportVersions.CountAsync());
        Assert.Equal(0, await db.WeddingPlannerResearchRoleContributions.CountAsync());
        Assert.Equal(WeddingPlannerResearchJobStatuses.Failed, (await db.WeddingPlannerResearchJobs.SingleAsync()).Status);
    }

    [Fact]
    public async Task Decisions_supersede_and_reject_without_mutating_document()
    {
        await using var db = CreateDb();
        var (workspace, curator, _, _) = await SeedWithApprovedBrandDnaAsync(db);
        var job1 = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "T1", "O", ["Q"], "G", "en", null,
            "TEST", "dec-1", true, null, "OPERATOR", "tester", "req1");
        var approve1 = await curator.DecideAsync(
            job1.OutputResearchReportVersionId!.Value, "APPROVE", "good", "TEST", "ap1",
            true, null, "OPERATOR", "tester", "req-ap1");
        Assert.True(approve1.Version.IsCurrentApproved);

        var originalJson = (await db.WeddingPlannerResearchReportVersions.AsNoTracking()
            .SingleAsync(x => x.Id == job1.OutputResearchReportVersionId)).DocumentJson;

        var job2 = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "T2", "O", ["Q"], "G", "en", null,
            "TEST", "dec-2", true, null, "OPERATOR", "tester", "req2");
        await curator.DecideAsync(
            job2.OutputResearchReportVersionId!.Value, "APPROVE", "newer", "TEST", "ap2",
            true, null, "OPERATOR", "tester", "req-ap2");

        var v1 = await db.WeddingPlannerResearchReportVersions.AsNoTracking()
            .SingleAsync(x => x.Id == job1.OutputResearchReportVersionId);
        Assert.Equal(WeddingPlannerResearchReportStatuses.Superseded, v1.Status);
        Assert.Equal(originalJson, v1.DocumentJson);

        var job3 = await curator.CreateResearchJobAsync(
            workspace.WorkspaceId, "T3", "O", ["Q"], "G", "en", null,
            "TEST", "dec-3", true, null, "OPERATOR", "tester", "req3");
        var rejected = await curator.DecideAsync(
            job3.OutputResearchReportVersionId!.Value, "REJECT", "nope", "TEST", "rej",
            true, null, "OPERATOR", "tester", "req-rej");
        Assert.False(rejected.Version.IsCurrentApproved);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            curator.DecideAsync(
                job3.OutputResearchReportVersionId!.Value, "APPROVE", "late", "TEST", "late",
                true, null, "OPERATOR", "tester", "req-late"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            curator.DecideAsync(
                job2.OutputResearchReportVersionId!.Value, "APPROVE", "", "TEST", "empty",
                true, null, "OPERATOR", "tester", "req-empty"));
    }

    [Fact]
    public async Task Cross_tenant_lookup_is_not_found()
    {
        await using var db = CreateDb();
        var a = await SeedWithApprovedBrandDnaAsync(db, keyPrefix: "a");
        var b = await SeedWithApprovedBrandDnaAsync(db, keyPrefix: "b");
        var job = await a.Curator.CreateResearchJobAsync(
            a.Workspace.WorkspaceId, "T", "O", ["Q"], "G", "en", null,
            "TEST", "iso-1", true, null, "OPERATOR", "tester", "req");

        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            b.Curator.GetResearchJobAsync(job.ResearchJobId, false, b.Workspace.AdvertiserId));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            b.Curator.GetResearchReportAsync(job.OutputResearchReportVersionId!.Value, false, b.Workspace.AdvertiserId));
    }

    [Fact]
    public void Ef_model_has_phase4_defaults_uniqueness_and_restrict_deletes()
    {
        using var db = CreateDb();
        var job = db.Model.FindEntityType(typeof(WeddingPlannerResearchJob));
        Assert.False(job!.FindProperty(nameof(WeddingPlannerResearchJob.InputJson))!.IsNullable);
        Assert.Equal(
            "RUNNING",
            typeof(WeddingPlannerResearchJob).GetProperty(nameof(WeddingPlannerResearchJob.Status))!
                .GetValue(new WeddingPlannerResearchJob()));

        var report = db.Model.FindEntityType(typeof(WeddingPlannerResearchReportVersion));
        Assert.True(report!.GetIndexes().Any(i =>
            i.IsUnique
            && i.Properties.Any(p => p.Name == nameof(WeddingPlannerResearchReportVersion.SourceSystem))
            && i.Properties.Any(p => p.Name == nameof(WeddingPlannerResearchReportVersion.IdempotencyKey))));

        var contribution = db.Model.FindEntityType(typeof(WeddingPlannerResearchRoleContribution));
        Assert.True(contribution!.GetIndexes().Any(i =>
            i.IsUnique
            && i.Properties.Count == 2
            && i.Properties[0].Name == nameof(WeddingPlannerResearchRoleContribution.ResearchReportVersionId)
            && i.Properties[1].Name == nameof(WeddingPlannerResearchRoleContribution.LogicalRole)));

        var workspace = db.Model.FindEntityType(typeof(WeddingPlannerWorkspace));
        var pointerFk = workspace!.GetForeignKeys()
            .Single(x => x.Properties.Any(p => p.Name == nameof(WeddingPlannerWorkspace.CurrentApprovedResearchReportVersionId)));
        Assert.Equal(DeleteBehavior.Restrict, pointerFk.DeleteBehavior);

        var agentRun = db.Model.FindEntityType(typeof(WeddingPlannerAgentRun));
        Assert.NotNull(agentRun!.FindProperty(nameof(WeddingPlannerAgentRun.WorkerProfileVersion)));
        Assert.NotNull(agentRun.FindProperty(nameof(WeddingPlannerAgentRun.AssignedRolesJson)));
        Assert.NotNull(agentRun.FindProperty(nameof(WeddingPlannerAgentRun.OutputResearchReportVersionId)));
    }

    private static BlissDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BlissDbContext(options);
    }

    private static WeddingPlannerCuratorOrchestrationService CreateCurator(
        BlissDbContext db,
        IWeddingPlannerAiProvider ai,
        IWeddingPlannerResearchProvider research,
        WeddingPlannerResearchOptions? researchOptions = null)
    {
        return new WeddingPlannerCuratorOrchestrationService(
            db,
            new WeddingPlannerService(db),
            research,
            ai,
            Options.Create(researchOptions ?? new WeddingPlannerResearchOptions
            {
                Provider = WeddingPlannerResearchProviderKinds.Local
            }));
    }

    private static async Task<(
        WeddingPlannerWorkspaceResult Workspace,
        WeddingPlannerCuratorOrchestrationService Curator,
        LocalDeterministicWeddingPlannerAiProvider Ai,
        LocalDeterministicWeddingPlannerResearchProvider Research)> SeedWithApprovedBrandDnaAsync(
        BlissDbContext db,
        IWeddingPlannerAiProvider? ai = null,
        IWeddingPlannerResearchProvider? research = null,
        string keyPrefix = "p4")
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"Curator Adv {keyPrefix}", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var setupAi = new LocalDeterministicWeddingPlannerAiProvider();
        var researchProvider = research ?? new LocalDeterministicWeddingPlannerResearchProvider();
        var localResearch = researchProvider as LocalDeterministicWeddingPlannerResearchProvider
            ?? new LocalDeterministicWeddingPlannerResearchProvider();
        var curatorAi = ai ?? new LocalDeterministicWeddingPlannerAiProvider();
        var localCuratorAi = curatorAi as LocalDeterministicWeddingPlannerAiProvider
            ?? new LocalDeterministicWeddingPlannerAiProvider();

        var planner = new WeddingPlannerService(db);
        var orchestration = new WeddingPlannerOrchestrationService(db, planner, setupAi);
        var curator = CreateCurator(db, curatorAi, researchProvider);

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

        return (workspace, curator, localCuratorAi, localResearch);
    }

    private sealed class FailingResearchProvider : IWeddingPlannerResearchProvider
    {
        public string WorkerKey => WeddingPlannerResearchWorkers.LocalDeterministicV1;
        public int InvokeCount { get; private set; }

        public Task<WeddingPlannerResearchAcquisitionResult> AcquireSourcesAsync(
            WeddingPlannerResearchAcquisitionRequest request,
            CancellationToken cancellationToken = default)
        {
            InvokeCount++;
            throw new WeddingPlannerResearchProviderException("simulated research outage", "RESEARCH_PROVIDER_TRANSPORT");
        }
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
            _calls++;
            if (_calls >= _failOnCall
                && request.WorkerProfileVersion is not null)
            {
                throw new WeddingPlannerAiProviderException("simulated curator stage failure", "PROVIDER_TRANSPORT");
            }

            return await _inner.CompleteAsync(request, cancellationToken);
        }
    }

    private sealed class DanglingCitationAiProvider : IWeddingPlannerAiProvider
    {
        private readonly LocalDeterministicWeddingPlannerAiProvider _inner = new();
        public string WorkerKey => _inner.WorkerKey;

        public async Task<WeddingPlannerAiCompletionResult> CompleteAsync(
            WeddingPlannerAiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(request.WorkerProfileVersion, WeddingPlannerCuratorWorkerProfiles.ResearchV1, StringComparison.Ordinal))
            {
                var content = """
                    {"schemaVersion":"curator-worker-output.v1","workerProfileVersion":"CURATOR_RESEARCH_V1","contributions":[
                      {"logicalRole":"MARKET_LANDSCAPE_RESEARCHER","summary":"x","findings":[{"type":"FACT","statement":"s","confidence":0.5,"citationSourceIds":["missing_src"]}]},
                      {"logicalRole":"AUDIENCE_CONTEXT_RESEARCHER","summary":"x","findings":[{"type":"GAP","statement":"g","confidence":0.5,"citationSourceIds":[]}]},
                      {"logicalRole":"COMPETITOR_SIGNALS_RESEARCHER","summary":"x","findings":[{"type":"GAP","statement":"g","confidence":0.5,"citationSourceIds":[]}]},
                      {"logicalRole":"CHANNEL_FORMAT_RESEARCHER","summary":"x","findings":[{"type":"GAP","statement":"g","confidence":0.5,"citationSourceIds":[]}]}
                    ]}
                    """;
                return new WeddingPlannerAiCompletionResult(
                    content, "local", "m", "a", "r", WorkerKey, 1, 1, 2, 0m);
            }

            return await _inner.CompleteAsync(request, cancellationToken);
        }
    }
}
