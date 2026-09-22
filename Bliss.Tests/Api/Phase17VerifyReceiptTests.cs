using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bliss.Api.Runtime;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase17VerifyReceiptTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase17VerifyReceiptTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Verification_receipt_includes_correlation_and_event_detail()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var pack = await (await client.GetAsync("/api/audit/export/evaluations")).Content.ReadAsStringAsync();
        var response = await client.PostAsync(
            "/api/audit/verify",
            new StringContent(pack, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var ids));
        var body = await response.Content.ReadFromJsonAsync<ExportVerificationDto>();
        Assert.True(body!.Matched);
        Assert.Equal("evaluations", body.PackKind);
        Assert.Equal(ids.Single(), body.RequestId);
        Assert.True(body.VerifiedAt.HasValue);
        Assert.DoesNotContain("inputSnapshot", pack);
        Assert.DoesNotContain("ClientSecret", pack);

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.Contains(
            status!.RecentEvents,
            item => item.Kind == "AuditVerified"
                && item.Path == "/api/audit/verify"
                && item.Detail != null
                && item.Detail.Contains("evaluations")
                && item.Detail.Contains("matched")
                && item.Detail.Contains(body.ComputedSha256[..12]));
    }

    [Fact]
    public async Task Mismatch_receipt_still_records_detail_without_storing_the_pack()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var exported = await (await client.GetAsync("/api/audit/export/evaluations")).Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(exported);
        var json = exported.Replace(
            document.RootElement.GetProperty("records")[0].GetProperty("status").GetString()!,
            "TAMPERED",
            StringComparison.Ordinal);
        var response = await client.PostAsync(
            "/api/audit/verify",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadFromJsonAsync<ExportVerificationDto>();
        Assert.False(body!.Matched);
        Assert.False(string.IsNullOrWhiteSpace(body.RequestId));

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.Contains(
            status!.RecentEvents,
            item => item.Kind == "AuditVerified"
                && item.Detail != null
                && item.Detail.Contains("mismatch"));
    }
}
