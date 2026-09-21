using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bliss.Api.Runtime;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase15ExportIntegrityTests : IClassFixture<BlissApiFactory>
{
    private static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled);
    private readonly BlissApiFactory _factory;

    public Phase15ExportIntegrityTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ledger_pack_digest_covers_records_and_ignores_export_metadata()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var first = await client.GetAsync("/api/audit/export/evaluations");
        await Task.Delay(1100);
        var second = await client.GetAsync("/api/audit/export/evaluations");

        var firstRoot = await ReadRoot(first);
        var secondRoot = await ReadRoot(second);

        var digest = AssertDigest(first, firstRoot);
        Assert.Equal(digest, AssertDigest(second, secondRoot));
        Assert.NotEqual(firstRoot.GetProperty("exportedAt").GetString(), secondRoot.GetProperty("exportedAt").GetString());
        Assert.NotEqual(firstRoot.GetProperty("requestId").GetString(), secondRoot.GetProperty("requestId").GetString());

        var expected = ExportIntegrity.Sha256Hex(firstRoot.GetProperty("records"));
        Assert.Equal(expected, digest);
        Assert.DoesNotContain("inputSnapshot", firstRoot.GetRawText());
        Assert.DoesNotContain("ClientSecret", firstRoot.GetRawText());
    }

    [Fact]
    public async Task Match_creator_and_campaign_packs_include_stable_content_digests()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        await AssertStableCase(
            client,
            $"/api/audit/export/matches/{Phase2DataSeeder.MatchBrazilApprovedId}",
            "match", "scoreComponents", "eligibilityChecks", "evaluations", "formations", "reviews", "placements");
        await AssertStableCase(
            client,
            $"/api/audit/export/creators/{Phase2DataSeeder.BrazilCreatorId}",
            "creator", "platforms", "content", "matches", "ingestions", "provenances");
        await AssertStableCase(
            client,
            $"/api/audit/export/campaigns/{Phase2DataSeeder.Campaign2Id}",
            "campaign", "placements", "placementRuns", "matches");
    }

    [Fact]
    public async Task Missing_or_unsupported_exports_omit_content_digest()
    {
        var client = _factory.CreateClient();
        var missing = await client.GetAsync($"/api/audit/export/matches/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.False(missing.Headers.Contains(ExportIntegrity.HeaderName));

        var unsupported = await client.GetAsync("/api/audit/export/payments");
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.False(unsupported.Headers.Contains(ExportIntegrity.HeaderName));
    }

    [Fact]
    public void Sha256_hex_is_lowercase_and_stable()
    {
        var payload = new[] { new { id = Guid.Parse("11111111-1111-1111-1111-111111111111"), name = "alpha" } };
        var once = ExportIntegrity.Sha256Hex(payload);
        var twice = ExportIntegrity.Sha256Hex(payload);
        Assert.Equal(once, twice);
        Assert.Matches(Hex64, once);
        Assert.NotEqual(once, ExportIntegrity.Sha256Hex(new[] { new { id = Guid.NewGuid(), name = "beta" } }));
        Assert.Equal(ExportIntegrity.Sha256Hex(payload), once);
    }

    private static async Task AssertStableCase(HttpClient client, string path, params string[] domainProperties)
    {
        var first = await client.GetAsync(path);
        var second = await client.GetAsync(path);
        var firstRoot = await ReadRoot(first);
        var secondRoot = await ReadRoot(second);
        var digest = AssertDigest(first, firstRoot);
        Assert.Equal(digest, AssertDigest(second, secondRoot));

        var domain = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var name in domainProperties)
        {
            domain[name] = firstRoot.GetProperty(name);
        }

        Assert.Equal(ExportIntegrity.Sha256Hex(domain), digest);
        Assert.DoesNotContain("inputSnapshot", firstRoot.GetRawText());
        Assert.DoesNotContain("ClientSecret", firstRoot.GetRawText());
    }

    private static string AssertDigest(HttpResponseMessage response, JsonElement root)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues(ExportIntegrity.HeaderName, out var values));
        var header = values.Single();
        var body = root.GetProperty("contentSha256").GetString();
        Assert.Equal(header, body);
        Assert.Matches(Hex64, header);
        return header!;
    }

    private static async Task<JsonElement> ReadRoot(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }
}
