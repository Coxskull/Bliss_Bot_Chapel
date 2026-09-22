using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class WeddingPlannerPhase2PersistenceTests
{
    [Fact]
    public async Task Concierge_turn_is_idempotent_and_preserves_human_message_on_provider_failure()
    {
        await using var db = TestDb.CreateContext();
        var (workspace, session, planner, orchestration, counting) = await SeedAsync(db);

        var turn = await orchestration.ExecuteConciergeTurnAsync(
            session.SessionId,
            "audience: dental patients",
            "TEST",
            "turn-1",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-turn-1");
        var replay = await orchestration.ExecuteConciergeTurnAsync(
            session.SessionId,
            "audience: dental patients",
            "TEST",
            "turn-1",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-turn-1-replay");

        Assert.False(turn.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.Equal(1, counting.InvokeCount);
        Assert.Equal(2, await db.WeddingPlannerConversationMessages.CountAsync());
        Assert.Equal(1, turn.HumanMessage.SequenceNumber);
        Assert.Equal(2, turn.PlannerMessage!.SequenceNumber);
        Assert.Equal("PLANNER", turn.PlannerMessage.ActorType);
        Assert.Equal("SUCCEEDED", turn.AgentRun.Status);
        Assert.NotNull(turn.AgentRun.ProviderRequestId);
        Assert.True(turn.AgentRun.TotalTokens > 0);

        var failing = new WeddingPlannerOrchestrationService(db, planner, new FailingProvider());
        await Assert.ThrowsAsync<WeddingPlannerProviderException>(() => failing.ExecuteConciergeTurnAsync(
            session.SessionId,
            "this fails",
            "TEST",
            "turn-fail",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-fail"));

        Assert.Equal(3, await db.WeddingPlannerConversationMessages.CountAsync());
        Assert.Equal(1, await db.WeddingPlannerConversationMessages.CountAsync(x => x.ActorType == "PLANNER"));
        var failed = await db.WeddingPlannerAgentRuns.SingleAsync(x => x.IdempotencyKey == "turn-fail");
        Assert.Equal("FAILED", failed.Status);
        Assert.Null(failed.OutputMessageId);
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "AGENT_RUN_FAILED"));
    }

    [Fact]
    public async Task Brand_dna_versioning_approval_supersession_and_immutability()
    {
        await using var db = TestDb.CreateContext();
        var (workspace, session, _, orchestration, _) = await SeedAsync(db);

        await orchestration.ExecuteConciergeTurnAsync(
            session.SessionId,
            "voice: warm. audience: couples. offer: packages.",
            "TEST",
            "turn-dna",
            true,
            null,
            "OPERATOR",
            "tester",
            "req-1");

        var v1 = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", "dna-1", true, null, "OPERATOR", "tester", "req-2");
        var v1Replay = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", "dna-1", true, null, "OPERATOR", "tester", "req-2b");
        Assert.Equal(1, v1.VersionNumber);
        Assert.True(v1Replay.IsReplay);
        Assert.Equal(v1.BrandDnaVersionId, v1Replay.BrandDnaVersionId);
        Assert.Equal("PROPOSED", v1.Status);
        Assert.Contains("\"schemaVersion\":\"brand-dna.v1\"", v1.DocumentJson);

        var approve1 = await orchestration.DecideBrandDnaAsync(
            v1.BrandDnaVersionId, "APPROVE", "ok", "TEST", "dec-1", true, null, "OPERATOR", "tester", "req-3");
        Assert.Equal("APPROVED", approve1.Version.Status);
        Assert.True(approve1.Version.IsCurrentApproved);

        var v2 = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", "dna-2", true, null, "OPERATOR", "tester", "req-4");
        Assert.Equal(2, v2.VersionNumber);
        var originalV1Json = (await db.WeddingPlannerBrandDnaVersions.AsNoTracking()
            .SingleAsync(x => x.Id == v1.BrandDnaVersionId)).DocumentJson;

        await orchestration.DecideBrandDnaAsync(
            v2.BrandDnaVersionId, "APPROVE", "better", "TEST", "dec-2", true, null, "OPERATOR", "tester", "req-5");

        var list = await orchestration.ListBrandDnaAsync(workspace.WorkspaceId, true, null);
        Assert.Equal(v2.BrandDnaVersionId, list.CurrentApprovedBrandDnaVersionId);
        Assert.Equal("SUPERSEDED", list.Versions.Single(x => x.BrandDnaVersionId == v1.BrandDnaVersionId).Status);
        Assert.Equal(originalV1Json, list.Versions.Single(x => x.BrandDnaVersionId == v1.BrandDnaVersionId).DocumentJson);

        var v3 = await orchestration.InterpretBrandDnaAsync(
            workspace.WorkspaceId, "TEST", "dna-3", true, null, "OPERATOR", "tester", "req-6");
        var reject = await orchestration.DecideBrandDnaAsync(
            v3.BrandDnaVersionId, "REJECT", "no", "TEST", "dec-3", true, null, "OPERATOR", "tester", "req-7");
        Assert.Equal("REJECTED", reject.Version.Status);
        Assert.False(reject.Version.IsCurrentApproved);
        Assert.Equal(v2.BrandDnaVersionId, (await orchestration.ListBrandDnaAsync(workspace.WorkspaceId, true, null))
            .CurrentApprovedBrandDnaVersionId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestration.DecideBrandDnaAsync(
            v3.BrandDnaVersionId, "APPROVE", "illegal", "TEST", "dec-4", true, null, "OPERATOR", "tester", "req-8"));

        var decisionReplay = await orchestration.DecideBrandDnaAsync(
            v3.BrandDnaVersionId, "REJECT", "no", "TEST", "dec-3", true, null, "OPERATOR", "tester", "req-9");
        Assert.True(decisionReplay.IsReplay);

        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "BRAND_DNA_PROPOSED"));
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "BRAND_DNA_APPROVED"));
        Assert.True(await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == "BRAND_DNA_REJECTED"));
    }

    [Fact]
    public async Task Cross_tenant_orchestration_lookups_return_not_found()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var a = new Advertiser { Id = Guid.NewGuid(), Name = "A", CreatedAt = now };
        var b = new Advertiser { Id = Guid.NewGuid(), Name = "B", CreatedAt = now };
        db.Advertisers.AddRange(a, b);
        await db.SaveChangesAsync();

        var planner = new WeddingPlannerService(db);
        var orchestration = new WeddingPlannerOrchestrationService(db, planner, new LocalDeterministicWeddingPlannerAiProvider());
        var workspaceA = await planner.OpenPrimaryWorkspaceAsync(a.Id, "TEST", "a", true, null, "OPERATOR", "t", "r1");
        var sessionA = await planner.CreateSessionAsync(workspaceA.WorkspaceId, "TEST", "sa", true, null, "OPERATOR", "t", "r2");
        var turn = await orchestration.ExecuteConciergeTurnAsync(
            sessionA.SessionId, "hello", "TEST", "ta", true, null, "OPERATOR", "t", "r3");
        var dna = await orchestration.InterpretBrandDnaAsync(
            workspaceA.WorkspaceId, "TEST", "da", true, null, "OPERATOR", "t", "r4");

        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            orchestration.ExecuteConciergeTurnAsync(sessionA.SessionId, "x", "TEST", "tb", false, b.Id, "ADVERTISER", "b", "r5"));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            orchestration.GetAgentRunAsync(turn.AgentRunId, false, b.Id));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            orchestration.GetBrandDnaAsync(dna.BrandDnaVersionId, false, b.Id));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            orchestration.InterpretBrandDnaAsync(workspaceA.WorkspaceId, "TEST", "dx", false, b.Id, "ADVERTISER", "b", "r6"));
    }

    private static async Task<(WeddingPlannerWorkspaceResult Workspace, WeddingPlannerSessionResult Session, WeddingPlannerService Planner, WeddingPlannerOrchestrationService Orchestration, LocalDeterministicWeddingPlannerAiProvider Provider)> SeedAsync(BlissDbContext db)
    {
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "TEST Dental", CreatedAt = now };
        db.Advertisers.Add(advertiser);
        await db.SaveChangesAsync();

        var planner = new WeddingPlannerService(db);
        var provider = new LocalDeterministicWeddingPlannerAiProvider();
        var orchestration = new WeddingPlannerOrchestrationService(db, planner, provider);
        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiser.Id, "TEST", "open", true, null, "OPERATOR", "tester", "req-open");
        var session = await planner.CreateSessionAsync(
            workspace.WorkspaceId, "TEST", "session", true, null, "OPERATOR", "tester", "req-session");
        return (workspace, session, planner, orchestration, provider);
    }

    private sealed class FailingProvider : IWeddingPlannerAiProvider
    {
        public string WorkerKey => WeddingPlannerWorkers.LocalDeterministicV1;

        public Task<WeddingPlannerAiCompletionResult> CompleteAsync(
            WeddingPlannerAiCompletionRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("forced failure");
    }
}
