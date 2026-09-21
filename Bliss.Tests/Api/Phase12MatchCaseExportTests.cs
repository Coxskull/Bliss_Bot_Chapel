using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase12MatchCaseExportTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase12MatchCaseExportTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Match_case_file_includes_related_evaluations_without_snapshots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var matchId = Phase2DataSeeder.MatchBrazilApprovedId;
        var response = await client.GetAsync($"/api/audit/export/matches/{matchId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("bliss-match-", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        Assert.Equal("match-case", root.GetProperty("kind").GetString());
        Assert.Equal(matchId, root.GetProperty("matchId").GetGuid());
        Assert.Equal(matchId, root.GetProperty("match").GetProperty("id").GetGuid());
        var evaluationIds = root.GetProperty("evaluations").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToHashSet();
        Assert.Contains(Phase3DataSeeder.RunBrazilFirstId, evaluationIds);
        Assert.Contains(Phase3DataSeeder.RunBrazilSecondId, evaluationIds);

        var json = root.GetRawText();
        Assert.DoesNotContain("inputSnapshot", json);
        Assert.DoesNotContain("outputSnapshot", json);
        Assert.DoesNotContain("documentJson", json);
        Assert.DoesNotContain("ClientSecret", json);

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.Contains(
            status!.RecentEvents,
            item => item.Kind == "AuditExported" && item.Path == $"/api/audit/export/matches/{matchId}");
    }

    [Fact]
    public async Task Missing_match_case_file_is_not_found()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/audit/export/matches/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
