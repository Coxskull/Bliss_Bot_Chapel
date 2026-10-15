namespace Bliss.Api.Runtime;

public sealed class ControlledAiOptions
{
    public bool Enabled { get; set; }
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
    public bool Configured(string kind) => Enabled && !string.IsNullOrWhiteSpace(Provider)
        && !string.IsNullOrWhiteSpace(ApiToken) && (Protocol is "Gateway" or "OpenAI")
        && TimeoutSeconds is >= 1 and <= 300 && MaxCallsPerDay > 0 && DailyBudgetUsd > 0 && TotalBudgetUsd > 0
        && MaxOutputTokens is > 0 and <= 2000 && MaxResponseBytes is >= 1024 and <= 2_000_000
        && (kind == "TEXT" ? TextReservationUsd : ImageReservationUsd) > 0
        && (kind == "TEXT" ? TextReservationUsd : ImageReservationUsd) <= DailyBudgetUsd
        && !string.IsNullOrWhiteSpace(kind == "TEXT" ? TextModel : ImageModel)
        && Uri.TryCreate(kind == "TEXT" ? TextEndpoint : ImageEndpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
}
