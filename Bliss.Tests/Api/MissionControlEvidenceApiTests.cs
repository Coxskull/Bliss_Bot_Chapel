using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class MissionControlEvidenceApiTests
{
    [Fact]
    public async Task One_evidence_id_binds_the_report_and_the_video()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var probe = await client.PostAsync("/api/operations/mission-control/retrieval-probe", null);
        var body = await probe.Content.ReadFromJsonAsync<JsonElement>();
        var manifest = body.GetProperty("manifest");
        var evidenceId = manifest.GetProperty("evidenceId").GetString()!;
        var files = manifest.GetProperty("files").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, probe.StatusCode);
        Assert.StartsWith("Mission Control evidence ready: " + evidenceId, body.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("NOT_CONNECTED", body.GetProperty("driveStatus").GetString());
        Assert.Equal("NOT_RUN", body.GetProperty("chatgptRetrieval").GetString());
        Assert.Equal("NOT_REVIEWED", manifest.GetProperty("finalReviewResult").GetString());
        Assert.Equal(["REPORT", "VIDEO"], files.Select(item => item.GetProperty("evidenceType").GetString()).ToArray());
        Assert.All(files, item => Assert.StartsWith(evidenceId + "-", item.GetProperty("fileName").GetString(), StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-REPORT.pdf")));
        Assert.True(File.Exists(Path.Combine(factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-VIDEO.mp4")));
        Assert.True(File.Exists(Path.Combine(factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-MANIFEST.json")));

        var stored = await client.GetFromJsonAsync<JsonElement>("/api/operations/mission-control/evidence/" + evidenceId);
        Assert.Equal(evidenceId, stored.GetProperty("evidenceId").GetString());
    }

    [Fact]
    public async Task A_retest_keeps_a_new_id_linked_to_the_original()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var issued = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "MC-EVIDENCE-ID",
            testName = "Mission Control retrieval probe",
            testCategory = "evidence-retrieval",
            submittedBy = "Erwin",
            claimedResult = "BLOCKED"
        });
        var original = await issued.Content.ReadFromJsonAsync<JsonElement>();
        var originalId = original.GetProperty("evidenceId").GetString();
        var early = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + originalId + "/retest",
            new { submittedBy = "Erwin", retestReason = "RETEST AFTER CORRECTION" });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);

        var review = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + originalId + "/review",
            new { reviewer = "Independent", reviewerRole = "REVIEWER", result = "CORRECTION_REQUIRED" });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var retest = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + originalId + "/retest",
            new { submittedBy = "Erwin", retestReason = "RETEST AFTER CORRECTION" });
        var child = await retest.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, retest.StatusCode);
        Assert.NotEqual(originalId, child.GetProperty("evidenceId").GetString());
        Assert.Equal(originalId, child.GetProperty("parentEvidenceId").GetString());
        Assert.Equal("RETEST AFTER CORRECTION", child.GetProperty("retestReason").GetString());
        Assert.Equal("NOT_REVIEWED", child.GetProperty("finalReviewResult").GetString());

        var still = await client.GetFromJsonAsync<JsonElement>("/api/operations/mission-control/evidence/" + originalId);
        Assert.Equal("CORRECTION_REQUIRED", still.GetProperty("finalReviewResult").GetString());
        Assert.False(still.TryGetProperty("parentEvidenceId", out var parent) && parent.ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task Owner_pricing_and_self_review_stay_refused()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var issued = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "ARE-ECONOMICS",
            testName = "Economics price",
            testCategory = "economics",
            submittedBy = "Erwin"
        });
        var manifest = await issued.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(manifest.GetProperty("ownerDecisionRequired").GetBoolean());
        var review = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + manifest.GetProperty("evidenceId").GetString() + "/review",
            new { reviewer = "ChatGPT", reviewerRole = "REVIEWER", result = "PASS" });
        Assert.Equal(HttpStatusCode.BadRequest, review.StatusCode);

        var priced = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "MC-EVIDENCE-ID",
            testName = "Cost",
            testCategory = "evidence-retrieval",
            submittedBy = "Erwin",
            costStatus = "0"
        });
        Assert.Equal(HttpStatusCode.BadRequest, priced.StatusCode);

        var selected = await client.PostAsJsonAsync("/api/operations/mission-control/validation-catalog/select", new { seed = 1 });
        var choice = await selected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NOT_RUN", choice.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(choice.GetProperty("test").GetProperty("name").GetString()));
    }

    [Fact]
    public async Task A_validation_run_files_an_observation_and_stays_unreviewed()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var run = await client.PostAsJsonAsync("/api/operations/mission-control/validation-catalog/run", new { seed = 0 });
        var body = await run.Content.ReadFromJsonAsync<JsonElement>();
        var manifest = body.GetProperty("manifest");
        var evidenceId = manifest.GetProperty("evidenceId").GetString()!;
        var logPath = Path.Combine(factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-LOG.txt");

        Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        Assert.Equal("OBSERVED", body.GetProperty("claimedResult").GetString());
        Assert.Equal("NOT_REVIEWED", body.GetProperty("finalReviewResult").GetString());
        Assert.Equal("NOT_CONNECTED", body.GetProperty("driveStatus").GetString());
        Assert.Equal("NOT_RUN", body.GetProperty("chatgptRetrieval").GetString());
        Assert.Equal("LOG", manifest.GetProperty("files")[0].GetProperty("evidenceType").GetString());
        Assert.Contains("RETRIEVED", await File.ReadAllTextAsync(logPath));

        var economics = await client.PostAsJsonAsync("/api/operations/mission-control/validation-catalog/run", new { seed = 6 });
        var priced = await economics.Content.ReadFromJsonAsync<JsonElement>();
        var pricedManifest = priced.GetProperty("manifest");
        Assert.True(pricedManifest.GetProperty("ownerDecisionRequired").GetBoolean());
        Assert.Equal("NOT_REVIEWED", pricedManifest.GetProperty("finalReviewResult").GetString());
        var review = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + pricedManifest.GetProperty("evidenceId").GetString() + "/review",
            new { reviewer = "ChatGPT", reviewerRole = "REVIEWER", result = "PASS" });
        Assert.Equal(HttpStatusCode.BadRequest, review.StatusCode);
    }
}

public sealed class MissionControlApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "bliss-mission-control", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("MissionControl:Root", Root);
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                    || d.ServiceType == typeof(BlissDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(_dbName));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
