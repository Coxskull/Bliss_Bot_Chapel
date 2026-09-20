using System.Net;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase3ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase3ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Evaluation_runs_are_visible_and_match_history_is_listed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var all = await client.GetAsync("/api/match-evaluation-runs");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        var allBody = await all.Content.ReadAsStringAsync();
        Assert.Contains(Phase3DataSeeder.RunBrazilFirstId.ToString(), allBody);
        Assert.Contains(Phase3DataSeeder.RunBrazilSecondId.ToString(), allBody);

        var detail = await client.GetAsync($"/api/match-evaluation-runs/{Phase3DataSeeder.RunBrazilFirstId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadAsStringAsync();
        Assert.Contains("inputSnapshot", detailBody);
        Assert.Contains("outputSnapshot", detailBody);
        Assert.Contains("APPROVED", detailBody);

        var byMatch = await client.GetAsync($"/api/bliss/matches/{Phase2DataSeeder.MatchBrazilApprovedId}/evaluation-runs");
        Assert.Equal(HttpStatusCode.OK, byMatch.StatusCode);
        var byMatchBody = await byMatch.Content.ReadAsStringAsync();
        Assert.Contains(Phase3DataSeeder.RunBrazilFirstId.ToString(), byMatchBody);
        Assert.Contains(Phase3DataSeeder.RunBrazilSecondId.ToString(), byMatchBody);
    }

    [Fact]
    public async Task Evaluate_rules_appends_a_run_visible_on_the_api()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var before = await client.GetAsync($"/api/bliss/matches/{Phase2DataSeeder.MatchGeoFailId}/evaluation-runs");
        var beforeBody = await before.Content.ReadAsStringAsync();
        Assert.Contains(Phase3DataSeeder.RunGeoFailId.ToString(), beforeBody);

        var evaluate = await client.PostAsync($"/api/bliss/matches/{Phase2DataSeeder.MatchGeoFailId}/evaluate-rules", null);
        Assert.Equal(HttpStatusCode.OK, evaluate.StatusCode);

        var after = await client.GetAsync($"/api/bliss/matches/{Phase2DataSeeder.MatchGeoFailId}/evaluation-runs");
        var afterBody = await after.Content.ReadAsStringAsync();
        Assert.Contains(Phase3DataSeeder.RunGeoFailId.ToString(), afterBody);
        Assert.True(afterBody.Length > beforeBody.Length);
    }
}
