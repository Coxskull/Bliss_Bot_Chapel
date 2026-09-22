using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bliss.Domain.WeddingPlanner;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Vendor-neutral HTTP JSON adapter for Curator source acquisition.
/// Uses named <see cref="HttpClient"/> only — no vendor SDK. Secrets are never included in thrown messages.
/// The app never fetches URLs returned inside the source catalog.
/// </summary>
public sealed class RemoteHttpWeddingPlannerResearchProvider : IWeddingPlannerResearchProvider
{
    public const string ProviderKey = "remote-http-research";
    public const string AdapterVersion = "wp-research-remote-http-adapter.v1";
    public const string HttpClientName = "WeddingPlannerResearchRemoteHttp";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly WeddingPlannerResearchOptions _options;

    public RemoteHttpWeddingPlannerResearchProvider(
        HttpClient http,
        IOptions<WeddingPlannerResearchOptions> options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string WorkerKey =>
        string.IsNullOrWhiteSpace(_options.WorkerKey)
            ? WeddingPlannerResearchWorkers.RemoteHttpV1
            : _options.WorkerKey.Trim();

    public async Task<WeddingPlannerResearchAcquisitionResult> AcquireSourcesAsync(
        WeddingPlannerResearchAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateConfiguration();

        var payload = new ResearchAcquireRequestDto
        {
            Topic = request.Topic,
            Objective = request.Objective,
            Questions = request.Questions.ToList(),
            Geography = request.Geography,
            Language = request.Language,
            AllowedDomains = request.AllowedDomains.ToList(),
            ApprovedBrandDnaVersionId = request.ApprovedBrandDnaVersionId,
            ApprovedColorProfileVersionId = request.ApprovedColorProfileVersionId,
            BrandDnaSummaryFraming = request.BrandDnaSummaryFraming
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = Math.Clamp(_options.TimeoutSeconds <= 0 ? 30 : _options.TimeoutSeconds, 1, 600);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "research/acquire")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey!.Trim());
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeddingPlannerResearchProviderException(
                "Wedding Planner research provider request timed out.",
                "RESEARCH_PROVIDER_TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new WeddingPlannerResearchProviderException(
                "Wedding Planner research provider transport failed.",
                "RESEARCH_PROVIDER_TRANSPORT",
                ex);
        }

        using (response)
        {
            var rawBody = await ReadBoundedBodyAsync(response, timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new WeddingPlannerResearchProviderException(
                    $"Wedding Planner research provider returned HTTP {(int)response.StatusCode}.",
                    "RESEARCH_PROVIDER_HTTP_" + (int)response.StatusCode);
            }

            ResearchAcquireResponseDto? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ResearchAcquireResponseDto>(rawBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new WeddingPlannerResearchProviderException(
                    "Wedding Planner research provider returned an invalid JSON payload.",
                    "RESEARCH_PROVIDER_INVALID_PAYLOAD",
                    ex);
            }

            if (parsed is null)
            {
                throw new WeddingPlannerResearchProviderException(
                    "Wedding Planner research provider returned an empty payload.",
                    "RESEARCH_PROVIDER_EMPTY_PAYLOAD");
            }

            // Strict schema: only known top-level fields; reject unknown via re-parse as JsonDocument.
            using (var doc = JsonDocument.Parse(rawBody))
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (property.Name is not ("sourceCatalog" or "providerRequestId" or "estimatedCostUsd" or "modelId"))
                    {
                        throw new WeddingPlannerResearchProviderException(
                            $"Wedding Planner research provider returned unknown field '{SanitizeForError(property.Name)}'.",
                            "RESEARCH_PROVIDER_UNKNOWN_FIELD");
                    }
                }
            }

            if (parsed.SourceCatalog is null)
            {
                throw new WeddingPlannerResearchProviderException(
                    "Wedding Planner research provider omitted sourceCatalog.",
                    "RESEARCH_PROVIDER_MISSING_CATALOG");
            }

            string catalogJson;
            try
            {
                catalogJson = JsonSerializer.Serialize(parsed.SourceCatalog, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new WeddingPlannerResearchProviderException(
                    "Wedding Planner research provider sourceCatalog could not be serialized.",
                    "RESEARCH_PROVIDER_INVALID_CATALOG",
                    ex);
            }

            var providerRequestId = string.IsNullOrWhiteSpace(parsed.ProviderRequestId)
                ? Guid.NewGuid().ToString("N")
                : parsed.ProviderRequestId!.Trim();

            var cost = parsed.EstimatedCostUsd ?? _options.RemoteEstimatedCostUsd;

            return new WeddingPlannerResearchAcquisitionResult(
                SourceCatalogJson: catalogJson,
                ProviderKey: ProviderKey,
                AdapterVersion: AdapterVersion,
                ProviderRequestId: Truncate(providerRequestId, 128),
                WorkerKey: WorkerKey,
                EstimatedCostUsd: Math.Round(cost, 6, MidpointRounding.AwayFromZero));
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new WeddingPlannerResearchProviderException(
                "WeddingPlannerResearch:BaseUrl is required for RemoteHttp provider.",
                "RESEARCH_PROVIDER_CONFIG");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new WeddingPlannerResearchProviderException(
                "WeddingPlannerResearch:ApiKey is required for RemoteHttp provider.",
                "RESEARCH_PROVIDER_CONFIG");
        }

        if (!Uri.TryCreate(_options.BaseUrl.Trim(), UriKind.Absolute, out var baseUri)
            || baseUri.Scheme is not ("http" or "https"))
        {
            throw new WeddingPlannerResearchProviderException(
                "WeddingPlannerResearch:BaseUrl must be an absolute http or https URL.",
                "RESEARCH_PROVIDER_CONFIG");
        }
    }

    private static async Task<string> ReadBoundedBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var limited = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > WeddingPlannerCuratorValidation.MaxRemoteResponseBytes)
            {
                throw new WeddingPlannerResearchProviderException(
                    "Wedding Planner research provider response exceeded the maximum allowed size.",
                    "RESEARCH_PROVIDER_PAYLOAD_TOO_LARGE");
            }

            limited.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(limited.ToArray());
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string SanitizeForError(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, 64));
        foreach (var ch in value.Take(64))
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
        }

        return builder.ToString();
    }

    private sealed class ResearchAcquireRequestDto
    {
        [JsonPropertyName("topic")]
        public string Topic { get; set; } = string.Empty;

        [JsonPropertyName("objective")]
        public string Objective { get; set; } = string.Empty;

        [JsonPropertyName("questions")]
        public List<string> Questions { get; set; } = new();

        [JsonPropertyName("geography")]
        public string Geography { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("allowedDomains")]
        public List<string> AllowedDomains { get; set; } = new();

        [JsonPropertyName("approvedBrandDnaVersionId")]
        public Guid ApprovedBrandDnaVersionId { get; set; }

        [JsonPropertyName("approvedColorProfileVersionId")]
        public Guid? ApprovedColorProfileVersionId { get; set; }

        [JsonPropertyName("brandDnaSummaryFraming")]
        public string? BrandDnaSummaryFraming { get; set; }
    }

    private sealed class ResearchAcquireResponseDto
    {
        [JsonPropertyName("sourceCatalog")]
        public JsonElement? SourceCatalog { get; set; }

        [JsonPropertyName("providerRequestId")]
        public string? ProviderRequestId { get; set; }

        [JsonPropertyName("estimatedCostUsd")]
        public decimal? EstimatedCostUsd { get; set; }

        [JsonPropertyName("modelId")]
        public string? ModelId { get; set; }
    }
}

/// <summary>
/// Bounded, secret-safe research provider failure. Messages must never include API keys.
/// </summary>
public sealed class WeddingPlannerResearchProviderException : Exception
{
    public string ErrorCode { get; }

    public WeddingPlannerResearchProviderException(string message, string errorCode, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
