using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Operations;
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
    public async Task Screenshot_upload_rejects_non_png_bytes_before_writing()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var issued = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "MC-SCREENSHOT",
            testName = "Screenshot validation",
            testCategory = "evidence-retrieval",
            submittedBy = "Erwin",
            claimedResult = "BLOCKED"
        });
        var manifest = await issued.Content.ReadFromJsonAsync<JsonElement>();
        var evidenceId = manifest.GetProperty("evidenceId").GetString()!;
        var response = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + evidenceId + "/files",
            new { evidenceType = "SCREENSHOT", contentBase64 = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(File.Exists(Path.Combine(
            factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-SCREENSHOT-01.png")));
    }

    [Fact]
    public async Task Screenshot_upload_rejects_a_png_header_without_decodable_image_data()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var issued = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "MC-SCREENSHOT-DECODE",
            testName = "PNG decoder validation",
            testCategory = "visual-qa",
            submittedBy = "Erwin",
            claimedResult = "BLOCKED"
        });
        var manifest = await issued.Content.ReadFromJsonAsync<JsonElement>();
        var evidenceId = manifest.GetProperty("evidenceId").GetString()!;
        byte[] headerOnlyPng = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1];

        var response = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + evidenceId + "/files",
            new { evidenceType = "SCREENSHOT", contentBase64 = Convert.ToBase64String(headerOnlyPng) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(File.Exists(Path.Combine(
            factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-SCREENSHOT-01.png")));
    }

    [Theory]
    [InlineData(20_000u, 1u)]
    [InlineData(10_000u, 10_000u)]
    public async Task Screenshot_upload_rejects_dimensions_that_can_exhaust_decoder_resources(uint width, uint height)
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var issued = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "MC-SCREENSHOT-DIMENSIONS",
            testName = "PNG dimension limits",
            testCategory = "visual-qa",
            submittedBy = "Erwin",
            claimedResult = "BLOCKED"
        });
        var manifest = await issued.Content.ReadFromJsonAsync<JsonElement>();
        var evidenceId = manifest.GetProperty("evidenceId").GetString()!;
        var oversizedHeader = PngHeader(width, height);

        var response = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + evidenceId + "/files",
            new { evidenceType = "SCREENSHOT", contentBase64 = Convert.ToBase64String(oversizedHeader) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("dimensions", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(
            factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-SCREENSHOT-01.png")));
    }

    private static byte[] PngHeader(uint width, uint height)
    {
        byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 0, 0, 0, 0, 0];
        bytes[16] = (byte)(width >> 24);
        bytes[17] = (byte)(width >> 16);
        bytes[18] = (byte)(width >> 8);
        bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24);
        bytes[21] = (byte)(height >> 16);
        bytes[22] = (byte)(height >> 8);
        bytes[23] = (byte)height;
        return bytes;
    }

    [Fact]
    public async Task Visual_artifact_verification_rejects_a_tampered_screenshot()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var evidenceId = await UploadOnePixelScreenshotAsync(client);
        var screenshotPath = Path.Combine(factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-SCREENSHOT-01.png");
        await File.WriteAllBytesAsync(screenshotPath, [0, 1, 2, 3]);

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MissionControlEvidenceService>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyVisualArtifactAsync(evidenceId, CancellationToken.None));

        Assert.Contains("hash", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Visual_artifact_verification_rejects_a_missing_screenshot()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var evidenceId = await UploadOnePixelScreenshotAsync(client);
        var screenshotPath = Path.Combine(factory.Root, "02 — EVIDENCE SUBMITTED", evidenceId, evidenceId + "-SCREENSHOT-01.png");
        File.Delete(screenshotPath);

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MissionControlEvidenceService>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyVisualArtifactAsync(evidenceId, CancellationToken.None));

        Assert.Contains("missing", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> UploadOnePixelScreenshotAsync(HttpClient client)
    {
        var issued = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "MC-SCREENSHOT-VERIFY",
            testName = "Screenshot integrity",
            testCategory = "visual-qa",
            submittedBy = "Erwin",
            claimedResult = "BLOCKED"
        });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var manifest = await issued.Content.ReadFromJsonAsync<JsonElement>();
        var evidenceId = manifest.GetProperty("evidenceId").GetString()!;
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var upload = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + evidenceId + "/files",
            new { evidenceType = "SCREENSHOT", contentBase64 = Convert.ToBase64String(png) });
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        return evidenceId;
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

    [Fact]
    public async Task An_upload_folder_keeps_one_id_together_and_does_not_upload()
    {
        await using var factory = new MissionControlApiFactory();
        var client = factory.CreateClient();
        var probe = await client.PostAsync("/api/operations/mission-control/retrieval-probe", null);
        var probeBody = await probe.Content.ReadFromJsonAsync<JsonElement>();
        var evidenceId = probeBody.GetProperty("evidenceId").GetString()!;
        var issued = await client.PostAsJsonAsync("/api/operations/mission-control/evidence", new
        {
            taskId = "MC-EVIDENCE-ID",
            testName = "Correction sample",
            testCategory = "evidence-retrieval",
            submittedBy = "Erwin",
            claimedResult = "BLOCKED"
        });
        var original = await issued.Content.ReadFromJsonAsync<JsonElement>();
        var originalId = original.GetProperty("evidenceId").GetString()!;
        var review = await client.PostAsJsonAsync(
            "/api/operations/mission-control/evidence/" + originalId + "/review",
            new { reviewer = "Independent", reviewerRole = "REVIEWER", result = "CORRECTION_REQUIRED" });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var export = await client.PostAsync("/api/operations/mission-control/upload-folder", null);
        var body = await export.Content.ReadFromJsonAsync<JsonElement>();
        var root = Path.Combine(factory.Root, "ALPHA — ERWIN ↔ CHATGPT MISSION CONTROL");
        var package = Path.Combine(root, "02 — EVIDENCE SUBMITTED", evidenceId);

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.False(body.GetProperty("uploadPerformed").GetBoolean());
        Assert.Equal("NOT_CONNECTED", body.GetProperty("driveStatus").GetString());
        Assert.Equal("NOT_RUN", body.GetProperty("chatgptRetrieval").GetString());
        Assert.Equal(evidenceId, body.GetProperty("assignmentEvidenceId").GetString());
        Assert.True(File.Exists(Path.Combine(package, evidenceId + "-REPORT.pdf")));
        Assert.True(File.Exists(Path.Combine(package, evidenceId + "-VIDEO.mp4")));
        Assert.True(File.Exists(Path.Combine(package, evidenceId + "-MANIFEST.json")));
        Assert.Contains("Assess " + evidenceId, await File.ReadAllTextAsync(Path.Combine(root, "01 — CURRENT ASSIGNMENT", "CURRENT-ASSIGNMENT.txt")));
        Assert.True(File.Exists(Path.Combine(root, "04 — CORRECTIONS REQUIRED", originalId, originalId + "-MANIFEST.json")));
        Assert.False(Directory.Exists(Path.Combine(root, "02 — EVIDENCE SUBMITTED", originalId)));
        Assert.True(File.Exists(Path.Combine(root, "03 — CHATGPT REVIEW", "EMPTY.txt")));
        var index = await File.ReadAllTextAsync(Path.Combine(root, "EVIDENCE-INDEX.json"));
        Assert.Contains(evidenceId, index);
        Assert.Contains("\"uploadPerformed\": false", index);
        Assert.DoesNotContain("password=", index, StringComparison.OrdinalIgnoreCase);
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
