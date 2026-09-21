using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase13CreatorCaseExportTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase13CreatorCaseExportTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Creator_case_file_includes_related_matches_without_snapshots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var creatorId = Phase2DataSeeder.BrazilCreatorId;
        var response = await client.GetAsync($"/api/audit/export/creators/{creatorId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("bliss-creator-", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        Assert.Equal("creator-case", root.GetProperty("kind").GetString());
        Assert.Equal(creatorId, root.GetProperty("creatorId").GetGuid());
        Assert.Equal(creatorId, root.GetProperty("creator").GetProperty("id").GetGuid());
        var matchIds = root.GetProperty("matches").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToHashSet();
        Assert.Contains(Phase2DataSeeder.MatchBrazilApprovedId, matchIds);

        var json = root.GetRawText();
        Assert.DoesNotContain("inputSnapshot", json);
        Assert.DoesNotContain("outputSnapshot", json);
        Assert.DoesNotContain("ClientSecret", json);

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.Contains(
            status!.RecentEvents,
            item => item.Kind == "AuditExported" && item.Path == $"/api/audit/export/creators/{creatorId}");
    }

    [Fact]
    public async Task Missing_creator_case_file_is_not_found()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/audit/export/creators/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
