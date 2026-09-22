using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bliss.Api.Runtime;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase18LastVerificationTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase18LastVerificationTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Status_exposes_the_last_verification_receipt_without_the_pack()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonStatus>("/api/runtime/status");
        Assert.Null(before!.LastVerification);

        var pack = await (await client.GetAsync($"/api/audit/export/matches/{Phase2DataSeeder.MatchBrazilApprovedId}")).Content.ReadAsStringAsync();
        var verified = await client.PostAsync(
            "/api/audit/verify",
            new StringContent(pack, Encoding.UTF8, "application/json"));
        var receipt = await verified.Content.ReadFromJsonAsync<ExportVerificationDto>();
        Assert.True(receipt!.Matched);

        var after = await client.GetFromJsonAsync<JsonStatus>("/api/runtime/status");
        Assert.NotNull(after!.LastVerification);
        Assert.Equal("match-case", after.LastVerification.PackKind);
        Assert.True(after.LastVerification.Matched);
        Assert.Equal(receipt.ComputedSha256, after.LastVerification.ComputedSha256);
        Assert.Equal(receipt.RequestId, after.LastVerification.RequestId);
        Assert.DoesNotContain("inputSnapshot", pack);
        Assert.DoesNotContain("ClientSecret", System.Text.Json.JsonSerializer.Serialize(after));
    }

    private sealed record JsonStatus(ExportVerificationDto? LastVerification);
}
