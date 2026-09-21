using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bliss.Api.Runtime;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase16ExportVerifyTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase16ExportVerifyTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Exported_ledger_and_case_packs_verify_as_matched()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        await AssertMatched(client, "/api/audit/export/evaluations", "evaluations");
        await AssertMatched(client, $"/api/audit/export/matches/{Phase2DataSeeder.MatchBrazilApprovedId}", "match-case");
        await AssertMatched(client, $"/api/audit/export/creators/{Phase2DataSeeder.BrazilCreatorId}", "creator-case");
        await AssertMatched(client, $"/api/audit/export/campaigns/{Phase2DataSeeder.Campaign2Id}", "campaign-case");

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.Contains(status!.RecentEvents, item => item.Kind == "AuditVerified" && item.Path == "/api/audit/verify");
    }

    [Fact]
    public async Task Tampered_pack_is_reported_as_mismatch()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var exported = await client.GetAsync("/api/audit/export/evaluations");
        using var document = JsonDocument.Parse(await exported.Content.ReadAsStringAsync());
        var json = document.RootElement.GetRawText().Replace(
            document.RootElement.GetProperty("records")[0].GetProperty("status").GetString()!,
            "TAMPERED",
            StringComparison.Ordinal);

        var response = await client.PostAsync(
            "/api/audit/verify",
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExportVerificationDto>();
        Assert.False(body!.Matched);
        Assert.Equal("evaluations", body.PackKind);
        Assert.NotEqual(body.DeclaredSha256, body.ComputedSha256);
        Assert.DoesNotContain("inputSnapshot", json);
        Assert.DoesNotContain("ClientSecret", json);
    }

    [Fact]
    public async Task Unsupported_or_invalid_packs_are_rejected()
    {
        var client = _factory.CreateClient();
        var missing = await client.PostAsJsonAsync("/api/audit/verify", new { ledger = "evaluations", records = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("contentSha256", await missing.Content.ReadAsStringAsync());

        var unknown = await client.PostAsJsonAsync("/api/audit/verify", new { kind = "payments-case", contentSha256 = new string('a', 64) });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var invalid = await client.PostAsync(
            "/api/audit/verify",
            new StringContent("not-json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("valid JSON", await invalid.Content.ReadAsStringAsync());
    }

    private static async Task AssertMatched(HttpClient client, string exportPath, string expectedKind)
    {
        var exported = await client.GetAsync(exportPath);
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        var pack = await exported.Content.ReadAsStringAsync();
        var response = await client.PostAsync(
            "/api/audit/verify",
            new StringContent(pack, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExportVerificationDto>();
        Assert.True(body!.Matched);
        Assert.Equal(expectedKind, body.PackKind);
        Assert.Equal(body.DeclaredSha256, body.ComputedSha256);
    }
}
