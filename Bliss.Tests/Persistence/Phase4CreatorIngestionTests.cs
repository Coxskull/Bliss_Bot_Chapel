using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class Phase4CreatorIngestionTests
{
    [Fact]
    public async Task First_observation_creates_canonical_creator_profile_run_and_provenance()
    {
        await using var db = TestDb.CreateContext();
        var service = new CreatorIngestionService(db);

        var result = await service.IngestAsync(Command("run-001"));

        Assert.False(result.IsReplay);
        Assert.Equal("CREATED", result.Outcome);
        Assert.Equal("YOUTUBE::channel-AbC123", result.IdentityKey);
        Assert.Equal(1, await db.Creators.CountAsync());
        Assert.Equal(1, await db.CreatorPlatforms.CountAsync());
        Assert.Equal(1, await db.CreatorIngestionRuns.CountAsync());
        Assert.True(await db.DataProvenances.CountAsync() >= 7);

        var creator = await db.Creators.SingleAsync();
        Assert.Equal("Controlled Test Creator", creator.Name);
        Assert.Equal("ES", creator.CountryCode);
        Assert.Equal("Spanish", creator.PrimaryLanguage);
        Assert.Null(creator.FemalePercentage);
        Assert.Null(creator.MalePercentage);

        var platform = await db.CreatorPlatforms.SingleAsync();
        Assert.Equal(result.IdentityKey, platform.IdentityKey);
        Assert.Equal(result.CreatorId, platform.CreatorId);
    }

    [Fact]
    public async Task Same_source_and_idempotency_key_replays_without_duplicate_writes()
    {
        await using var db = TestDb.CreateContext();
        var service = new CreatorIngestionService(db);
        var command = Command("run-002");

        var first = await service.IngestAsync(command);
        var provenanceCount = await db.DataProvenances.CountAsync();
        var replay = await service.IngestAsync(command);

        Assert.True(replay.IsReplay);
        Assert.Equal(first.RunId, replay.RunId);
        Assert.Equal(first.CreatorId, replay.CreatorId);
        Assert.Equal(1, await db.Creators.CountAsync());
        Assert.Equal(1, await db.CreatorPlatforms.CountAsync());
        Assert.Equal(1, await db.CreatorIngestionRuns.CountAsync());
        Assert.Equal(provenanceCount, await db.DataProvenances.CountAsync());
    }

    [Fact]
    public async Task New_observation_for_existing_identity_updates_without_duplicate_creator()
    {
        await using var db = TestDb.CreateContext();
        var service = new CreatorIngestionService(db);
        var first = await service.IngestAsync(Command("run-003"));

        var updated = await service.IngestAsync(Command("run-004") with
        {
            CreatorName = "Controlled Test Creator Updated",
            AudienceSize = 220_000,
            Followers = 225_000
        });

        Assert.False(updated.IsReplay);
        Assert.Equal("UPDATED", updated.Outcome);
        Assert.Equal(first.CreatorId, updated.CreatorId);
        Assert.Equal(1, await db.Creators.CountAsync());
        Assert.Equal(1, await db.CreatorPlatforms.CountAsync());
        Assert.Equal(2, await db.CreatorIngestionRuns.CountAsync());
        Assert.Equal(220_000, (await db.Creators.SingleAsync()).AudienceSize);
        Assert.Equal(225_000, (await db.CreatorPlatforms.SingleAsync()).Followers);
    }

    [Fact]
    public async Task Same_external_id_on_different_platform_is_a_distinct_identity()
    {
        await using var db = TestDb.CreateContext();
        var service = new CreatorIngestionService(db);
        var youtube = await service.IngestAsync(Command("run-005"));
        var podcast = await service.IngestAsync(Command("run-006") with { Platform = "Podcast" });

        Assert.NotEqual(youtube.IdentityKey, podcast.IdentityKey);
        Assert.NotEqual(youtube.CreatorId, podcast.CreatorId);
        Assert.Equal(2, await db.Creators.CountAsync());
        Assert.Equal(2, await db.CreatorPlatforms.CountAsync());
    }

    [Fact]
    public async Task Invalid_observation_persists_nothing()
    {
        await using var db = TestDb.CreateContext();
        var service = new CreatorIngestionService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.IngestAsync(Command("run-007") with { AudienceSize = -1 }));

        Assert.Contains("cannot be negative", error.Message);
        Assert.Equal(0, await db.Creators.CountAsync());
        Assert.Equal(0, await db.CreatorIngestionRuns.CountAsync());
        Assert.Equal(0, await db.DataProvenances.CountAsync());
    }

    private static CreatorIngestionCommand Command(string idempotencyKey) => new(
        SourceSystem: "ControlledFixture",
        IdempotencyKey: idempotencyKey,
        Platform: "YouTube",
        ExternalProfileId: "channel-AbC123",
        CreatorName: "Controlled Test Creator",
        ProfileUrl: "https://example.test/creators/channel-AbC123",
        CountryCode: "es",
        PrimaryLanguage: "Spanish",
        AudienceSize: 180_000,
        Followers: 185_000,
        SourceUrl: "https://example.test/observations/channel-AbC123",
        ConfidenceLevel: "HIGH",
        CollectedAt: new DateTime(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc));
}
