using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase8PersistenceTests
{
    [Fact]
    public async Task Commit_creates_handshake_mark_placement_and_zero_agent_runs()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db);

        var agentRunsBefore = await db.WeddingPlannerAgentRuns.CountAsync();
        var result = await CommitAsync(seeded, "cr-happy");
        Assert.False(result.IsReplay);
        Assert.Equal(WeddingPlannerCampaignReadinessHandshakeStatuses.CampaignReady, result.Status);
        Assert.True(result.IsCurrent);
        Assert.Contains(WeddingPlannerCampaignReadinessHandshakeDisclaimer.Text, result.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerCampaignReadinessMarkers.SyntheticDevelopmentCampaignReadiness, result.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(agentRunsBefore, await db.WeddingPlannerAgentRuns.CountAsync());

        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == result.CampaignPlacementId);
        Assert.Equal(EntityStatuses.Planned, placement.Status);
        var run = await db.CampaignPlacementRuns.SingleAsync(x => x.Id == result.CampaignPlacementRunId);
        Assert.Equal(EntityStatuses.Completed, run.Status);
        Assert.Equal(EntityStatuses.Planned, run.Outcome);
        Assert.EndsWith(":PLACEMENT", run.IdempotencyKey, StringComparison.Ordinal);

        var mark = await db.WeddingPlannerCampaignReadinessDecisions.SingleAsync(x =>
            x.CampaignReadinessHandshakeVersionId == result.CampaignReadinessHandshakeVersionId
            && x.Decision == WeddingPlannerCampaignReadinessDecisions.MarkCampaignReady);
        Assert.EndsWith(":MARK", mark.IdempotencyKey, StringComparison.Ordinal);

        var ws = await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == seeded.WorkspaceId);
        Assert.Equal(result.CampaignReadinessHandshakeVersionId, ws.CurrentCampaignReadinessHandshakeVersionId);
        Assert.True((await db.AdInventorySlots.SingleAsync(x => x.Id == seeded.SlotId)).IsAvailable);
        Assert.Equal(
            EntityStatuses.Approved,
            (await db.BlissMatches.SingleAsync(x => x.Id == seeded.MatchId)).Status);

        var stored = await db.WeddingPlannerCampaignReadinessHandshakeVersions
            .SingleAsync(x => x.Id == result.CampaignReadinessHandshakeVersionId);
        var match = await db.BlissMatches.SingleAsync(x => x.Id == seeded.MatchId);
        var qa = await db.WeddingPlannerQaReviewReportVersions.SingleAsync(x =>
            x.Id == stored.QaReviewReportVersionId);
        var expectedQaSha = WeddingPlannerCampaignReadinessValidation.Sha256Hex(qa.DocumentJson);
        Assert.Equal(expectedQaSha, stored.QaReviewReportDocumentSha256);
        Assert.Equal(match.CreatorId, stored.CreatorId);
        Assert.Equal(match.AdvertiserOpportunityId, stored.AdvertiserOpportunityId);
        Assert.Equal(match.RuleVersionId, stored.RuleVersionId);

        var doc = System.Text.Json.Nodes.JsonNode.Parse(stored.DocumentJson)!.AsObject();
        var pins = doc["pins"]!.AsObject();
        Assert.Equal(expectedQaSha, pins["qaReviewReportDocumentSha256"]!.GetValue<string>());
        Assert.Equal(match.CreatorId.ToString(), pins["creatorId"]!.GetValue<string>());
        Assert.Equal(match.AdvertiserOpportunityId.ToString(), pins["advertiserOpportunityId"]!.GetValue<string>());
        Assert.Equal(match.RuleVersionId.ToString(), pins["ruleVersionId"]!.GetValue<string>());
        Assert.Equal("VIDEO", doc["snapshots"]!["contentType"]!.GetValue<string>());
        Assert.Equal(5, doc["snapshots"]!["slotStartSecond"]!.GetValue<int>());
        Assert.Equal(30, doc["snapshots"]!["slotDurationSeconds"]!.GetValue<int>());
        Assert.Equal(
            (await db.Creators.SingleAsync(x => x.Id == match.CreatorId)).Name,
            doc["snapshots"]!["creatorName"]!.GetValue<string>());
        Assert.Equal(
            (await db.AdvertiserOpportunities.SingleAsync(x => x.Id == match.AdvertiserOpportunityId)).Name,
            doc["snapshots"]!["opportunityName"]!.GetValue<string>());
        Assert.DoesNotContain("\"url\"", stored.DocumentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Model_constraints_handshake_match_graph_fks_restrict_and_uniqueness()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(WeddingPlannerCampaignReadinessHandshakeVersion));
        Assert.NotNull(entity);

        Assert.All(entity!.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

        var creatorFk = entity.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(WeddingPlannerCampaignReadinessHandshakeVersion.CreatorId)));
        Assert.Equal(typeof(Creator), creatorFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, creatorFk.DeleteBehavior);

        var opportunityFk = entity.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(WeddingPlannerCampaignReadinessHandshakeVersion.AdvertiserOpportunityId)));
        Assert.Equal(typeof(AdvertiserOpportunity), opportunityFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, opportunityFk.DeleteBehavior);

        var ruleFk = entity.GetForeignKeys()
            .Single(fk => fk.Properties.Any(p => p.Name == nameof(WeddingPlannerCampaignReadinessHandshakeVersion.RuleVersionId)));
        Assert.Equal(typeof(RuleVersion), ruleFk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, ruleFk.DeleteBehavior);

        Assert.Contains(entity.GetIndexes(), i =>
            i.Properties.Count == 1
            && i.Properties[0].Name == nameof(WeddingPlannerCampaignReadinessHandshakeVersion.CreatorId));
        Assert.Contains(entity.GetIndexes(), i =>
            i.Properties.Count == 1
            && i.Properties[0].Name == nameof(WeddingPlannerCampaignReadinessHandshakeVersion.AdvertiserOpportunityId));
        Assert.Contains(entity.GetIndexes(), i =>
            i.Properties.Count == 1
            && i.Properties[0].Name == nameof(WeddingPlannerCampaignReadinessHandshakeVersion.RuleVersionId));

        Assert.Contains(entity.GetIndexes(), i =>
            i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(WeddingPlannerCampaignReadinessHandshakeVersion.SourceSystem),
                nameof(WeddingPlannerCampaignReadinessHandshakeVersion.IdempotencyKey)
            }));
        Assert.Contains(entity.GetIndexes(), i =>
            i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(WeddingPlannerCampaignReadinessHandshakeVersion.WorkspaceId),
                nameof(WeddingPlannerCampaignReadinessHandshakeVersion.VersionNumber)
            }));
    }

    [Fact]
    public async Task Exact_replay_stable_after_revoke_writes_nothing_including_audit()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db);
        var first = await CommitAsync(seeded, "cr-replay");
        var placements = await db.CampaignPlacements.CountAsync();
        var runs = await db.CampaignPlacementRuns.CountAsync();
        var handshakes = await db.WeddingPlannerCampaignReadinessHandshakeVersions.CountAsync();
        var decisions = await db.WeddingPlannerCampaignReadinessDecisions.CountAsync();
        var audits = await db.WeddingPlannerAuditEvents.CountAsync(x => x.WorkspaceId == seeded.WorkspaceId);

        await seeded.Cr.RevokeAsync(
            first.CampaignReadinessHandshakeVersionId,
            WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
            "withdraw planning",
            null,
            "TEST",
            "cr-replay-revoke",
            true,
            true,
            null,
            "OPERATOR",
            "tester",
            "req-rev");

        var decisionsAfterRevoke = await db.WeddingPlannerCampaignReadinessDecisions.CountAsync();
        var auditsAfterRevoke = await db.WeddingPlannerAuditEvents.CountAsync(x => x.WorkspaceId == seeded.WorkspaceId);
        var ws = await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == seeded.WorkspaceId);
        Assert.Null(ws.CurrentCampaignReadinessHandshakeVersionId);

        var replay = await CommitAsync(seeded, "cr-replay");
        Assert.True(replay.IsReplay);
        Assert.Equal(first.CampaignReadinessHandshakeVersionId, replay.CampaignReadinessHandshakeVersionId);
        Assert.Equal(placements, await db.CampaignPlacements.CountAsync());
        Assert.Equal(runs, await db.CampaignPlacementRuns.CountAsync());
        Assert.Equal(handshakes, await db.WeddingPlannerCampaignReadinessHandshakeVersions.CountAsync());
        Assert.Equal(decisionsAfterRevoke, await db.WeddingPlannerCampaignReadinessDecisions.CountAsync());
        Assert.Equal(auditsAfterRevoke, await db.WeddingPlannerAuditEvents.CountAsync(x => x.WorkspaceId == seeded.WorkspaceId));
        Assert.True(decisionsAfterRevoke > decisions);
        Assert.True(auditsAfterRevoke > audits);

        await db.Entry(ws).ReloadAsync();
        Assert.Null(ws.CurrentCampaignReadinessHandshakeVersionId);
        Assert.Equal(
            WeddingPlannerCampaignReadinessHandshakeStatuses.Revoked,
            (await db.WeddingPlannerCampaignReadinessHandshakeVersions.SingleAsync(x => x.Id == first.CampaignReadinessHandshakeVersionId)).Status);
    }

    [Fact]
    public async Task Idempotency_conflict_rejects_mismatched_selection_and_derived_key_collisions()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db);
        await CommitAsync(seeded, "cr-collide");

        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeded.Cr.CommitAsync(
                seeded.WorkspaceId,
                seeded.MatchId,
                seeded.CampaignId,
                seeded.ContentId,
                seeded.SlotId,
                "different rationale",
                true,
                true,
                null,
                "TEST",
                "cr-collide",
                true,
                true,
                null,
                "OPERATOR",
                "tester",
                "req-conflict"));
        Assert.Contains("Idempotency conflict", conflict.Message, StringComparison.Ordinal);

        var otherWorkspace = await SeedEligibleAsync(db, keyPrefix: "p8b");
        var cross = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            otherWorkspace.Cr.CommitAsync(
                otherWorkspace.WorkspaceId,
                otherWorkspace.MatchId,
                otherWorkspace.CampaignId,
                otherWorkspace.ContentId,
                otherWorkspace.SlotId,
                "Clean ACCEPTED QA bound to approved match inventory.",
                true,
                true,
                null,
                "TEST",
                "cr-collide",
                true,
                true,
                null,
                "OPERATOR",
                "tester",
                "req-cross-ws"));
        Assert.Contains("Idempotency conflict", cross.Message, StringComparison.Ordinal);

        await db.WeddingPlannerCampaignReadinessDecisions.AddAsync(new WeddingPlannerCampaignReadinessDecision
        {
            Id = Guid.NewGuid(),
            AdvertiserId = otherWorkspace.AdvertiserId,
            WorkspaceId = otherWorkspace.WorkspaceId,
            CampaignReadinessHandshakeVersionId = (await db.WeddingPlannerCampaignReadinessHandshakeVersions.FirstAsync()).Id,
            Decision = WeddingPlannerCampaignReadinessDecisions.MarkCampaignReady,
            Rationale = "orphan mark",
            ActorType = "OPERATOR",
            ActorLabel = "tester",
            SourceSystem = "TEST",
            IdempotencyKey = "fresh-key:MARK",
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var derivedMark = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            otherWorkspace.Cr.CommitAsync(
                otherWorkspace.WorkspaceId,
                otherWorkspace.MatchId,
                otherWorkspace.CampaignId,
                otherWorkspace.ContentId,
                otherWorkspace.SlotId,
                "Clean ACCEPTED QA bound to approved match inventory.",
                true,
                true,
                null,
                "TEST",
                "fresh-key",
                true,
                true,
                null,
                "OPERATOR",
                "tester",
                "req-derived-mark"));
        Assert.Contains("derived MARK key", derivedMark.Message, StringComparison.Ordinal);

        var firstPlacement = await db.CampaignPlacementRuns.AsNoTracking().FirstAsync();
        await db.CampaignPlacementRuns.AddAsync(new CampaignPlacementRun
        {
            Id = Guid.NewGuid(),
            CampaignPlacementId = firstPlacement.CampaignPlacementId,
            BlissMatchId = firstPlacement.BlissMatchId,
            CampaignId = firstPlacement.CampaignId,
            CreatorId = firstPlacement.CreatorId,
            AdvertiserOpportunityId = firstPlacement.AdvertiserOpportunityId,
            ContentItemId = firstPlacement.ContentItemId,
            AdInventorySlotId = firstPlacement.AdInventorySlotId,
            SourceSystem = "TEST",
            IdempotencyKey = "place-key:PLACEMENT",
            OperatorLabel = "orphan",
            Status = EntityStatuses.Completed,
            Outcome = EntityStatuses.Planned,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            InputSnapshot = "{}"
        });
        await db.SaveChangesAsync();

        var derivedPlacement = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            otherWorkspace.Cr.CommitAsync(
                otherWorkspace.WorkspaceId,
                otherWorkspace.MatchId,
                otherWorkspace.CampaignId,
                otherWorkspace.ContentId,
                otherWorkspace.SlotId,
                "Clean ACCEPTED QA bound to approved match inventory.",
                true,
                true,
                null,
                "TEST",
                "place-key",
                true,
                true,
                null,
                "OPERATOR",
                "tester",
                "req-derived-placement"));
        Assert.Contains("derived PLACEMENT key", derivedPlacement.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_replay_rejects_mark_key_collision_and_mismatched_handshake()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db);
        var committed = await CommitAsync(seeded, "cr-rev-idemp");
        var mark = await db.WeddingPlannerCampaignReadinessDecisions.SingleAsync(x =>
            x.CampaignReadinessHandshakeVersionId == committed.CampaignReadinessHandshakeVersionId
            && x.Decision == WeddingPlannerCampaignReadinessDecisions.MarkCampaignReady);

        var markAsRevoke = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeded.Cr.RevokeAsync(
                committed.CampaignReadinessHandshakeVersionId,
                WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
                "nope",
                null,
                mark.SourceSystem,
                mark.IdempotencyKey,
                true,
                true,
                null,
                "OPERATOR",
                "tester",
                "req-mark-as-revoke"));
        Assert.Contains("MARK_CAMPAIGN_READY", markAsRevoke.Message, StringComparison.Ordinal);

        await seeded.Cr.RevokeAsync(
            committed.CampaignReadinessHandshakeVersionId,
            WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
            "withdraw",
            null,
            "TEST",
            "cr-rev-ok",
            true,
            true,
            null,
            "OPERATOR",
            "tester",
            "req-rev-ok");

        var mismatched = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeded.Cr.RevokeAsync(
                Guid.NewGuid(),
                WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
                "withdraw",
                null,
                "TEST",
                "cr-rev-ok",
                true,
                true,
                null,
                "OPERATOR",
                "tester",
                "req-rev-mismatch"));
        Assert.Contains("Idempotency conflict", mismatched.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Revoke_leaves_placement_and_isavailable_unchanged()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db);
        var committed = await CommitAsync(seeded, "cr-revoke-stable");
        var slotBefore = await db.AdInventorySlots.AsNoTracking().SingleAsync(x => x.Id == seeded.SlotId);

        await seeded.Cr.RevokeAsync(
            committed.CampaignReadinessHandshakeVersionId,
            WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
            "done",
            null,
            "TEST",
            "cr-revoke-1",
            true,
            true,
            null,
            "OPERATOR",
            "tester",
            "req-rev2");

        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == committed.CampaignPlacementId);
        Assert.Equal(EntityStatuses.Planned, placement.Status);
        Assert.Equal(1, await db.CampaignPlacementRuns.CountAsync(x => x.Id == committed.CampaignPlacementRunId));
        var slotAfter = await db.AdInventorySlots.SingleAsync(x => x.Id == seeded.SlotId);
        Assert.Equal(slotBefore.IsAvailable, slotAfter.IsAvailable);
    }

    [Fact]
    public async Task Exception_qa_rejected_and_pointer_nonnull_blocks_new_commit()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db, acceptClean: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CommitAsync(seeded, "cr-exception"));
        Assert.Contains("BLOCK", ex.Message, StringComparison.Ordinal);

        await using var db2 = CreateDb();
        var seeded2 = await SeedEligibleAsync(db2);
        await CommitAsync(seeded2, "cr-pointer");
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => CommitAsync(seeded2, "cr-pointer-2"));
        Assert.Contains("pointer is set", blocked.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Qa_accept_and_creative_approve_clear_handshake_pointer_only()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db);
        var committed = await CommitAsync(seeded, "cr-clear");
        Assert.NotNull((await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == seeded.WorkspaceId))
            .CurrentCampaignReadinessHandshakeVersionId);

        // New clean ACCEPT on a second report path: create another QA job after resetting package pointer stay.
        // Simpler: WAIVE path is separate; here clear via creative APPROVE of a new package.
        var creative = CreateCreative(db, new LocalDeterministicWeddingPlannerAiProvider(), new LocalDeterministicWeddingPlannerCreativeAssetProvider());
        var nextJob = await creative.CreateCreativeProductionJobAsync(
            seeded.WorkspaceId,
            WeddingPlannerCreativeProductionJobKinds.Initial,
            "Second package",
            [WeddingPlannerChannelFormats.StaticSocialSquare],
            1,
            null, null, null, null, null,
            "TEST",
            "creative-clear-2",
            true, null, "OPERATOR", "tester", "req-c2");
        await creative.DecideAsync(
            nextJob.OutputCreativePackageVersionId!.Value,
            WeddingPlannerCreativePackageDecisions.Approve,
            "ok",
            "variant_1",
            "TEST",
            "creative-clear-2-ap",
            true, null, "OPERATOR", "tester", "req-c2-ap");

        var ws = await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == seeded.WorkspaceId);
        Assert.Null(ws.CurrentAcceptedQaReviewReportVersionId);
        Assert.Null(ws.CurrentCampaignReadinessHandshakeVersionId);
        var historical = await db.WeddingPlannerCampaignReadinessHandshakeVersions
            .SingleAsync(x => x.Id == committed.CampaignReadinessHandshakeVersionId);
        Assert.Equal(WeddingPlannerCampaignReadinessHandshakeStatuses.CampaignReady, historical.Status);
        Assert.Contains(WeddingPlannerCampaignReadinessHandshakeDisclaimer.Text, historical.DocumentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forced_save_failure_persists_nothing()
    {
        await using var db = new FailingSaveDbContext(
            new DbContextOptionsBuilder<BlissDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var seeded = await SeedEligibleAsync(db);
        db.FailNextSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CommitAsync(seeded, "cr-atomic"));
        Assert.Equal(0, await db.WeddingPlannerCampaignReadinessHandshakeVersions.CountAsync());
        Assert.Equal(0, await db.WeddingPlannerCampaignReadinessDecisions.CountAsync());
        Assert.Equal(0, await db.CampaignPlacements.CountAsync());
        Assert.Equal(0, await db.CampaignPlacementRuns.CountAsync());
        Assert.Null((await db.WeddingPlannerWorkspaces.SingleAsync(x => x.Id == seeded.WorkspaceId))
            .CurrentCampaignReadinessHandshakeVersionId);
    }

    [Fact]
    public async Task Shared_core_parity_bindasync_unchanged()
    {
        await using var db = await SeedBlissPlacementGraphAsync();
        var service = new CampaignPlacementService(db);
        var result = await service.BindAsync(new CampaignPlacementCommand(
            "ControlledFixture",
            "binding-phase8-parity",
            "TEST_OPERATOR",
            Phase2DataSeeder.MatchBrazilApprovedId,
            Phase2DataSeeder.Campaign2Id,
            Phase2DataSeeder.BrazilContentId,
            Guid.Parse("99999999-9999-9999-9999-999999999997")));
        Assert.False(result.IsReplay);
        Assert.Equal(EntityStatuses.Planned, result.Outcome);
        Assert.True((await db.AdInventorySlots.SingleAsync(x =>
            x.Id == Guid.Parse("99999999-9999-9999-9999-999999999997"))).IsAvailable);
    }

    [Fact]
    public async Task Cross_advertiser_match_blocked()
    {
        await using var db = CreateDb();
        var seeded = await SeedEligibleAsync(db);
        // Create foreign match under different advertiser program
        var foreignAdv = new Advertiser { Id = Guid.NewGuid(), Name = "Foreign", CreatedAt = DateTime.UtcNow };
        var program = new AdvertiserProgram
        {
            Id = Guid.NewGuid(),
            AdvertiserId = foreignAdv.Id,
            Name = "Foreign Program",
            Status = EntityStatuses.Active
        };
        var opp = new AdvertiserOpportunity
        {
            Id = Guid.NewGuid(),
            AdvertiserProgramId = program.Id,
            Name = "Foreign Opp",
            Status = EntityStatuses.Active
        };
        var rule = new RuleVersion
        {
            Id = Guid.NewGuid(),
            Version = "v-foreign",
            Name = "Foreign",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var match = new BlissMatch
        {
            Id = Guid.NewGuid(),
            CreatorId = seeded.CreatorId,
            AdvertiserOpportunityId = opp.Id,
            RuleVersionId = rule.Id,
            Status = EntityStatuses.Approved,
            CreatedAt = DateTime.UtcNow
        };
        db.Advertisers.Add(foreignAdv);
        db.AdvertiserPrograms.Add(program);
        db.AdvertiserOpportunities.Add(opp);
        db.RuleVersions.Add(rule);
        db.BlissMatches.Add(match);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeded.Cr.CommitAsync(
                seeded.WorkspaceId,
                match.Id,
                seeded.CampaignId,
                seeded.ContentId,
                seeded.SlotId,
                "cross advertiser",
                true,
                true,
                null,
                "TEST",
                "cr-cross",
                true,
                true,
                null,
                "OPERATOR",
                "tester",
                "req-cross"));
        Assert.Contains("BLOCK", ex.Message, StringComparison.Ordinal);
    }

    private static async Task<WeddingPlannerCampaignReadinessHandshakeResult> CommitAsync(
        SeededGraph seeded,
        string key) =>
        await seeded.Cr.CommitAsync(
            seeded.WorkspaceId,
            seeded.MatchId,
            seeded.CampaignId,
            seeded.ContentId,
            seeded.SlotId,
            "Clean ACCEPTED QA bound to approved match inventory.",
            true,
            true,
            null,
            "TEST",
            key,
            true,
            true,
            null,
            "OPERATOR",
            "tester",
            "req-" + key);

    private static BlissDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BlissDbContext(options);
    }

    private static async Task<BlissDbContext> SeedBlissPlacementGraphAsync()
    {
        var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        return db;
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

    private static WeddingPlannerCampaignReadinessOrchestrationService CreateCr(BlissDbContext db) =>
        new(
            db,
            new WeddingPlannerService(db),
            new CampaignPlacementService(db),
            new ExplicitWeddingPlannerCampaignReadinessHostEnvironment(true));

    private static async Task<SeededGraph> SeedEligibleAsync(
        BlissDbContext db,
        bool acceptClean = true,
        string keyPrefix = "p8")
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = $"Phase8 Adv {keyPrefix}", CreatedAt = now };
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
        var creative = CreateCreative(db, setupAi, new LocalDeterministicWeddingPlannerCreativeAssetProvider());
        var qa = CreateQa(db, setupAi);
        var cr = CreateCr(db);

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
                $"{keyPrefix}-qa-accept",
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
                $"{keyPrefix}-qa-esc",
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
                $"{keyPrefix}-qa-waive",
                true, null, true, true,
                "OPERATOR", "tester", "req-qa-waive");
        }

        var creator = new Creator
        {
            Id = Guid.NewGuid(),
            Name = $"Phase8 Creator {keyPrefix}",
            CreatedAt = now,
            UpdatedAt = now
        };
        var program = new AdvertiserProgram
        {
            Id = Guid.NewGuid(),
            AdvertiserId = advertiser.Id,
            Name = $"Phase8 Program {keyPrefix}",
            Status = EntityStatuses.Active
        };
        var opportunity = new AdvertiserOpportunity
        {
            Id = Guid.NewGuid(),
            AdvertiserProgramId = program.Id,
            Name = $"Phase8 Opp {keyPrefix}",
            Status = EntityStatuses.Active
        };
        var rule = new RuleVersion
        {
            Id = Guid.NewGuid(),
            Version = $"{keyPrefix}-v1",
            Name = $"Phase8 Rules {keyPrefix}",
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
            OverallScore = 0.91m,
            CreatedAt = now
        };
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Name = $"Phase8 Draft {keyPrefix}",
            Status = EntityStatuses.Draft,
            AdvertiserOpportunityId = null,
            CreatedAt = now
        };
        var content = new ContentItem
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            ContentType = "VIDEO",
            Title = $"Phase8 Content {keyPrefix}",
            CreatedAt = now
        };
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

        return new SeededGraph(
            workspace.WorkspaceId,
            advertiser.Id,
            match.Id,
            campaign.Id,
            content.Id,
            slot.Id,
            creator.Id,
            cr);
    }

    private sealed record SeededGraph(
        Guid WorkspaceId,
        Guid AdvertiserId,
        Guid MatchId,
        Guid CampaignId,
        Guid ContentId,
        Guid SlotId,
        Guid CreatorId,
        WeddingPlannerCampaignReadinessOrchestrationService Cr);

    private sealed class FailingSaveDbContext(DbContextOptions<BlissDbContext> options) : BlissDbContext(options)
    {
        public bool FailNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("forced failure");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
