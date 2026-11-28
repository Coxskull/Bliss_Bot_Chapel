using System.Text.Json;
using Bliss.Api.Operations;
using Bliss.Api.Runtime;

namespace Bliss.Tests.Api;

public sealed class AlphaCreativeImagePilotTests
{
    private static ControlledAiOptions Pilot() => new()
    {
        Enabled = true,
        ImagePilotEnabled = true,
        Protocol = "OpenAI",
        Provider = "OpenAI",
        ApiToken = "test-key-not-real",
        ImageEndpoint = "https://api.openai.com/v1/images/generations",
        ImageModel = "gpt-image-2.5-flare",
        MaxCallsPerDay = 11,
        DailyBudgetUsd = 20m,
        TotalBudgetUsd = 20m,
        ImageReservationUsd = 1.80m,
        TimeoutSeconds = 120,
        MaxResponseBytes = 24_000_000
    };

    [Fact]
    public void Image_pilot_requires_all_isolation_identity_and_budget_controls()
    {
        var p = Pilot();
        Assert.True(p.Configured("IMAGE"));
        p.ImagePilotEnabled = false; Assert.False(p.Configured("IMAGE")); p.ImagePilotEnabled = true;
        p.MaxResponseBytes = 2_000_000; Assert.False(p.Configured("IMAGE")); p.MaxResponseBytes = 24_000_000;
        p.TotalBudgetUsd = 20.01m; Assert.False(p.Configured("IMAGE")); p.TotalBudgetUsd = 20m;
        p.DailyBudgetUsd = 20.01m; Assert.False(p.Configured("IMAGE")); p.DailyBudgetUsd = 20m;
        p.ImageReservationUsd = 1.79m; Assert.False(p.Configured("IMAGE")); p.ImageReservationUsd = 1.80m;
        p.MaxCallsPerDay = 12; Assert.False(p.Configured("IMAGE")); p.MaxCallsPerDay = 11;
        p.ImageModel = "gpt-image-2.6-flare"; Assert.False(p.Configured("IMAGE")); p.ImageModel = "gpt-image-2.5-flare";
        p.ImageEndpoint = "https://attacker.example/generate"; Assert.False(p.Configured("IMAGE")); p.ImageEndpoint = "https://api.openai.com/v1/images/generations";
        p.Provider = "not-openai"; Assert.False(p.Configured("IMAGE")); p.Provider = "OpenAI";
        Assert.True(p.Configured("IMAGE"));
    }

    [Fact]
    public void Restricted_worker_pin_requires_all_identifiers_and_canonical_hash()
    {
        var pin = new RestrictedImageRunOptions();
        Assert.False(pin.Configured);
        pin.RunId = Guid.NewGuid();
        pin.WorkspaceId = Guid.NewGuid();
        pin.BriefId = Guid.NewGuid();
        pin.InputSha256 = new string('a', 64);
        Assert.True(pin.Configured);
        pin.InputSha256 = new string('A', 64);
        Assert.False(pin.Configured);
        pin.InputSha256 = new string('a', 63);
        Assert.False(pin.Configured);
        pin.InputSha256 = new string('a', 64);
        pin.BriefId = Guid.Empty;
        Assert.False(pin.Configured);
    }

    [Fact]
    public void Direct_images_api_usage_breakdown_has_exact_metered_usd()
    {
        using var doc = JsonDocument.Parse("""{"input_tokens":100,"input_tokens_details":{"text_tokens":80,"image_tokens":20},"output_tokens":1000}""");
        Assert.Equal(.03056m, ConfiguredControlledAiProvider.ImagePilotCost(doc.RootElement));
        using var invalid = JsonDocument.Parse("""{"input_tokens":100,"output_tokens":1000}""");
        Assert.Equal("IMAGE_USAGE_BREAKDOWN_MISSING",
            Assert.Throws<AiProviderException>(() => ConfiguredControlledAiProvider.ImagePilotCost(invalid.RootElement)).Code);
        using var inconsistent = JsonDocument.Parse("""{"input_tokens":100,"input_tokens_details":{"text_tokens":90,"image_tokens":20},"output_tokens":1000}""");
        Assert.Equal("IMAGE_USAGE_BREAKDOWN_INVALID",
            Assert.Throws<AiProviderException>(() => ConfiguredControlledAiProvider.ImagePilotCost(inconsistent.RootElement)).Code);
    }
}
