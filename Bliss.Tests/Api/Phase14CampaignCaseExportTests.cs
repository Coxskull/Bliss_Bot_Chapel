using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase14CampaignCaseExportTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase14CampaignCaseExportTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Campaign_case_file_includes_planned_placements_without_snapshots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var campaignId = Phase2DataSeeder.Campaign2Id;
        var response = await client.GetAsync($"/api/audit/export/campaigns/{campaignId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("bliss-campaign-", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        Assert.Equal("campaign-case", root.GetProperty("kind").GetString());
        Assert.Equal(campaignId, root.GetProperty("campaignId").GetGuid());
        Assert.Equal("Phase 2 Overlay Inventory Campaign", root.GetProperty("campaign").GetProperty("name").GetString());
        Assert.True(root.GetProperty("placements").GetArrayLength() >= 1);

        var json = root.GetRawText();
        Assert.DoesNotContain("inputSnapshot", json);
        Assert.DoesNotContain("ClientSecret", json);

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.Contains(
            status!.RecentEvents,
            item => item.Kind == "AuditExported" && item.Path == $"/api/audit/export/campaigns/{campaignId}");
    }

    [Fact]
    public async Task Missing_campaign_case_file_is_not_found()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/audit/export/campaigns/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
