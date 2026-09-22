using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Deterministic local research source acquisition. Emits conspicuous SYNTHETIC fixtures only
/// with <c>.invalid</c> hosts. Never claims live facts and never fetches URLs.
/// </summary>
public sealed class LocalDeterministicWeddingPlannerResearchProvider : IWeddingPlannerResearchProvider
{
    public const string ProviderKey = "local-deterministic-research";
    public const string AdapterVersion = "wp-research-local-adapter.v1";

    private readonly WeddingPlannerResearchOptions _options;

    public LocalDeterministicWeddingPlannerResearchProvider(WeddingPlannerResearchOptions? options = null)
    {
        _options = options ?? new WeddingPlannerResearchOptions();
    }

    public string WorkerKey =>
        string.IsNullOrWhiteSpace(_options.WorkerKey)
            ? WeddingPlannerResearchWorkers.LocalDeterministicV1
            : _options.WorkerKey.Trim();

    public int InvokeCount { get; private set; }

    public Task<WeddingPlannerResearchAcquisitionResult> AcquireSourcesAsync(
        WeddingPlannerResearchAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        InvokeCount++;

        var topicSlug = Slug(request.Topic);
        var catalog = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ResearchSourceCatalogV1,
            ["sources"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "src_1",
                    ["title"] = $"SYNTHETIC market landscape brief for {request.Topic}",
                    ["url"] = $"https://research.example.invalid/synthetic/{topicSlug}/market",
                    ["publisher"] = "SYNTHETIC Curator Fixture Publisher",
                    ["retrievedAt"] = "2026-01-01T00:00:00Z",
                    ["synthetic"] = true
                },
                new JsonObject
                {
                    ["id"] = "src_2",
                    ["title"] = $"SYNTHETIC audience context note for {request.Geography}",
                    ["url"] = $"https://audience.example.invalid/synthetic/{topicSlug}/audience",
                    ["publisher"] = "SYNTHETIC Audience Lab",
                    ["retrievedAt"] = "2026-01-01T00:00:00Z",
                    ["synthetic"] = true
                },
                new JsonObject
                {
                    ["id"] = "src_3",
                    ["title"] = "SYNTHETIC competitor signal digest",
                    ["url"] = $"https://signals.example.invalid/synthetic/{topicSlug}/competitors",
                    ["publisher"] = "SYNTHETIC Signals Desk",
                    ["retrievedAt"] = "2026-01-01T00:00:00Z",
                    ["synthetic"] = true
                }
            }
        };

        var json = catalog.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        });
        var requestId = "local-research-" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json + request.Topic))).ToLowerInvariant()[..16];

        return Task.FromResult(new WeddingPlannerResearchAcquisitionResult(
            SourceCatalogJson: json,
            ProviderKey: ProviderKey,
            AdapterVersion: AdapterVersion,
            ProviderRequestId: requestId,
            WorkerKey: WorkerKey,
            EstimatedCostUsd: Math.Round(_options.LocalEstimatedCostUsd, 6, MidpointRounding.AwayFromZero)));
    }

    private static string Slug(string value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? "topic" : value.Trim().ToLowerInvariant();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (ch is ' ' or '-' or '_')
            {
                sb.Append('-');
            }
        }

        var slug = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "topic" : slug[..Math.Min(slug.Length, 48)];
    }
}
