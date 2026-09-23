using Bliss.Domain.Economics;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase7ResearchTests
{
    [Fact]
    public async Task Accepted_candidate_becomes_sourced_append_only_observation()
    {
        await using var db = TestDb.CreateContext();
        await new EconomicsDataSeeder(db).SeedAsync();
        var service = new EconomicsResearchService(db);
        var queueCommand = QueueCommand("phase7-accepted-run");

        var queued = await service.QueueAsync(queueCommand);
        var queueReplay = await service.QueueAsync(queueCommand);
        Assert.True(queueReplay.IsReplay);
        Assert.Equal(queued.Run.Id, queueReplay.Run.Id);

        var stageCommand = CandidateCommand(
            queued.Run.Id, "phase7-accepted-candidate");
        var staged = await service.StageCandidateAsync(stageCommand);
        var stageReplay = await service.StageCandidateAsync(stageCommand);
        Assert.True(stageReplay.IsReplay);
        var candidate = Assert.Single(staged.Run.Candidates);
        Assert.Equal(EconomicsResearchCandidateStatuses.Staged, candidate.Status);

        var reviewCommand = new ReviewEconomicsResearchCandidateCommand(
            candidate.Id,
            EconomicsResearchReviewDecisions.Accept,
            "TEST_REVIEWER",
            "Public source and extracted value reviewed",
            "TEST",
            "phase7-accepted-review");
        var reviewed = await service.ReviewCandidateAsync(reviewCommand);
        var reviewReplay = await service.ReviewCandidateAsync(reviewCommand);

        Assert.False(reviewed.IsReplay);
        Assert.True(reviewReplay.IsReplay);
        Assert.Equal(EconomicsResearchRunStatuses.Completed, reviewed.Run.Status);
        var promoted = Assert.Single(reviewed.Run.Candidates);
        Assert.Equal(EconomicsResearchCandidateStatuses.Promoted, promoted.Status);
        Assert.NotNull(promoted.PromotedObservationId);
        var observation = await db.MarketBenchmarkObservations
            .Include(x => x.ResearchSource)
            .SingleAsync(x => x.Id == promoted.PromotedObservationId);
        Assert.Equal(4025.25m, observation.NumericValue);
        Assert.Equal(ObservationVerificationStatuses.Estimated,
            observation.VerificationStatus);
        Assert.Equal(EconomicsConfidenceLevels.Medium, observation.ConfidenceLevel);
        Assert.Equal("https://api.worldbank.org/v2/country/PHL/indicator/NY.GDP.PCAP.CD",
            observation.ResearchSource!.SourceUrl);
        Assert.False(observation.ResearchSource.IsApproved);
    }

    [Fact]
    public async Task Rejected_candidate_is_preserved_without_observation()
    {
        await using var db = TestDb.CreateContext();
        await new EconomicsDataSeeder(db).SeedAsync();
        var service = new EconomicsResearchService(db);
        var run = await service.QueueAsync(QueueCommand("phase7-rejected-run"));
        var staged = await service.StageCandidateAsync(
            CandidateCommand(run.Run.Id, "phase7-rejected-candidate"));
        var candidate = Assert.Single(staged.Run.Candidates);

        var reviewed = await service.ReviewCandidateAsync(new(
            candidate.Id,
            EconomicsResearchReviewDecisions.Reject,
            "TEST_REVIEWER",
            "Source does not support the extracted interpretation",
            "TEST",
            "phase7-rejected-review"));

        var rejected = Assert.Single(reviewed.Run.Candidates);
        Assert.Equal(EconomicsResearchCandidateStatuses.Rejected, rejected.Status);
        Assert.Null(rejected.PromotedObservationId);
        Assert.Equal(6, await db.MarketBenchmarkObservations.CountAsync());
        Assert.Single(rejected.ReviewDecisions);
    }

    [Theory]
    [InlineData("HIGH", "ESTIMATED", "https://example.test/source", "confidence")]
    [InlineData("MEDIUM", "VERIFIED", "https://example.test/source", "VERIFIED")]
    [InlineData("MEDIUM", "ESTIMATED", "http://example.test/source", "HTTPS")]
    public async Task Untrusted_candidate_claims_are_rejected(
        string confidence,
        string verification,
        string sourceUrl,
        string expected)
    {
        await using var db = TestDb.CreateContext();
        await new EconomicsDataSeeder(db).SeedAsync();
        var service = new EconomicsResearchService(db);
        var run = await service.QueueAsync(
            QueueCommand($"phase7-invalid-run-{Guid.NewGuid():N}"));
        var command = CandidateCommand(
            run.Run.Id,
            $"phase7-invalid-{Guid.NewGuid():N}") with
        {
            ConfidenceLevel = confidence,
            VerificationStatus = verification,
            SourceUrl = sourceUrl
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StageCandidateAsync(command));
        Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.EconomicsResearchCandidates.ToListAsync());
    }

    private static QueueEconomicsResearchCommand QueueCommand(string key) =>
        new(
            EconomicsDataSeeder.ManilaId,
            "GDP_PER_CAPITA_CURRENT_USD",
            "WOMENS_FOOTWEAR",
            "DIGITAL",
            null,
            "Find recent public economic context for Manila.",
            "TEST_OPERATOR",
            "TEST",
            key);

    private static StageEconomicsResearchCandidateCommand CandidateCommand(
        Guid runId,
        string key) =>
        new(
            runId,
            4025.25m,
            null,
            null,
            "USD",
            "World Bank Open Data API",
            "https://api.worldbank.org/v2/country/PHL/indicator/NY.GDP.PCAP.CD",
            "PUBLIC_API",
            new DateOnly(2024, 12, 31),
            DateTime.UtcNow,
            EconomicsConfidenceLevels.Medium,
            ObservationVerificationStatuses.Estimated,
            "TEST_STRUCTURED_EXTRACTION_V1",
            """{"indicator":"NY.GDP.PCAP.CD","value":4025.25,"aiRole":"extraction only"}""",
            "N8N_RESEARCH",
            key);
}
