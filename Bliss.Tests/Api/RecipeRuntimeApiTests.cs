using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class RecipeRuntimeApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public RecipeRuntimeApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_creative_academy_exposes_a_separate_operator_only_zero_credit_demonstration()
    {
        var client = _factory.CreateClient();
        var page = await client.GetStringAsync("/production.html");
        var script = await client.GetStringAsync("/production.js");
        var sourceMediaScript = await client.GetStringAsync("/source-media/source-media.js");
        var composer = await client.GetStringAsync("/ad-composer.js");

        Assert.Contains("DEMONSTRATION / NO AI CREDITS", page);
        Assert.Contains("id=\"demo-generate\" type=\"button\" disabled", page);
        Assert.Contains("id=\"demo-evidence\" hidden", page);
        Assert.Contains("!session?.canWrite", script);
        Assert.Contains("runZeroCreditDemonstration", script);
        Assert.Contains("/api/demonstrations/abc-pharmacy/produce", script);
        Assert.Contains("/api/demonstrations/factory-batch", script);
        Assert.DoesNotContain("api(\"/api/operations/academy/generate\"", script);
        Assert.Contains("X-CSRF-TOKEN", sourceMediaScript);
        Assert.Contains("session?.canWrite", sourceMediaScript);
        Assert.Contains("id=\"composer-render\" type=\"button\" disabled", page);
        Assert.Contains("id=\"composer-preview\"", page);
        Assert.Contains("/ad-composer.js", page);
        Assert.Contains("renderBrandTemplatePng", script);
        Assert.Contains("new DataTransfer", script);
        Assert.Contains("!history?.brief", script);
        Assert.Contains("window.AlphaTemplateComposer", composer);
        Assert.Contains("toBlob(", composer);
        Assert.Contains("reader.readAsDataURL(blob)", script);
        Assert.DoesNotContain("$(\"#composer-preview\").src = composerObjectUrl", script);
        Assert.DoesNotContain("fetch(", composer);
        Assert.DoesNotContain("XMLHttpRequest", composer);
    }

    [Fact]
    public void Media_studio_limits_parser_protocols_and_bounds_subprocess_lifetime()
    {
        // This guard prevents future refactors from silently restoring
        // unbounded ffmpeg/ffprobe runtimes or arbitrary upload protocols.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? file = null;
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Bliss.Api",
                "Demonstrations", "DemonstrationVideoStudio.cs");
            if (File.Exists(candidate)) { file = candidate; break; }
            directory = directory.Parent;
        }
        Assert.NotNull(file);
        var code = File.ReadAllText(file!);
        Assert.Contains("\"-protocol_whitelist\", \"file,pipe\"", code);
        Assert.Contains("linked.CancelAfter(limit)", code);
        Assert.Contains("process.Kill(entireProcessTree: true)", code);
        Assert.Contains("process.StandardInput.Close()", code);
    }

    [Fact]
    public async Task Produce_stores_overlay_1_and_serves_the_player()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var slice = await client.PostAsJsonAsync(
            "/api/demonstrations/source-media/studio-slice",
            new { market = "Panama City", country = "Panama", seconds = 8 });
        Assert.Equal(HttpStatusCode.OK, slice.StatusCode);
        var clip = await slice.Content.ReadFromJsonAsync<JsonElement>();

        var produced = await client.PostAsJsonAsync(
            "/api/demonstrations/mesa-norte/produce",
            new { sourceClipId = clip!.GetProperty("id").GetGuid() });
        var page = await produced.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, produced.StatusCode);
        Assert.Equal("NOT_SENT", page!.GetProperty("delivery").GetString());
        Assert.False(page.GetProperty("campaignReady").GetBoolean());
        Assert.Equal(0, page.GetProperty("aiCalls").GetInt32());
        Assert.Contains("Mesa Norte has not sponsored", page.GetProperty("disclosure").GetString());
        var concept = page.GetProperty("concepts")[0];
        Assert.Equal("overlay-1", concept.GetProperty("recipeVersion").GetString());
        Assert.Equal("PASSED", concept.GetProperty("qaStatus").GetString());
        Assert.Equal("La Mesa", concept.GetProperty("headline").GetString());
        Assert.Contains("/demonstrations/mesa-norte", concept.GetProperty("qrDestination").GetString());

        Assert.Equal("PLAYER", concept.GetProperty("servedPicture").GetString());
        Assert.Contains("not required", concept.GetProperty("playerNotice").GetString());
        Assert.Contains("NOT_SENT", concept.GetProperty("playerNotice").GetString());
        Assert.False(concept.TryGetProperty("videoUrl", out _));
        var source = await client.GetAsync(concept.GetProperty("sourceUrl").GetString());
        var qr = await client.GetAsync(concept.GetProperty("qrUrl").GetString());
        var flat = await client.GetAsync("/api/demonstrations/mesa-norte/concepts/" + concept.GetProperty("id").GetString() + "/video");
        Assert.Equal(HttpStatusCode.OK, source.StatusCode);
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, flat.StatusCode);
        Assert.True(source.Content.Headers.ContentLength > 0);
        Assert.Equal("video/mp4", source.Content.Headers.ContentType?.MediaType);
    }
}
