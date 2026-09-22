using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bliss.Api.Runtime;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase19VerificationHistoryTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase19VerificationHistoryTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Status_keeps_recent_verification_receipts_without_the_pack()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var evaluations = await (await client.GetAsync("/api/audit/export/evaluations")).Content.ReadAsStringAsync();
        var match = await (await client.GetAsync(
            $"/api/audit/export/matches/{Phase2DataSeeder.MatchBrazilApprovedId}")).Content.ReadAsStringAsync();

        var first = await Verify(client, evaluations);
        var second = await Verify(client, match);
        Assert.True(first.Matched);
        Assert.True(second.Matched);
        Assert.Equal("evaluations", first.PackKind);
        Assert.Equal("match-case", second.PackKind);

        var after = await client.GetFromJsonAsync<JsonStatus>("/api/runtime/status");
        Assert.NotNull(after!.LastVerification);
        Assert.Equal(second.RequestId, after.LastVerification.RequestId);
        Assert.Equal("match-case", after.LastVerification.PackKind);
        Assert.NotEmpty(after.RecentVerifications);
        Assert.Equal(after.LastVerification.RequestId, after.RecentVerifications[0].RequestId);
        Assert.Contains(after.RecentVerifications, item => item.RequestId == first.RequestId && item.PackKind == "evaluations");
        Assert.True(after.RecentVerifications.Length <= OperationalEventStore.VerificationHistoryLimit);
        Assert.DoesNotContain("inputSnapshot", evaluations);
        Assert.DoesNotContain("inputSnapshot", match);
        Assert.DoesNotContain("ClientSecret", JsonSerializer.Serialize(after));
    }

    [Fact]
    public void Verification_history_drops_receipts_beyond_the_limit()
    {
        var store = new OperationalEventStore();
        for (var index = 0; index < OperationalEventStore.VerificationHistoryLimit + 3; index++)
        {
            store.RememberVerification(new ExportVerificationDto(
                "evaluations",
                new string('a', 64),
                new string('a', 64),
                true,
                Guid.NewGuid().ToString("D"),
                DateTime.UtcNow));
        }

        var history = store.RecentVerifications();
        Assert.Equal(OperationalEventStore.VerificationHistoryLimit, history.Count);
        Assert.Equal(store.LastVerification!.RequestId, history[0].RequestId);
    }

    private static async Task<ExportVerificationDto> Verify(HttpClient client, string pack)
    {
        var response = await client.PostAsync(
            "/api/audit/verify",
            new StringContent(pack, Encoding.UTF8, "application/json"));
        var receipt = await response.Content.ReadFromJsonAsync<ExportVerificationDto>();
        return receipt!;
    }

    private sealed record JsonStatus(
        ExportVerificationDto? LastVerification,
        ExportVerificationDto[] RecentVerifications);
}
