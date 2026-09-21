using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase11AuditExportTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase11AuditExportTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Evaluation_export_is_a_summary_pack_with_request_id()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/audit/export/evaluations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("bliss-evaluations-", response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "");
        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var ids));
        Assert.True(Guid.TryParse(ids.Single(), out _));

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        Assert.Equal("evaluations", root.GetProperty("ledger").GetString());
        Assert.Equal("Development", root.GetProperty("environment").GetString());
        Assert.False(root.GetProperty("truncated").GetBoolean());
        Assert.True(root.GetProperty("recordCount").GetInt32() >= 2);
        var records = root.GetProperty("records").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Contains(Phase3DataSeeder.RunBrazilFirstId, records);
        Assert.Contains(Phase3DataSeeder.RunBrazilSecondId, records);

        var json = root.GetRawText();
        Assert.DoesNotContain("inputSnapshot", json);
        Assert.DoesNotContain("outputSnapshot", json);
        Assert.DoesNotContain("ConnectionStrings", json);
        Assert.DoesNotContain("ClientSecret", json);

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.Contains(status!.RecentEvents, item => item.Kind == "AuditExported" && item.Path == "/api/audit/export/evaluations");
    }

    [Fact]
    public async Task Unknown_ledger_is_rejected()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/audit/export/payments");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unsupported ledger", body);
    }
}
