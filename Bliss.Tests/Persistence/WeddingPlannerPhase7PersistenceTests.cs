using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase7PersistenceTests
{
    [Fact]
    public async Task Happy_path_exactly_two_runs_three_contributions_steward_null_run()
    {
        await using var db = CreateDb();
        var (workspace, qa, ai) = await SeedWithApprovedCreativeAsync(db);

        var job = await CreateQaJobAsync(qa, workspace.WorkspaceId, "qa-happy");
        Assert.Equal(WeddingPlannerQaReviewJobStatuses.Succeeded, job.Status);
        Assert.NotNull(job.OutputQaReviewReportVersionId);
        Assert.NotNull(job.RulesFindingsJson);
        Assert.Equal(2, ai.InvokeCount);

        var runs = await db.WeddingPlannerAgentRuns
            .Where(x => x.WorkspaceId == workspace.WorkspaceId
                        && WeddingPlannerQaWorkerProfiles.All.Contains(x.WorkerProfileVersion!))
            .OrderBy(x => x.StartedAt)
            .ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal(WeddingPlannerAgentRunStatuses.Succeeded, r.Status));
        Assert.All(runs, r => Assert.True(r.TotalTokens is > 0));
        Assert.Equal(1, runs.Count(r => r.OutputQaReviewReportVersionId is not null));
        Assert.DoesNotContain(runs, r =>
            WeddingPlannerCreativeDepartmentWorkerProfiles.All.Contains(r.WorkerProfileVersion!));

        var contributions = await db.WeddingPlannerQaRoleContributions
            .Where(x => x.QaReviewReportVersionId == job.OutputQaReviewReportVersionId)
            .ToListAsync();
        Assert.Equal(3, contributions.Count);
        Assert.Equal(
            WeddingPlannerQaLogicalRoles.AllInOrder.ToArray(),
            contributions.OrderBy(c => Array.IndexOf(WeddingPlannerQaLogicalRoles.AllInOrder.ToArray(), c.LogicalRole))
                .Select(c => c.LogicalRole)
                .ToArray());

        var chaperone = contributions.Single(c => c.LogicalRole == WeddingPlannerQaLogicalRoles.CreativeChaperone);
        var inspector = contributions.Single(c => c.LogicalRole == WeddingPlannerQaLogicalRoles.QaInspector);
        var steward = contributions.Single(c => c.LogicalRole == WeddingPlannerQaLogicalRoles.HumanEscalationSteward);
        Assert.Equal(WeddingPlannerQaContributionSources.Ai, chaperone.ContributionSource);
        Assert.Equal(WeddingPlannerQaContributionSources.Ai, inspector.ContributionSource);
        Assert.Equal(WeddingPlannerQaContributionSources.RulesHuman, steward.ContributionSource);
        Assert.NotNull(chaperone.ProducingAgentRunId);
        Assert.NotNull(inspector.ProducingAgentRunId);
        Assert.Null(steward.ProducingAgentRunId);

        var report = await db.WeddingPlannerQaReviewReportVersions.SingleAsync(x => x.Id == job.OutputQaReviewReportVersionId);
        Assert.Equal(WeddingPlannerQaReviewReportStatuses.Proposed, report.Status);
        Assert.Contains(WeddingPlannerQaReviewReportDisclaimer.Text, report.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview, report.DocumentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"bytes\"", report.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(WeddingPlannerQaFindingSeverities.Warn, job.RulesOverallSeverity);
    }

    [Fact]
    public async Task Accept_sets_pointer_and_later_creative_approve_clears_pointer_only()
    {
        await using var db = CreateDb();
        var seeded = await SeedWithApprovedCreativeAsync(db);
        var job = await CreateQaJobAsync(seeded.Qa, seeded.Workspace.WorkspaceId, "qa-accept");
        var decision = await seeded.Qa.DecideQaReviewReportAsync(
            job.OutputQaReviewReportVersionId!.Value,
            WeddingPlannerQaReviewDecisions.Accept,
            "Looks good after visual review",
            "variant_1",
            true,
            true,
            true,
            true,
            null,
            null,
            "TEST",
            "dec-accept",
            true,
            null,
            true,
            "OPERATOR",
            "tester",
            "req-dec");
        Assert.Equal(WeddingPlannerQaReviewReportStatuses.Accepted, decision.Version.Status);
        Assert.True(decision.Version.IsCurrentAccepted);

        var ws = await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == seeded.Workspace.WorkspaceId);
        Assert.Equal(job.OutputQaReviewReportVersionId, ws.CurrentAcceptedQaReviewReportVersionId);

        var creative = CreateCreative(db, new LocalDeterministicWeddingPlannerAiProvider(), new LocalDeterministicWeddingPlannerCreativeAssetProvider());
        var nextJob = await creative.CreateCreativeProductionJobAsync(
            seeded.Workspace.WorkspaceId,
            WeddingPlannerCreativeProductionJobKinds.Initial,
            "Second package",
            [WeddingPlannerChannelFormats.StaticSocialSquare],
            1,
            null,
            null,
            null,
            null,
            null,
            "TEST",
            "creative-2",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-c2");
        await creative.DecideAsync(
            nextJob.OutputCreativePackageVersionId!.Value,
            WeddingPlannerCreativePackageDecisions.Approve,
            "ok",
            "variant_1",
            "TEST",
            "creative-2-ap",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-c2-ap");

        await db.Entry(ws).ReloadAsync();
        Assert.Null(ws.CurrentAcceptedQaReviewReportVersionId);
        var historical = await db.WeddingPlannerQaReviewReportVersions.SingleAsync(x => x.Id == job.OutputQaReviewReportVersionId);
        Assert.Equal(WeddingPlannerQaReviewReportStatuses.Accepted, historical.Status);
        Assert.Contains(WeddingPlannerQaReviewReportDisclaimer.Text, historical.DocumentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Escalation_and_operator_waive_sets_exception_status_and_pointer()
    {
        await using var db = CreateDb();
        var seeded = await SeedWithApprovedCreativeAsync(db);
        var job = await CreateQaJobAsync(seeded.Qa, seeded.Workspace.WorkspaceId, "qa-esc");
        var decision = await seeded.Qa.DecideQaReviewReportAsync(
            job.OutputQaReviewReportVersionId!.Value,
            WeddingPlannerQaReviewDecisions.Escalate,
            "Need steward path",
            "variant_1",
            null,
            null,
            null,
            null,
            WeddingPlannerQaEscalationCategories.ClaimBoundary,
            null,
            "TEST",
            "dec-esc",
            true,
            null,
            true,
            "OPERATOR",
            "tester",
            "req-esc");
        Assert.NotNull(decision.EscalationCaseId);
        Assert.Equal(WeddingPlannerQaReviewReportStatuses.Escalated, decision.Version.Status);

        var blockers = WeddingPlannerQaReviewValidation.ExtractBlockerCodes(job.RulesFindingsJson!);
        var resolution = await seeded.Qa.ResolveEscalationCaseAsync(
            decision.EscalationCaseId!.Value,
            WeddingPlannerQaEscalationResolutions.WaiveAndAccept,
            "Operator exception",
            "Documented temporary waiver",
            true,
            blockers.ToArray(),
            null,
            "TEST",
            "res-waive",
            true,
            null,
            true,
            true,
            "OPERATOR",
            "tester",
            "req-waive");
        Assert.Equal(WeddingPlannerQaReviewReportStatuses.AcceptedWithException, resolution.Version.Status);
        Assert.True(resolution.Version.IsCurrentAccepted);
        Assert.Equal(WeddingPlannerQaEscalationCaseStatuses.Resolved, resolution.Case.Status);
    }

    [Fact]
    public async Task Succeeded_and_failed_replay_do_not_retry_rules_or_ai()
    {
        await using var db = CreateDb();
        var seeded = await SeedWithApprovedCreativeAsync(db);
        var first = await CreateQaJobAsync(seeded.Qa, seeded.Workspace.WorkspaceId, "qa-replay-ok");
        Assert.Equal(2, seeded.Ai.InvokeCount);

        var replay = await CreateQaJobAsync(seeded.Qa, seeded.Workspace.WorkspaceId, "qa-replay-ok");
        Assert.True(replay.IsReplay);
        Assert.Equal(first.QaReviewJobId, replay.QaReviewJobId);
        Assert.Equal(2, seeded.Ai.InvokeCount);

        await using var failDb = CreateDb();
        var failingAi = new QaStageFailingAiProvider(failOnQaCall: 1);
        var failSeeded = await SeedWithApprovedCreativeAsync(failDb, failingAi);
        await Assert.ThrowsAsync<WeddingPlannerProviderException>(() =>
            CreateQaJobAsync(failSeeded.Qa, failSeeded.Workspace.WorkspaceId, "qa-replay-fail"));
        var failedCount = failingAi.QaInvokeCount;
        Assert.True(failedCount >= 1);

        var failedReplay = await CreateQaJobAsync(failSeeded.Qa, failSeeded.Workspace.WorkspaceId, "qa-replay-fail");
        Assert.True(failedReplay.IsReplay);
        Assert.Equal(WeddingPlannerQaReviewJobStatuses.Failed, failedReplay.Status);
        Assert.Equal(failedCount, failingAi.QaInvokeCount);
        Assert.Equal(0, await failDb.WeddingPlannerQaReviewReportVersions.CountAsync(x => x.WorkspaceId == failSeeded.Workspace.WorkspaceId));
    }

    [Fact]
    public async Task Ai_context_excludes_bytes_and_mid_stage_fail_keeps_no_report()
    {
        await using var db = CreateDb();
        var capturing = new CapturingQaAiProvider();
        var seeded = await SeedWithApprovedCreativeAsync(db, capturing);
        var job = await CreateQaJobAsync(seeded.Qa, seeded.Workspace.WorkspaceId, "qa-context");
        Assert.Equal(WeddingPlannerQaReviewJobStatuses.Succeeded, job.Status);
        Assert.Equal(2, capturing.Contexts.Count);
        Assert.All(capturing.Contexts, ctx =>
        {
            Assert.DoesNotContain("base64", ctx, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("imageBytes", ctx, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"bytes\"", ctx, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/creative-assets/", ctx, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("selectedAssetMetadata", ctx, StringComparison.Ordinal);
        });

        await using var failDb = CreateDb();
        var failing = new QaStageFailingAiProvider(failOnQaCall: 2);
        var failSeeded = await SeedWithApprovedCreativeAsync(failDb, failing);
        await Assert.ThrowsAsync<WeddingPlannerProviderException>(() =>
            CreateQaJobAsync(failSeeded.Qa, failSeeded.Workspace.WorkspaceId, "qa-mid-fail"));
        Assert.Equal(0, await failDb.WeddingPlannerQaReviewReportVersions.CountAsync());
        Assert.Equal(0, await failDb.WeddingPlannerQaRoleContributions.CountAsync());
        var runs = await failDb.WeddingPlannerAgentRuns
            .Where(x => WeddingPlannerQaWorkerProfiles.All.Contains(x.WorkerProfileVersion!))
            .ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.Equal(1, runs.Count(r => r.Status == WeddingPlannerAgentRunStatuses.Succeeded));
        Assert.Equal(1, runs.Count(r => r.Status == WeddingPlannerAgentRunStatuses.Failed));
    }

    private static BlissDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BlissDbContext(options);
    }

    private static WeddingPlannerCreativeDepartmentOrchestrationService CreateCreative(
        BlissDbContext db,
        IWeddingPlannerAiProvider ai,
        IWeddingPlannerCreativeAssetProvider assets) =>
        new(
            db,
            new WeddingPlannerService(db),
            ai,
            assets,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }),
            Options.Create(new WeddingPlannerCreativeAssetOptions
            {
                Provider = WeddingPlannerCreativeAssetProviderKinds.Local
            }));

    private static WeddingPlannerQaReviewOrchestrationService CreateQa(
        BlissDbContext db,
        IWeddingPlannerAiProvider ai) =>
        new(
            db,
            new WeddingPlannerService(db),
            ai,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }));

    private static async Task<WeddingPlannerQaReviewJobResult> CreateQaJobAsync(
        WeddingPlannerQaReviewOrchestrationService qa,
        Guid workspaceId,
        string key) =>
        await qa.CreateQaReviewJobAsync(
            workspaceId,
            "Control review selected variant",
            [WeddingPlannerQaFocusAreas.Copy, WeddingPlannerQaFocusAreas.Visual, WeddingPlannerQaFocusAreas.Provenance],
            null,
            null,
            "TEST",
            key,
            true,
            null,
            "OPERATOR",
            "tester",
            "req-qa");

    private static async Task<(
        WeddingPlannerWorkspaceResult Workspace,
        WeddingPlannerQaReviewOrchestrationService Qa,
        LocalDeterministicWeddingPlannerAiProvider Ai)> SeedWithApprovedCreativeAsync(
        BlissDbContext db,
        IWeddingPlannerAiProvider? ai = null,
        string keyPrefix = "p7")
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"QA Adv {keyPrefix}", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var setupAi = new LocalDeterministicWeddingPlannerAiProvider();
        var qaAi = ai ?? new LocalDeterministicWeddingPlannerAiProvider();
        var localAi = qaAi as LocalDeterministicWeddingPlannerAiProvider
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
        var workshop = new WeddingPlannerConceptWorkshopOrchestrationService(
            db,
            planner,
            setupAi,
            Options.Create(new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }));
        var creative = CreateCreative(db, setupAi, new LocalDeterministicWeddingPlannerCreativeAssetProvider());
        var qa = CreateQa(db, qaAi);

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
            null,
            null,
            null,
            "TEST",
            $"{keyPrefix}-workshop",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-workshop");
        await workshop.DecideAsync(
            workshopJob.OutputConceptPackageVersionId!.Value,
            "APPROVE",
            "Concept 1 fits",
            "concept_1",
            "TEST",
            $"{keyPrefix}-concept-dec",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-concept-dec");
        var creativeJob = await creative.CreateCreativeProductionJobAsync(
            workspace.WorkspaceId,
            WeddingPlannerCreativeProductionJobKinds.Initial,
            "Draft calm square",
            [WeddingPlannerChannelFormats.StaticSocialSquare],
            1,
            null,
            null,
            null,
            null,
            null,
            "TEST",
            $"{keyPrefix}-creative",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-creative");
        await creative.DecideAsync(
            creativeJob.OutputCreativePackageVersionId!.Value,
            WeddingPlannerCreativePackageDecisions.Approve,
            "Variant 1",
            "variant_1",
            "TEST",
            $"{keyPrefix}-creative-dec",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-creative-dec");

        return (workspace, qa, localAi);
    }

    private sealed class QaStageFailingAiProvider : IWeddingPlannerAiProvider
    {
        private readonly LocalDeterministicWeddingPlannerAiProvider _inner = new();
        private readonly int _failOnQaCall;
        public int QaInvokeCount { get; private set; }

        public QaStageFailingAiProvider(int failOnQaCall) => _failOnQaCall = failOnQaCall;
        public string WorkerKey => _inner.WorkerKey;

        public async Task<WeddingPlannerAiCompletionResult> CompleteAsync(
            WeddingPlannerAiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (WeddingPlannerQaWorkerProfiles.All.Contains(request.WorkerProfileVersion ?? string.Empty))
            {
                QaInvokeCount++;
                if (QaInvokeCount >= _failOnQaCall)
                {
                    throw new WeddingPlannerAiProviderException("simulated qa stage failure", "PROVIDER_TRANSPORT");
                }
            }

            return await _inner.CompleteAsync(request, cancellationToken);
        }
    }

    private sealed class CapturingQaAiProvider : IWeddingPlannerAiProvider
    {
        private readonly LocalDeterministicWeddingPlannerAiProvider _inner = new();
        public List<string> Contexts { get; } = [];
        public string WorkerKey => _inner.WorkerKey;

        public async Task<WeddingPlannerAiCompletionResult> CompleteAsync(
            WeddingPlannerAiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (WeddingPlannerQaWorkerProfiles.All.Contains(request.WorkerProfileVersion ?? string.Empty))
            {
                Contexts.Add(string.Join("\n", request.Messages.Select(m => m.Body)));
            }

            return await _inner.CompleteAsync(request, cancellationToken);
        }
    }
}
