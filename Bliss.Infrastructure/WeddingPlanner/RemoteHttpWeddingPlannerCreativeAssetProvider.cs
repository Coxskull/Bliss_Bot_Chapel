using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bliss.Domain.WeddingPlanner;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Remote HTTP creative asset adapter. Posts to the configured endpoint only and accepts
/// raw image/png response bodies plus bounded safe receipt headers. Rejects URLs, base64,
/// HTML, and non-PNG content types. No vendor SDK.
/// </summary>
public sealed class RemoteHttpWeddingPlannerCreativeAssetProvider : IWeddingPlannerCreativeAssetProvider
{
    public const string ProviderKey = "remote-http-creative-asset";
    public const string AdapterVersion = "wp-creative-asset-remote-http-adapter.v1";
    public const int MaxResponseBytes = WeddingPlannerPngValidator.MaxAssetBytes;
    public const int MaxReceiptHeaderValueLength = 256;
    public const int MaxReceiptHeaders = 16;

    private static readonly HashSet<string> SafeReceiptHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "x-request-id",
        "x-provider-request-id",
        "x-provider-latency-ms",
        "x-estimated-cost-usd",
        "x-cost-usd"
    };

    private readonly HttpClient _http;
    private readonly WeddingPlannerCreativeAssetOptions _options;

    public RemoteHttpWeddingPlannerCreativeAssetProvider(
        HttpClient http,
        IOptions<WeddingPlannerCreativeAssetOptions> options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string WorkerKey =>
        string.IsNullOrWhiteSpace(_options.WorkerKey)
            ? WeddingPlannerCreativeAssetWorkers.RemoteHttpV1
            : _options.WorkerKey.Trim();

    public async Task<WeddingPlannerCreativeAssetGenerationResult> GeneratePngAsync(
        WeddingPlannerCreativeAssetGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateConfiguration();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = Math.Clamp(_options.TimeoutSeconds <= 0 ? 60 : _options.TimeoutSeconds, 1, 600);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "creative-assets/generate");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey!.Trim());
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));

        var payload = JsonSerializer.Serialize(new
        {
            variantId = request.VariantId,
            format = request.Format,
            width = request.Width,
            height = request.Height,
            renderSpecJson = request.RenderSpecJson,
            creativeProductionJobId = request.CreativeProductionJobId,
            approvedConceptPackageVersionId = request.ApprovedConceptPackageVersionId,
            selectedConceptId = request.SelectedConceptId
        });
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeddingPlannerCreativeAssetProviderException(
                "Wedding Planner creative asset provider request timed out.",
                "CREATIVE_ASSET_PROVIDER_TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new WeddingPlannerCreativeAssetProviderException(
                "Wedding Planner creative asset provider transport failed.",
                "CREATIVE_ASSET_PROVIDER_TRANSPORT",
                ex);
        }

        using (response)
        {
            var body = await ReadBoundedBodyAsync(response, timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new WeddingPlannerCreativeAssetProviderException(
                    $"Wedding Planner creative asset provider returned HTTP {(int)response.StatusCode}.",
                    "CREATIVE_ASSET_PROVIDER_HTTP_" + (int)response.StatusCode);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (!string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
            {
                throw new WeddingPlannerCreativeAssetProviderException(
                    "Wedding Planner creative asset provider must return Content-Type image/png.",
                    "CREATIVE_ASSET_PROVIDER_CONTENT_TYPE");
            }

            if (LooksLikeUrlOrBase64OrHtml(body))
            {
                throw new WeddingPlannerCreativeAssetProviderException(
                    "Wedding Planner creative asset provider returned URL/base64/HTML instead of raw PNG bytes.",
                    "CREATIVE_ASSET_PROVIDER_INVALID_PAYLOAD");
            }

            WeddingPlannerValidatedPng validated;
            try
            {
                validated = WeddingPlannerPngValidator.ValidateExactCanvas(body, request.Width, request.Height);
            }
            catch (InvalidOperationException ex)
            {
                throw new WeddingPlannerCreativeAssetProviderException(
                    "Wedding Planner creative asset provider returned an invalid PNG.",
                    "CREATIVE_ASSET_PROVIDER_INVALID_PNG",
                    ex);
            }

            var receipt = CaptureSafeReceiptHeaders(response);
            var cost = TryReadCost(response) ?? _options.RemoteEstimatedCostUsd;
            var providerRequestId = response.Headers.TryGetValues("x-request-id", out var values)
                ? Truncate(values.FirstOrDefault() ?? string.Empty, MaxReceiptHeaderValueLength)
                : "remote-" + validated.Sha256[..16];

            return new WeddingPlannerCreativeAssetGenerationResult(
                validated.Bytes,
                ProviderKey,
                AdapterVersion,
                string.IsNullOrWhiteSpace(providerRequestId) ? "remote-" + validated.Sha256[..16] : providerRequestId,
                WorkerKey,
                receipt,
                cost);
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new WeddingPlannerCreativeAssetProviderException(
                "WeddingPlannerCreativeAsset:BaseUrl is required for RemoteHttp.",
                "CREATIVE_ASSET_PROVIDER_MISCONFIGURED");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new WeddingPlannerCreativeAssetProviderException(
                "WeddingPlannerCreativeAsset:ApiKey is required for RemoteHttp.",
                "CREATIVE_ASSET_PROVIDER_MISCONFIGURED");
        }

        if (_http.BaseAddress is null)
        {
            throw new WeddingPlannerCreativeAssetProviderException(
                "Remote creative asset HttpClient BaseAddress is not configured.",
                "CREATIVE_ASSET_PROVIDER_MISCONFIGURED");
        }
    }

    private static async Task<byte[]> ReadBoundedBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            total += read;
            if (total > MaxResponseBytes)
            {
                throw new WeddingPlannerCreativeAssetProviderException(
                    $"Wedding Planner creative asset provider response exceeded {MaxResponseBytes} bytes.",
                    "CREATIVE_ASSET_PROVIDER_OVERSIZE");
            }

            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static string? CaptureSafeReceiptHeaders(HttpResponseMessage response)
    {
        var captured = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            if (!SafeReceiptHeaderNames.Contains(header.Key))
            {
                continue;
            }

            if (captured.Count >= MaxReceiptHeaders)
            {
                break;
            }

            var value = Truncate(string.Join(",", header.Value), MaxReceiptHeaderValueLength);
            if (!string.IsNullOrWhiteSpace(value))
            {
                captured[header.Key] = value;
            }
        }

        return captured.Count == 0 ? null : JsonSerializer.Serialize(captured);
    }

    private static decimal? TryReadCost(HttpResponseMessage response)
    {
        foreach (var name in new[] { "x-estimated-cost-usd", "x-cost-usd" })
        {
            if (response.Headers.TryGetValues(name, out var values)
                && decimal.TryParse(values.FirstOrDefault(), out var cost)
                && cost >= 0)
            {
                return cost;
            }
        }

        return null;
    }

    private static bool LooksLikeUrlOrBase64OrHtml(byte[] body)
    {
        if (body.Length >= 8
            && body[0] == 0x89
            && body[1] == 0x50
            && body[2] == 0x4E
            && body[3] == 0x47)
        {
            return false;
        }

        var sampleLength = Math.Min(body.Length, 64);
        var sample = Encoding.UTF8.GetString(body, 0, sampleLength).TrimStart();
        return sample.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("{", StringComparison.Ordinal);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

/// <summary>
/// Raised when the creative asset provider fails. Controllers map this to HTTP 502 after
/// durable job failure persistence when appropriate.
/// </summary>
public sealed class WeddingPlannerCreativeAssetProviderException : InvalidOperationException
{
    public string ErrorCode { get; }

    public WeddingPlannerCreativeAssetProviderException(
        string message,
        string errorCode,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
