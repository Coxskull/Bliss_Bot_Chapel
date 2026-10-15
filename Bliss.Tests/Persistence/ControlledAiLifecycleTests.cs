using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Api;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class ControlledAiLifecycleTests
{
    public sealed class Provider : IControlledAiProvider
    {
        public string Mode { get; set; } = "SUCCESS";
        public int Calls;
        public Func<Task>? BeforeReturn;
        public List<AiProviderRequest> Requests { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Configured(string kind) => true;
        public async Task<AiProviderResult> ExecuteAsync(AiProviderRequest request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls); Requests.Add(request); Started.TrySetResult();
            if (Mode == "WAIT") await Task.Delay(Timeout.Infinite, ct);
            if (Mode == "FAIL") throw new AiProviderException("PROVIDER_HTTP_503");
            if (BeforeReturn is not null) await BeforeReturn();
            return new AiProviderResult("Local test draft suggestion", request.Kind == "IMAGE" ? Convert.FromBase64String(AcademyProductionTests.Pixel) : null, "fixture", 10, 5, Mode == "OVER" ? .2m : Mode == "RECEIPT" ? .05m : null);
        }
    }
    private sealed class Harness : IAsyncDisposable
    {
        public string Name { get; } = Guid.NewGuid().ToString();
        public BlissDbContext Db { get; }
        public BlissRuntimeOptions Runtime { get; } = ControlledAiProviderTests.Runtime();
        public Provider Provider { get; } = new();
        public ControlledAiService Service { get; }
        public WeddingPlannerWorkspaceResult Workspace = null!;
        public Guid Session;
        public Harness() { Db = NewContext(); Service = new ControlledAiService(Db, Runtime, Provider); }
        public BlissDbContext NewContext() => new(new DbContextOptionsBuilder<BlissDbContext>().UseInMemoryDatabase(Name).Options);
        public async Task Initialize()
        {
            var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "TEST AI", CreatedAt = DateTime.UtcNow };
            Db.Add(advertiser); await Db.SaveChangesAsync();
            var planner = new WeddingPlannerService(Db);
            Workspace = await planner.OpenPrimaryWorkspaceAsync(advertiser.Id, "TEST", "workspace", true, null, "OPERATOR", "tester", "test");
            Session = (await planner.CreateSessionAsync(Workspace.WorkspaceId, "TEST", "session", true, null, "OPERATOR", "tester", "test")).SessionId;
            Db.WeddingPlannerBrandDna.Add(new WeddingPlannerBrandDna { Id = Guid.NewGuid(), WorkspaceId = Workspace.WorkspaceId,
                Version = 1, Status = "CONFIRMED", BrandName = "TEST Private Brand", Market = "Taipei", Language = "English", Niche = "pharmacy", Goals = "Awareness", IdempotencyKey = "brand", CreatedAt = DateTime.UtcNow });
            await Db.SaveChangesAsync();
        }
        public SubmitAiRun Request(string key = "run") => new("TEXT", Session, null, null, "Offer one campaign idea", key, true);
        public Task<(ControlledAiRun Run, bool Replay)> Submit(SubmitAiRun request) => Service.Submit(Workspace, request, "OPERATOR", "tester", "test", default);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    [Fact]
    public async Task Draft_output_is_durable_context_is_bounded_and_replay_never_calls_twice()
    {
        await using var h = new Harness(); await h.Initialize();
        var first = await h.Submit(h.Request()); var replay = await h.Submit(h.Request());
        Assert.True(replay.Replay); Assert.Equal(first.Run.Id, replay.Run.Id);
        await Assert.ThrowsAsync<WeddingPlannerConflictException>(() => h.Submit(h.Request() with { Prompt = "Changed" }));
        Assert.True(await h.Service.RunOne(default)); Assert.Equal(1, h.Provider.Calls);
        Assert.Equal("SUCCEEDED_PENDING_REVIEW", first.Run.Status); Assert.Equal("UNRECORDED", first.Run.CostStatus); Assert.Null(first.Run.ActualUsd);
        Assert.Equal("Local test draft suggestion", first.Run.OutputText);
        Assert.Contains("TEST Private Brand", h.Provider.Requests[0].InputJson);
        Assert.DoesNotContain("ApiToken", h.Provider.Requests[0].InputJson);
        Assert.False(await h.Service.RunOne(default)); Assert.Equal(1, h.Provider.Calls);
        await h.Submit(h.Request("followup") with { PreviousRunId = first.Run.Id });
        Assert.Contains("previousReply", (await h.Db.ControlledAiRuns.SingleAsync(x => x.IdempotencyKey == "followup")).InputSnapshotJson);
        Assert.Equal(3, await h.Db.ControlledAiEvents.CountAsync(x => x.RunId == first.Run.Id));
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() => h.Submit(h.Request("wrong-previous") with { PreviousRunId = Guid.NewGuid() }));
    }
    [Theory]
    [InlineData("daily")]
    [InlineData("total")]
    [InlineData("calls")]
    public async Task Budgets_are_reserved_before_dispatch_and_cancellation_does_not_refund(string limit)
    {
        await using var h = new Harness(); await h.Initialize();
        if (limit == "daily") h.Runtime.ControlledAi.DailyBudgetUsd = .1m;
        if (limit == "total") h.Runtime.ControlledAi.TotalBudgetUsd = .1m;
        if (limit == "calls") h.Runtime.ControlledAi.MaxCallsPerDay = 1;
        var run = (await h.Submit(h.Request())).Run;
        await h.Service.Cancel(h.Workspace.WorkspaceId, run.Id, "tester", default);
        await Assert.ThrowsAsync<AiQuotaException>(() => h.Submit(h.Request("next")));
        Assert.Equal(0, h.Provider.Calls);
        using var readiness = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(await h.Service.Readiness(default)));
        Assert.False(readiness.RootElement.GetProperty("textReady").GetBoolean());
        Assert.Equal(.1m, (await h.Db.ControlledAiBudgetLedgers.SingleAsync()).ReservedUsd);
        Assert.Single(await h.Db.ControlledAiRuns.ToListAsync());
    }
    [Fact]
    public async Task Running_cancellation_is_observed_without_a_second_provider_attempt()
    {
        await using var h = new Harness(); await h.Initialize(); h.Provider.Mode = "WAIT";
        var run = (await h.Submit(h.Request())).Run;
        var processing = h.Service.RunOne(default); await h.Provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await using (var cancelDb = h.NewContext()) await new ControlledAiService(cancelDb, h.Runtime, h.Provider).Cancel(h.Workspace.WorkspaceId, run.Id, "tester", default);
        await processing;
        Assert.Equal("CANCELLED", run.Status); Assert.Null(run.ActualUsd); Assert.Equal(1, h.Provider.Calls);
        Assert.False(await h.Service.RunOne(default));
    }
    [Theory]
    [InlineData("WAIT", "TIMED_OUT")]
    [InlineData("FAIL", "FAILED")]
    public async Task Failure_and_timeout_are_terminal_with_unknown_cost_and_no_retry(string mode, string expected)
    {
        await using var h = new Harness(); await h.Initialize(); h.Provider.Mode = mode;
        var run = (await h.Submit(h.Request())).Run; await h.Service.RunOne(default);
        Assert.Equal(expected, run.Status); Assert.Null(run.ActualUsd); Assert.Equal(.1m, run.ReservedUsd);
        await h.Service.RunOne(default); Assert.Equal(1, h.Provider.Calls);
    }
    [Fact]
    public async Task Expired_execution_is_recovered_as_unknown_even_when_calls_are_disabled()
    {
        await using var h = new Harness(); await h.Initialize(); var run = (await h.Submit(h.Request())).Run;
        run.Status = "RUNNING"; run.StartedAt = DateTime.UtcNow.AddMinutes(-10); run.DeadlineAt = DateTime.UtcNow.AddMinutes(-1);
        await h.Db.SaveChangesAsync(); h.Runtime.ControlledAi.Enabled = false;
        await h.Service.RunOne(default);
        Assert.Equal("UNKNOWN", run.Status); Assert.Equal(0, h.Provider.Calls);
        Assert.False(await h.Service.RunOne(default));
    }
    [Fact]
    public async Task Consent_configuration_inputs_and_usage_ledger_cannot_be_bypassed()
    {
        await using var h = new Harness(); await h.Initialize();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Submit(h.Request() with { ConsentToShare = false }));
        h.Runtime.ControlledAi.Enabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Submit(h.Request()));
        Assert.Empty(await h.Db.ControlledAiRuns.ToListAsync());
        h.Runtime.ControlledAi.Enabled = true; var run = (await h.Submit(h.Request())).Run;
        run.InputSnapshotJson = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Db.SaveChangesAsync());
        h.Db.ChangeTracker.Clear(); var ledger = await h.Db.ControlledAiBudgetLedgers.SingleAsync(); ledger.ReservedUsd = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task Reported_charge_over_reservation_stops_further_queueing()
    {
        await using var h = new Harness(); await h.Initialize(); h.Provider.Mode = "OVER";
        var run = (await h.Submit(h.Request())).Run;
        var previouslyQueued = (await h.Submit(h.Request("already-queued"))).Run;
        await h.Service.RunOne(default);
        Assert.Equal("OVER_RESERVATION", run.CostStatus); Assert.Equal(.2m, run.ActualUsd);
        await Assert.ThrowsAsync<AiQuotaException>(() => h.Submit(h.Request("next")));
        await h.Service.RunOne(default); Assert.Equal("FAILED", previouslyQueued.Status);
        Assert.Equal("BILLING_RECONCILIATION_REQUIRED", previouslyQueued.FailureCode);
        Assert.Equal(1, h.Provider.Calls);
    }
    [Fact]
    public async Task Cancellation_discards_output_but_keeps_an_already_returned_usage_receipt()
    {
        await using var h = new Harness(); await h.Initialize(); h.Provider.Mode = "RECEIPT";
        var run = (await h.Submit(h.Request())).Run;
        h.Provider.BeforeReturn = async () =>
        {
            await using var cancelDb = h.NewContext();
            await new ControlledAiService(cancelDb, h.Runtime, h.Provider).Cancel(h.Workspace.WorkspaceId, run.Id, "tester", default);
        };
        await h.Service.RunOne(default);
        Assert.Equal("CANCELLED", run.Status); Assert.Equal("", run.OutputText);
        Assert.Equal(.05m, run.ActualUsd); Assert.Equal("PROVIDER_REPORTED", run.CostStatus);
    }
}
