using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Api;

public sealed class RestrictedImageWorkerScopeTests
{
    [Fact]
    public async Task Preview_worker_cannot_select_or_mutate_unpinned_jobs()
    {
        using var db = new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var runId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var briefId = Guid.NewGuid();
        var input = "{}";
        var hash = ControlledAiService.Hash(input);
        ControlledAiRun Make(Guid id, Guid workspace, string kind, string state) => new()
        {
            Id = id, WorkspaceId = workspace, BriefId = briefId, Kind = kind,
            Status = state, Provider = "OpenAI", Model = "gpt-image-2.5-flare",
            InputSnapshotJson = input, InputSha256 = hash, ReservedUsd = 1.80m,
            IdempotencyKey = id.ToString(), CreatedAt = DateTime.UtcNow,
            DeadlineAt = DateTime.UtcNow.AddMinutes(-5)
        };
        var pinned = Make(runId, workspaceId, "IMAGE", "QUEUED");
        var other = Make(Guid.NewGuid(), workspaceId, "IMAGE", "QUEUED");
        var expired = Make(Guid.NewGuid(), Guid.NewGuid(), "TEXT", "RUNNING");
        db.ControlledAiRuns.AddRange(pinned, other, expired);
        await db.SaveChangesAsync();
        var runtime = new BlissRuntimeOptions { RestrictedPreview = true, ControlledAi = new ControlledAiOptions
        {
            Enabled = true, ImagePilotEnabled = true, Protocol = "OpenAI", Provider = "OpenAI",
            ApiToken = "test-not-real", ImageEndpoint = "https://api.openai.com/v1/images/generations",
            ImageModel = "gpt-image-2.5-flare", MaxCallsPerDay = 1,
            TotalBudgetUsd = 18m, DailyBudgetUsd = 1.80m, ImageReservationUsd = 1.80m,
            MaxResponseBytes = 24_000_000
        }};
        using var http = new HttpClient(new DenyNetwork());
        var service = new ControlledAiService(db, runtime, new ConfiguredControlledAiProvider(http, runtime));
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        Assert.False(await service.RunOne(default));
        Assert.Equal("RUNNING", expired.Status);
        Assert.Equal("QUEUED", other.Status);
        var pin = runtime.ControlledAi.RestrictedImageRun;
        pin.RunId = runId; pin.WorkspaceId = workspaceId; pin.BriefId = briefId; pin.InputSha256 = hash;
        Assert.Equal(runId, (await service.WorkerCandidates().SingleAsync()).Id);
        pin.WorkspaceId = Guid.NewGuid();
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        pin.WorkspaceId = workspaceId;
        pin.InputSha256 = new string('0', 64);
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        pin.InputSha256 = hash;
        runtime.ControlledAi.MaxCallsPerDay = 2;
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        runtime.ControlledAi.DailyBudgetUsd = 3.60m;
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        runtime.ControlledAi.RestrictedRevisionBriefId = Guid.NewGuid();
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        runtime.ControlledAi.RestrictedRevisionBriefId = briefId;
        Assert.Equal(runId,(await service.WorkerCandidates().SingleAsync()).Id);
        Assert.False(runtime.ControlledAi.RestrictedBriefAllowed(Guid.NewGuid()));
        runtime.ControlledAi.DailyBudgetUsd = 3.61m;
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        runtime.ControlledAi.MaxCallsPerDay = 1;
        runtime.ControlledAi.DailyBudgetUsd = 1.80m;
        runtime.ControlledAi.RestrictedRevisionBriefId = Guid.Empty;
        runtime.ControlledAi.TotalBudgetUsd = 18.01m;
        Assert.Empty(await service.WorkerCandidates().ToListAsync());
        runtime.RestrictedPreview = false;
        Assert.Equal(3, await service.WorkerCandidates().CountAsync());
    }

    private sealed class DenyNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Network forbidden in worker scope test.");
    }
}
