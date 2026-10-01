using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class ProspectDemonstrationApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public ProspectDemonstrationApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Studio_slice_duplicate_and_pharmacy_page_keep_the_quota_and_contact_rules()
    {
        var client = _factory.CreateClient();
        var first = await client.PostAsJsonAsync(
            "/api/demonstrations/source-media/studio-slice",
            new { market = "Panama City", country = "Panama", seconds = 8 });
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("QUALIFIED", firstBody.GetProperty("status").GetString());
        Assert.Equal(1, firstBody.GetProperty("quotaCredit").GetInt32());

        var clipPath = await WriteClipAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4"));
        var uploaded = await UploadAsync(client, clipPath, "panama-slice.mp4");
        Assert.Equal("QUALIFIED", uploaded.GetProperty("status").GetString());
        var renamed = await UploadAsync(client, clipPath, "panama-slice-renamed.mp4");
        Assert.Equal("DUPLICATE", renamed.GetProperty("status").GetString());
        Assert.Equal(0, renamed.GetProperty("quotaCredit").GetInt32());

        var reencoded = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
        await RunAsync("ffmpeg", "-y", "-i", clipPath, "-an", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "30", reencoded);
        var near = await UploadAsync(client, reencoded, "panama-slice-reencoded.mp4");
        Assert.Equal("NEAR_DUPLICATE", near.GetProperty("status").GetString());
        Assert.Equal(0, near.GetProperty("quotaCredit").GetInt32());

        var roles = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/buying-roles?niche=pharmacy");
        Assert.Contains("Marketing manager", roles.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));

        var produce = await client.PostAsJsonAsync(
            "/api/demonstrations/abc-pharmacy/produce",
            new { sourceClipId = firstBody.GetProperty("id").GetGuid(), conceptCount = 1 });
        var page = await produce.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, produce.StatusCode);
        Assert.Equal("UNVERIFIED", page!.GetProperty("decisionMakerStatus").GetString());
        Assert.Equal("NOT_SENT", page.GetProperty("delivery").GetString());
        Assert.Equal("TIER_4", page.GetProperty("contactTier").GetString());
        Assert.Single(page.GetProperty("concepts").EnumerateArray());
        Assert.Contains("has not sponsored", page.GetProperty("disclosure").GetString());

        var concept = page.GetProperty("concepts")[0];
        var video = await client.GetAsync(concept.GetProperty("videoUrl").GetString());
        var qr = await client.GetAsync(concept.GetProperty("qrUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, video.StatusCode);
        Assert.Equal("video/mp4", video.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal("image/png", qr.Content.Headers.ContentType?.MediaType);
        var qrBytes = await qr.Content.ReadAsByteArrayAsync();
        Assert.Equal(0x89, qrBytes[0]);
        Assert.Contains("/demonstrations/abc-pharmacy", concept.GetProperty("qrDestination").GetString());

        var reply = await client.PostAsJsonAsync(
            "/api/demonstrations/abc-pharmacy/messages",
            new { text = "Is Maria González the marketing director?" });
        var conversation = await reply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);
        var answer = conversation!.GetProperty("reply").GetString()!;
        Assert.Contains("not verified", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Maria González is the", answer);

        var library = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/library");
        Assert.True(library!.GetProperty("qualifiedUnique").GetInt32() >= 1);
        Assert.True(library.GetProperty("duplicates").GetInt32() >= 2);
        Assert.Equal("SHORTAGE", library.GetProperty("fuelStatus").GetString());
        Assert.Equal(20, library.GetProperty("dailyTarget").GetInt32());

        var html = await client.GetAsync("/demonstrations/abc-pharmacy");
        var outreach = await client.GetAsync("/outreach/abc-pharmacy");
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.Equal(HttpStatusCode.OK, outreach.StatusCode);
        Assert.Contains("prospect.js", await html.Content.ReadAsStringAsync());
    }

    private static async Task<JsonElement> UploadAsync(HttpClient client, string path, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(await File.ReadAllBytesAsync(path));
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");
        content.Add(bytes, "file", fileName);
        content.Add(new StringContent("Panama City"), "market");
        content.Add(new StringContent("Panama"), "country");
        content.Add(new StringContent("es"), "culture");
        var response = await client.PostAsync("/api/demonstrations/source-media", content);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }

    private static async Task<string> WriteClipAsync(string path)
    {
        await RunAsync(
            "ffmpeg",
            "-y", "-f", "lavfi", "-i", "color=c=0x204060:s=320x180:d=8",
            "-f", "lavfi", "-i", "sine=frequency=440:duration=8",
            "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac",
            path);
        return path;
    }

    private static async Task RunAsync(string fileName, params string[] args)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;
        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error);
        }
    }
}
