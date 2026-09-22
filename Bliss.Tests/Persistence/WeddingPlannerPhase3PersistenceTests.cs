using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase3PersistenceTests
{
    [Fact]
    public async Task Compute_requires_approved_brand_dna()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "No DNA Adv", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var planner = new WeddingPlannerService(db);
        var color = new WeddingPlannerColorIntelligenceService(db, planner);
        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open", true, null, "OPERATOR", "tester", "req-open");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => color.ComputeAsync(
            workspace.WorkspaceId, "#112233", null, null, null, null, null,
            "TEST", "nodna-cp", true, null, "OPERATOR", "tester", "req-nodna-cp"));
        Assert.Contains("Brand DNA", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await db.WeddingPlannerColorProfileVersions.CountAsync());
        Assert.Equal(0, await db.WeddingPlannerAgentRuns.CountAsync());
    }

    [Fact]
    public async Task Compute_replay_versioning_immutability_approve_supersede_and_reject()
    {
        await using var db = TestDb.CreateContext();
        var (workspace, color, _) = await SeedWithApprovedBrandDnaAsync(db);

        var v1 = await color.ComputeAsync(
            workspace.WorkspaceId,
            "#e11d48",
            null,
            null,
            null,
            null,
            "first notes",
            "TEST",
            "cp-1",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-1");
        Assert.Equal(1, v1.VersionNumber);
        Assert.Equal("PROPOSED", v1.Status);
        Assert.Equal("color-profile.v1", v1.SchemaVersion);
        Assert.Equal("aci.hsl.v1", v1.AlgorithmVersion);
        Assert.False(v1.IsReplay);
        Assert.Contains("\"notes\":\"first notes\"", v1.DocumentJson);

        var replay = await color.ComputeAsync(
            workspace.WorkspaceId,
            "#e11d48",
            null,
            null,
            null,
            null,
            "ignored on replay",
            "TEST",
            "cp-1",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-1b");
        Assert.True(replay.IsReplay);
        Assert.Equal(v1.ColorProfileVersionId, replay.ColorProfileVersionId);
        Assert.Equal(1, await db.WeddingPlannerColorProfileVersions.CountAsync(x => x.WorkspaceId == workspace.WorkspaceId));
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "COLOR_PROFILE_REPLAYED"));

        var v1NotesAlt = await color.ComputeAsync(
            workspace.WorkspaceId,
            "#e11d48",
            null,
            null,
            null,
            null,
            "different notes",
            "TEST",
            "cp-1-notes",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-1c");
        Assert.Equal(2, v1NotesAlt.VersionNumber);
        Assert.Equal(v1.InputSha256, v1NotesAlt.InputSha256);
        Assert.NotEqual(v1.DocumentJson, v1NotesAlt.DocumentJson);

        var emptyRationale = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            color.DecideAsync(
                v1.ColorProfileVersionId, "APPROVE", "  ", "TEST", "dec-empty",
                true, null, "OPERATOR", "tester", "req-empty"));
        Assert.Contains("rationale", emptyRationale.Message, StringComparison.OrdinalIgnoreCase);

        var approve1 = await color.DecideAsync(
            v1.ColorProfileVersionId, "APPROVE", "Ship it", "TEST", "dec-1",
            true, null, "OPERATOR", "tester", "req-2");
        Assert.Equal("APPROVED", approve1.Version.Status);
        Assert.True(approve1.Version.IsCurrentApproved);

        var originalPayload = await db.WeddingPlannerColorProfileVersions.AsNoTracking()
            .SingleAsync(x => x.Id == v1.ColorProfileVersionId);

        var v3 = await color.ComputeAsync(
            workspace.WorkspaceId, "#336699", "#AABBCC", null, "#FFFFFF", null, null,
            "TEST", "cp-3", true, null, "OPERATOR", "tester", "req-3");
        Assert.Equal(3, v3.VersionNumber);

        await color.DecideAsync(
            v3.ColorProfileVersionId, "APPROVE", "Newer palette", "TEST", "dec-3",
            true, null, "OPERATOR", "tester", "req-4");

        var list = await color.ListAsync(workspace.WorkspaceId, true, null);
        Assert.Equal(v3.ColorProfileVersionId, list.CurrentApprovedColorProfileVersionId);
        var superseded = list.Versions.Single(x => x.ColorProfileVersionId == v1.ColorProfileVersionId);
        Assert.Equal("SUPERSEDED", superseded.Status);
        Assert.Equal(originalPayload.DocumentJson, superseded.DocumentJson);
        Assert.Equal(originalPayload.InputJson, superseded.InputJson);
        Assert.Equal(originalPayload.InputSha256, superseded.InputSha256);
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "COLOR_PROFILE_SUPERSEDED"));

        var v4 = await color.ComputeAsync(
            workspace.WorkspaceId, "#010101", null, null, null, null, null,
            "TEST", "cp-4", true, null, "OPERATOR", "tester", "req-5");
        var reject = await color.DecideAsync(
            v4.ColorProfileVersionId, "REJECT", "Too dark", "TEST", "dec-4",
            true, null, "OPERATOR", "tester", "req-6");
        Assert.Equal("REJECTED", reject.Version.Status);
        Assert.False(reject.Version.IsCurrentApproved);
        Assert.Equal(v3.ColorProfileVersionId, (await color.ListAsync(workspace.WorkspaceId, true, null))
            .CurrentApprovedColorProfileVersionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => color.DecideAsync(
            v4.ColorProfileVersionId, "APPROVE", "late", "TEST", "dec-late",
            true, null, "OPERATOR", "tester", "req-7"));

        var decisionReplay = await color.DecideAsync(
            v4.ColorProfileVersionId, "REJECT", "Too dark", "TEST", "dec-4",
            true, null, "OPERATOR", "tester", "req-8");
        Assert.True(decisionReplay.IsReplay);

        Assert.Equal(0, await db.WeddingPlannerAgentRuns.CountAsync(x =>
            x.IdempotencyKey.StartsWith("cp-")));
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "COLOR_PROFILE_PROPOSED"));
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "COLOR_PROFILE_APPROVED"));
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "COLOR_PROFILE_REJECTED"));

        var proposedAudit = await db.WeddingPlannerAuditEvents
            .Where(x => x.Action == "COLOR_PROFILE_PROPOSED")
            .OrderBy(x => x.OccurredAt)
            .FirstAsync();
        Assert.Null(proposedAudit.MessageId);
        Assert.Contains(v1.ColorProfileVersionId.ToString(), proposedAudit.Detail);
    }

    [Fact]
    public async Task Cross_tenant_color_profile_lookups_return_not_found()
    {
        await using var db = TestDb.CreateContext();
        var (workspace, color, _) = await SeedWithApprovedBrandDnaAsync(db);
        var profile = await color.ComputeAsync(
            workspace.WorkspaceId, "#ABCDEF", null, null, null, null, null,
            "TEST", "cp-x", true, null, "OPERATOR", "tester", "req-x");

        var other = Guid.NewGuid();
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            color.ComputeAsync(workspace.WorkspaceId, "#ABCDEF", null, null, null, null, null,
                "TEST", "cp-x2", false, other, "ADVERTISER", "other", "req-x2"));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            color.ListAsync(workspace.WorkspaceId, false, other));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            color.GetAsync(profile.ColorProfileVersionId, false, other));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            color.DecideAsync(profile.ColorProfileVersionId, "APPROVE", "no", "TEST", "dec-x",
                false, other, "ADVERTISER", "other", "req-xd"));
    }

    [Fact]
    public void Ef_model_has_unique_indexes_restrict_fks_and_required_rationale()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(WeddingPlannerColorProfileVersion));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), x =>
            x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "WorkspaceId", "VersionNumber" }));
        Assert.Contains(entity.GetIndexes(), x =>
            x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "SourceSystem", "IdempotencyKey" }));
        Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        Assert.False(entity.FindProperty(nameof(WeddingPlannerColorProfileVersion.DocumentJson))!.IsNullable);

        var decision = db.Model.FindEntityType(typeof(WeddingPlannerColorProfileDecision));
        Assert.NotNull(decision);
        Assert.False(decision!.FindProperty(nameof(WeddingPlannerColorProfileDecision.Rationale))!.IsNullable);
        Assert.Contains(decision.GetIndexes(), x =>
            x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "SourceSystem", "IdempotencyKey" }));
        Assert.All(decision.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));

        var workspace = db.Model.FindEntityType(typeof(WeddingPlannerWorkspace));
        Assert.NotNull(workspace!.FindProperty(nameof(WeddingPlannerWorkspace.CurrentApprovedColorProfileVersionId)));
        var pointerFk = workspace.GetForeignKeys()
            .Single(x => x.Properties.Any(p => p.Name == nameof(WeddingPlannerWorkspace.CurrentApprovedColorProfileVersionId)));
        Assert.Equal(DeleteBehavior.Restrict, pointerFk.DeleteBehavior);
    }

    private static async Task<(WeddingPlannerWorkspaceResult Workspace, WeddingPlannerColorIntelligenceService Color, WeddingPlannerOrchestrationService Orchestration)> SeedWithApprovedBrandDnaAsync(BlissDbContext db)
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "Color Adv", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var planner = new WeddingPlannerService(db);
        var orchestration = new WeddingPlannerOrchestrationService(db, planner, new LocalDeterministicWeddingPlannerAiProvider());
        var color = new WeddingPlannerColorIntelligenceService(db, planner);
        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open", true, null, "OPERATOR", "tester", "req-open");
        var session = await planner.CreateSessionAsync(
            workspace.WorkspaceId, "TEST", "session", true, null, "OPERATOR", "tester", "req-session");
        await orchestration.ExecuteConciergeTurnAsync(
            session.SessionId, "voice: calm. audience: couples.", "TEST", "turn-1",
            true, null, "OPERATOR", "tester", "req-turn");
        var dna = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", "dna-1", true, null, "OPERATOR", "tester", "req-dna");
        await orchestration.DecideBrandDnaAsync(
            dna.BrandDnaVersionId, "APPROVE", "ok", "TEST", "dna-dec",
            true, null, "OPERATOR", "tester", "req-dna-dec");
        return (workspace, color, orchestration);
    }
}
