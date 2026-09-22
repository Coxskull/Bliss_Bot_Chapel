using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase6PersistenceTests
{
    [Fact]
    public async Task Happy_path_creates_exactly_six_runs_thirteen_contributions_and_n_assets()
    {
        await using var db = CreateDb();
        var (workspace, creative, ai, assets) = await SeedWithApprovedConceptAsync(db);

        var job = await CreateInitialJobAsync(creative, workspace.WorkspaceId, "job-happy-1", variantCount: 2);
        Assert.Equal(WeddingPlannerCreativeProductionJobStatuses.Succeeded, job.Status);
        Assert.False(job.IsReplay);
        Assert.NotNull(job.OutputCreativePackageVersionId);
        Assert.Equal(6, ai.InvokeCount);
        Assert.Equal(2, assets.InvokeCount);

        var runs = await db.WeddingPlannerAgentRuns
            .Where(x => x.WorkspaceId == workspace.WorkspaceId
                        && WeddingPlannerCreativeDepartmentWorkerProfiles.All.Contains(x.WorkerProfileVersion!))
            .OrderBy(x => x.StartedAt)
            .ToListAsync();
        Assert.Equal(6, runs.Count);
        Assert.All(runs, r => Assert.Equal(WeddingPlannerAgentRunStatuses.Succeeded, r.Status));
        Assert.All(runs, r => Assert.True(r.TotalTokens is > 0));
        Assert.Equal(1, runs.Count(r => r.OutputCreativePackageVersionId is not null));
        Assert.DoesNotContain(runs, r =>
            r.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1
            || r.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1
            || r.WorkerProfileVersion == WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1);

        var contributions = await db.WeddingPlannerCreativeRoleContributions
            .Where(x => x.CreativePackageVersionId == job.OutputCreativePackageVersionId)
            .ToListAsync();
        Assert.Equal(13, contributions.Count);
        Assert.Equal(
            WeddingPlannerCreativeDepartmentLogicalRoles.AllInOrder.ToArray(),
            WeddingPlannerCreativeDepartmentLogicalRoles.AllInOrder
                .Select(role => contributions.Single(c => c.LogicalRole == role).LogicalRole)
                .ToArray());

        var packageAssets = await db.WeddingPlannerCreativeAssets
            .Where(x => x.CreativePackageVersionId == job.OutputCreativePackageVersionId)
            .OrderBy(x => x.VariantId)
            .ToListAsync();
        Assert.Equal(2, packageAssets.Count);
        Assert.All(packageAssets, a =>
        {
            Assert.Equal(WeddingPlannerCreativeAssetContentTypes.ImagePng, a.ContentType);
            Assert.True(a.ByteSize > 0 && a.ByteSize <= WeddingPlannerPngValidator.MaxAssetBytes);
            WeddingPlannerPngValidator.ValidateExactCanvas(a.Bytes, a.Width, a.Height);
        });

        var package = await db.WeddingPlannerCreativePackageVersions.SingleAsync(x => x.Id == job.OutputCreativePackageVersionId);
        Assert.Equal(WeddingPlannerCreativePackageStatuses.Proposed, package.Status);
        Assert.Contains(WeddingPlannerCreativePackageDisclaimer.Text, package.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage, package.DocumentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"bytes\"", package.DocumentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initial_and_revision_jobs_rerun_six_profiles_and_create_new_assets()
    {
        await using var db = CreateDb();
        var (workspace, creative, ai, assets) = await SeedWithApprovedConceptAsync(db);
        var initial = await CreateInitialJobAsync(creative, workspace.WorkspaceId, "rev-parent", 2);
        Assert.Equal(6, ai.InvokeCount);
        Assert.Equal(2, assets.InvokeCount);

        var revision = await creative.CreateCreativeProductionJobAsync(
            workspace.WorkspaceId,
            WeddingPlannerCreativeProductionJobKinds.Revision,
            "Revise for clearer CTA",
            [WeddingPlannerChannelFormats.StaticSocialSquare, WeddingPlannerChannelFormats.StaticSocialStory],
            2,
            initial.OutputCreativePackageVersionId,
            "Sharpen CTA contrast",
            null,
            null,
            null,
            "TEST",
            "rev-child",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-rev");

        Assert.Equal(WeddingPlannerCreativeProductionJobStatuses.Succeeded, revision.Status);
        Assert.Equal(12, ai.InvokeCount);
        Assert.Equal(4, assets.InvokeCount);
        Assert.NotEqual(initial.OutputCreativePackageVersionId, revision.OutputCreativePackageVersionId);
        Assert.Equal(initial.OutputCreativePackageVersionId, revision.RevisionParentCreativePackageVersionId);

        var packages = await db.WeddingPlannerCreativePackageVersions
            .Where(x => x.WorkspaceId == workspace.WorkspaceId)
            .ToListAsync();
        Assert.Equal(2, packages.Count);
        Assert.Equal(4, await db.WeddingPlannerCreativeAssets.CountAsync(x => x.WorkspaceId == workspace.WorkspaceId));
        Assert.Equal(26, await db.WeddingPlannerCreativeRoleContributions.CountAsync(x => x.WorkspaceId == workspace.WorkspaceId));
    }

    [Fact]
    public async Task Succeeded_and_failed_replay_do_not_retry_ai_or_assets()
    {
        await using var db = CreateDb();
        var (workspace, creative, ai, assets) = await SeedWithApprovedConceptAsync(db);
        var first = await CreateInitialJobAsync(creative, workspace.WorkspaceId, "replay-ok", 1);
        Assert.Equal(6, ai.InvokeCount);
        Assert.Equal(1, assets.InvokeCount);

        var replay = await CreateInitialJobAsync(creative, workspace.WorkspaceId, "replay-ok", 1);
        Assert.True(replay.IsReplay);
        Assert.Equal(first.CreativeProductionJobId, replay.CreativeProductionJobId);
        Assert.Equal(6, ai.InvokeCount);
        Assert.Equal(1, assets.InvokeCount);

        await using var failDb = CreateDb();
        var failingAi = new StageFailingAiProvider(failOnCreativeCall: 2);
        var seeded = await SeedWithApprovedConceptAsync(failDb, failingAi);
        await Assert.ThrowsAsync<WeddingPlannerProviderException>(() =>
            CreateInitialJobAsync(seeded.Creative, seeded.Workspace.WorkspaceId, "replay-fail", 1));
        var failedCount = failingAi.CreativeInvokeCount;
        Assert.True(failedCount >= 2);
        var assetCountAfterFail = seeded.Assets.InvokeCount;
        Assert.Equal(0, assetCountAfterFail);

        var failedReplay = await CreateInitialJobAsync(seeded.Creative, seeded.Workspace.WorkspaceId, "replay-fail", 1);
        Assert.True(failedReplay.IsReplay);
        Assert.Equal(WeddingPlannerCreativeProductionJobStatuses.Failed, failedReplay.Status);
        Assert.Equal(failedCount, failingAi.CreativeInvokeCount);
        Assert.Equal(0, seeded.Assets.InvokeCount);
    }

    [Fact]
    public async Task Asset_failure_after_six_keeps_runs_and_creates_no_package()
    {
        await using var db = CreateDb();
        var failingAssets = new FailingAssetProvider();
        var seeded = await SeedWithApprovedConceptAsync(db, assetProvider: failingAssets);
        await Assert.ThrowsAsync<WeddingPlannerCreativeAssetJobProviderException>(() =>
            CreateInitialJobAsync(seeded.Creative, seeded.Workspace.WorkspaceId, "asset-fail", 2));

        Assert.Equal(6, seeded.Ai.InvokeCount);
        Assert.Equal(0, await db.WeddingPlannerCreativePackageVersions.CountAsync(x => x.WorkspaceId == seeded.Workspace.WorkspaceId));
        Assert.Equal(0, await db.WeddingPlannerCreativeAssets.CountAsync(x => x.WorkspaceId == seeded.Workspace.WorkspaceId));
        Assert.Equal(0, await db.WeddingPlannerCreativeRoleContributions.CountAsync(x => x.WorkspaceId == seeded.Workspace.WorkspaceId));
        Assert.Equal(6, await db.WeddingPlannerAgentRuns.CountAsync(x =>
            x.WorkspaceId == seeded.Workspace.WorkspaceId
            && WeddingPlannerCreativeDepartmentWorkerProfiles.All.Contains(x.WorkerProfileVersion!)));
    }

    [Fact]
    public async Task Final_package_SaveChanges_failure_leaves_no_package_and_fails_job_cleanly()
    {
        var interceptor = new FailWhenAddingCreativePackageInterceptor();
        await using var db = CreateDb(interceptor);
        var seeded = await SeedWithApprovedConceptAsync(db, keyPrefix: "p6-save-fail");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateInitialJobAsync(seeded.Creative, seeded.Workspace.WorkspaceId, "final-save-fail", 2));
        Assert.Contains("simulated creative package SaveChanges failure", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, interceptor.PackageSaveAttempts);

        Assert.Equal(0, await db.WeddingPlannerCreativePackageVersions.CountAsync(x => x.WorkspaceId == seeded.Workspace.WorkspaceId));
        Assert.Equal(0, await db.WeddingPlannerCreativeAssets.CountAsync(x => x.WorkspaceId == seeded.Workspace.WorkspaceId));
        Assert.Equal(0, await db.WeddingPlannerCreativeRoleContributions.CountAsync(x => x.WorkspaceId == seeded.Workspace.WorkspaceId));

        var job = await db.WeddingPlannerCreativeProductionJobs
            .SingleAsync(x => x.WorkspaceId == seeded.Workspace.WorkspaceId);
        Assert.Equal(WeddingPlannerCreativeProductionJobStatuses.Failed, job.Status);
        Assert.Null(job.OutputCreativePackageVersionId);
        Assert.False(string.IsNullOrWhiteSpace(job.ErrorCode));
        Assert.False(string.IsNullOrWhiteSpace(job.ErrorMessage));

        var productionRun = await db.WeddingPlannerAgentRuns.SingleAsync(x =>
            x.WorkspaceId == seeded.Workspace.WorkspaceId
            && x.WorkerProfileVersion == WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1);
        Assert.Equal(WeddingPlannerAgentRunStatuses.Succeeded, productionRun.Status);
        Assert.Null(productionRun.OutputCreativePackageVersionId);

        var audits = await db.WeddingPlannerAuditEvents
            .Where(x => x.WorkspaceId == seeded.Workspace.WorkspaceId)
            .Select(x => x.Action)
            .ToListAsync();
        Assert.Contains(WeddingPlannerAuditActions.CreativeProductionJobFailed, audits);
        Assert.DoesNotContain(WeddingPlannerAuditActions.CreativePackageProposed, audits);
        Assert.DoesNotContain(WeddingPlannerAuditActions.CreativeProductionJobSucceeded, audits);

        // Failed job remains replayable without retrying AI/assets.
        var aiCount = seeded.Ai.InvokeCount;
        var assetCount = seeded.Assets.InvokeCount;
        var replay = await CreateInitialJobAsync(seeded.Creative, seeded.Workspace.WorkspaceId, "final-save-fail", 2);
        Assert.True(replay.IsReplay);
        Assert.Equal(WeddingPlannerCreativeProductionJobStatuses.Failed, replay.Status);
        Assert.Null(replay.OutputCreativePackageVersionId);
        Assert.Equal(aiCount, seeded.Ai.InvokeCount);
        Assert.Equal(assetCount, seeded.Assets.InvokeCount);
    }

    [Fact]
    public async Task Approve_sets_pointer_and_selected_variant_on_decision_only()
    {
        await using var db = CreateDb();
        var (workspace, creative, _, _) = await SeedWithApprovedConceptAsync(db);
        var job = await CreateInitialJobAsync(creative, workspace.WorkspaceId, "dec-1", 2);
        var packageId = job.OutputCreativePackageVersionId!.Value;
        var before = (await db.WeddingPlannerCreativePackageVersions.SingleAsync(x => x.Id == packageId)).DocumentJson;

        var decision = await creative.DecideAsync(
            packageId, "APPROVE", "Variant 1 fits", "variant_1", "TEST", "ap-1",
            true, null, "OPERATOR", "tester", "req-ap");
        Assert.Equal("variant_1", decision.SelectedVariantId);
        Assert.True(decision.Version.IsCurrentApproved);

        var package = await db.WeddingPlannerCreativePackageVersions.SingleAsync(x => x.Id == packageId);
        Assert.Equal(before, package.DocumentJson);
        Assert.Equal(WeddingPlannerCreativePackageStatuses.Approved, package.Status);
        Assert.Equal(packageId, (await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == workspace.WorkspaceId))
            .CurrentApprovedCreativePackageVersionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creative.DecideAsync(packageId, "REJECT", "late", null, "TEST", "late", true, null, "OPERATOR", "tester", "req"));

        var job2 = await CreateInitialJobAsync(creative, workspace.WorkspaceId, "dec-2", 2);
        await creative.DecideAsync(
            job2.OutputCreativePackageVersionId!.Value, "APPROVE", "Next", "variant_2", "TEST", "ap-2",
            true, null, "OPERATOR", "tester", "req-ap2");
        Assert.Equal(WeddingPlannerCreativePackageStatuses.Superseded,
            (await db.WeddingPlannerCreativePackageVersions.SingleAsync(x => x.Id == packageId)).Status);
        Assert.Equal(before, (await db.WeddingPlannerCreativePackageVersions.SingleAsync(x => x.Id == packageId)).DocumentJson);

        var job3 = await CreateInitialJobAsync(creative, workspace.WorkspaceId, "dec-3", 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            creative.DecideAsync(
                job3.OutputCreativePackageVersionId!.Value, "REJECT", "None", "variant_1", "TEST", "rj-bad",
                true, null, "OPERATOR", "tester", "req"));
        var reject = await creative.DecideAsync(
            job3.OutputCreativePackageVersionId!.Value, "REJECT", "None fit", null, "TEST", "rj-1",
            true, null, "OPERATOR", "tester", "req");
        Assert.Null(reject.SelectedVariantId);
        Assert.Equal(job2.OutputCreativePackageVersionId,
            (await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == workspace.WorkspaceId))
            .CurrentApprovedCreativePackageVersionId);
    }

    [Fact]
    public async Task Prerequisites_and_production_guards()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "No concept", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();
        var planner = new WeddingPlannerService(db);
        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open", true, null, "OPERATOR", "tester", "req");
        var creative = CreateCreative(db, new LocalDeterministicWeddingPlannerAiProvider(), new LocalDeterministicWeddingPlannerCreativeAssetProvider());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateInitialJobAsync(creative, workspace.WorkspaceId, "no-concept", 1));

        var seeded = await SeedWithApprovedConceptAsync(db);
        var guardedAi = CreateCreative(
            db,
            new LocalDeterministicWeddingPlannerAiProvider(),
            new LocalDeterministicWeddingPlannerCreativeAssetProvider(),
            new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local, RequireRemoteAiProvider = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateInitialJobAsync(guardedAi, seeded.Workspace.WorkspaceId, "guard-ai", 1));

        var guardedAsset = CreateCreative(
            db,
            new LocalDeterministicWeddingPlannerAiProvider(),
            new LocalDeterministicWeddingPlannerCreativeAssetProvider(),
            assetOptions: new WeddingPlannerCreativeAssetOptions
            {
                Provider = WeddingPlannerCreativeAssetProviderKinds.Local,
                RequireRemoteCreativeAssetProvider = true
            });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateInitialJobAsync(guardedAsset, seeded.Workspace.WorkspaceId, "guard-asset", 1));
    }

    [Fact]
    public void Ef_model_indexes_and_restrict_delete_for_phase6()
    {
        using var db = CreateDb();
        var contribution = db.Model.FindEntityType(typeof(WeddingPlannerCreativeRoleContribution));
        Assert.Contains(contribution!.GetIndexes(), i =>
            i.IsUnique
            && i.Properties.Count == 2
            && i.Properties[0].Name == nameof(WeddingPlannerCreativeRoleContribution.CreativePackageVersionId)
            && i.Properties[1].Name == nameof(WeddingPlannerCreativeRoleContribution.LogicalRole));

        var asset = db.Model.FindEntityType(typeof(WeddingPlannerCreativeAsset));
        Assert.Contains(asset!.GetIndexes(), i =>
            i.IsUnique
            && i.Properties.Count == 2
            && i.Properties[0].Name == nameof(WeddingPlannerCreativeAsset.CreativePackageVersionId)
            && i.Properties[1].Name == nameof(WeddingPlannerCreativeAsset.VariantId));

        var workspace = db.Model.FindEntityType(typeof(WeddingPlannerWorkspace));
        var pointerFk = workspace!.GetForeignKeys()
            .Single(x => x.Properties.Any(p => p.Name == nameof(WeddingPlannerWorkspace.CurrentApprovedCreativePackageVersionId)));
        Assert.Equal(DeleteBehavior.Restrict, pointerFk.DeleteBehavior);

        var agentRun = db.Model.FindEntityType(typeof(WeddingPlannerAgentRun));
        Assert.NotNull(agentRun!.FindProperty(nameof(WeddingPlannerAgentRun.OutputCreativePackageVersionId)));

        Assert.All(db.Model.GetEntityTypes()
            .Where(e => e.ClrType.Name.StartsWith("WeddingPlannerCreative", StringComparison.Ordinal))
            .SelectMany(e => e.GetForeignKeys()),
            fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    private static BlissDbContext CreateDb(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (interceptors.Length > 0)
        {
            builder.AddInterceptors(interceptors);
        }

        return new BlissDbContext(builder.Options);
    }

    private static WeddingPlannerCreativeDepartmentOrchestrationService CreateCreative(
        BlissDbContext db,
        IWeddingPlannerAiProvider ai,
        IWeddingPlannerCreativeAssetProvider assets,
        WeddingPlannerAiOptions? aiOptions = null,
        WeddingPlannerCreativeAssetOptions? assetOptions = null) =>
        new(
            db,
            new WeddingPlannerService(db),
            ai,
            assets,
            Options.Create(aiOptions ?? new WeddingPlannerAiOptions { Provider = WeddingPlannerAiProviderKinds.Local }),
            Options.Create(assetOptions ?? new WeddingPlannerCreativeAssetOptions
            {
                Provider = WeddingPlannerCreativeAssetProviderKinds.Local
            }));

    private static async Task<WeddingPlannerCreativeProductionJobResult> CreateInitialJobAsync(
        WeddingPlannerCreativeDepartmentOrchestrationService creative,
        Guid workspaceId,
        string key,
        int variantCount) =>
        await creative.CreateCreativeProductionJobAsync(
            workspaceId,
            WeddingPlannerCreativeProductionJobKinds.Initial,
            "Objective for draft creative",
            variantCount >= 2
                ? [WeddingPlannerChannelFormats.StaticSocialSquare, WeddingPlannerChannelFormats.StaticSocialStory]
                : [WeddingPlannerChannelFormats.StaticSocialSquare],
            variantCount,
            null,
            null,
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
        WeddingPlannerCreativeDepartmentOrchestrationService Creative,
        LocalDeterministicWeddingPlannerAiProvider Ai,
        LocalDeterministicWeddingPlannerCreativeAssetProvider Assets)> SeedWithApprovedConceptAsync(
        BlissDbContext db,
        IWeddingPlannerAiProvider? ai = null,
        IWeddingPlannerCreativeAssetProvider? assetProvider = null,
        string keyPrefix = "p6")
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"Creative Adv {keyPrefix}", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var setupAi = new LocalDeterministicWeddingPlannerAiProvider();
        var creativeAi = ai ?? new LocalDeterministicWeddingPlannerAiProvider();
        var localAi = creativeAi as LocalDeterministicWeddingPlannerAiProvider
            ?? new LocalDeterministicWeddingPlannerAiProvider();
        var assets = assetProvider as LocalDeterministicWeddingPlannerCreativeAssetProvider
            ?? (assetProvider is null
                ? new LocalDeterministicWeddingPlannerCreativeAssetProvider()
                : null);
        var assetImpl = assetProvider ?? new LocalDeterministicWeddingPlannerCreativeAssetProvider();
        var localAssets = assets ?? new LocalDeterministicWeddingPlannerCreativeAssetProvider();

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
        var creative = CreateCreative(db, creativeAi, assetImpl);

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

        return (workspace, creative, localAi, assetImpl as LocalDeterministicWeddingPlannerCreativeAssetProvider ?? localAssets);
    }

    private sealed class StageFailingAiProvider : IWeddingPlannerAiProvider
    {
        private readonly LocalDeterministicWeddingPlannerAiProvider _inner = new();
        private readonly int _failOnCreativeCall;
        public int CreativeInvokeCount { get; private set; }

        public StageFailingAiProvider(int failOnCreativeCall) => _failOnCreativeCall = failOnCreativeCall;
        public string WorkerKey => _inner.WorkerKey;

        public async Task<WeddingPlannerAiCompletionResult> CompleteAsync(
            WeddingPlannerAiCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (WeddingPlannerCreativeDepartmentWorkerProfiles.All.Contains(request.WorkerProfileVersion ?? string.Empty))
            {
                CreativeInvokeCount++;
                if (CreativeInvokeCount >= _failOnCreativeCall)
                {
                    throw new WeddingPlannerAiProviderException("simulated creative stage failure", "PROVIDER_TRANSPORT");
                }
            }

            return await _inner.CompleteAsync(request, cancellationToken);
        }
    }

    private sealed class FailingAssetProvider : IWeddingPlannerCreativeAssetProvider
    {
        public string WorkerKey => WeddingPlannerCreativeAssetWorkers.LocalDeterministicV1;
        public Task<WeddingPlannerCreativeAssetGenerationResult> GeneratePngAsync(
            WeddingPlannerCreativeAssetGenerationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new WeddingPlannerCreativeAssetProviderException("simulated asset failure", "CREATIVE_ASSET_PROVIDER_TRANSPORT");
    }

    private sealed class FailWhenAddingCreativePackageInterceptor : SaveChangesInterceptor
    {
        public int PackageSaveAttempts { get; private set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ThrowIfAddingCreativePackage(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfAddingCreativePackage(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void ThrowIfAddingCreativePackage(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            if (context.ChangeTracker.Entries<WeddingPlannerCreativePackageVersion>()
                .Any(e => e.State == EntityState.Added))
            {
                PackageSaveAttempts++;
                throw new InvalidOperationException("simulated creative package SaveChanges failure");
            }
        }
    }
}
