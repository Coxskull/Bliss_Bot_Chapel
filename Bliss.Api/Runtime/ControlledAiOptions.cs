namespace Bliss.Api.Runtime;

public sealed class ControlledAiOptions
{
    public bool Enabled { get; set; }
    // Explicit isolated commercial-creative pilot; off by default in every environment.
    public bool ImagePilotEnabled { get; set; }
    // A future isolated worker may consume only this exact pre-authorized run.
    // This configuration alone does not register or start a worker.
    public RestrictedImageRunOptions RestrictedImageRun { get; set; } = new();
    public string Protocol { get; set; } = "Gateway";
    public string Provider { get; set; } = "";
    public string TextEndpoint { get; set; } = "";
    public string ImageEndpoint { get; set; } = "";
    public string ApiToken { get; set; } = "";
    public string TextModel { get; set; } = "";
    public string ImageModel { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxCallsPerDay { get; set; }
    public decimal DailyBudgetUsd { get; set; }
    public decimal TotalBudgetUsd { get; set; }
    public decimal TextReservationUsd { get; set; }
    public decimal ImageReservationUsd { get; set; }
    public int MaxOutputTokens { get; set; } = 1000;
    public int MaxResponseBytes { get; set; } = 1_450_000;
    public bool Configured(string kind) => Enabled
        // Never let the public/ungated image route activate an OpenAI key.
        && (kind != "IMAGE" || Protocol != "OpenAI" ||
            (ImagePilotEnabled
             && string.Equals(Provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
             && ImageModel == "gpt-image-2.5-flare"
             && ImageEndpoint == "https://api.openai.com/v1/images/generations"
             && TotalBudgetUsd <= 20m && DailyBudgetUsd <= 20m
             && ImageReservationUsd >= 1.80m && MaxCallsPerDay <= 11
             // A 1 MiB generic response limit would reject many real paid
             // 1024x1024 PNGs AFTER billing. Cap and size the private pilot
             // response envelope for a 16 MiB generated PNG.
             && MaxResponseBytes >= 22_500_000 && MaxResponseBytes <= 24_000_000))
        && !string.IsNullOrWhiteSpace(Provider)
        && !string.IsNullOrWhiteSpace(ApiToken) && (Protocol is "Gateway" or "OpenAI")
        && TimeoutSeconds is >= 1 and <= 300 && MaxCallsPerDay > 0 && DailyBudgetUsd > 0 && TotalBudgetUsd > 0
        && MaxOutputTokens is > 0 and <= 2000 && MaxResponseBytes is >= 1024 and <= 24_000_000
        && (Protocol == "OpenAI" && kind == "IMAGE" && ImagePilotEnabled || MaxResponseBytes <= 2_000_000)
        && (kind == "TEXT" ? TextReservationUsd : ImageReservationUsd) > 0
        && (kind == "TEXT" ? TextReservationUsd : ImageReservationUsd) <= DailyBudgetUsd
        && !string.IsNullOrWhiteSpace(kind == "TEXT" ? TextModel : ImageModel)
        && Uri.TryCreate(kind == "TEXT" ? TextEndpoint : ImageEndpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
}

public sealed class RestrictedImageRunOptions
{
    public Guid RunId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid BriefId { get; set; }
    public string InputSha256 { get; set; } = "";

    public bool Configured => RunId != Guid.Empty && WorkspaceId != Guid.Empty
        && BriefId != Guid.Empty && InputSha256 is { Length: 64 }
        && InputSha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
