using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bliss.Api.Runtime;

public static class ExportIntegrity
{
    public const string HeaderName = "X-Content-SHA256";
    public const int VerifyLimitBytes = 512_000;
    private static readonly Regex Hex64 = new("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static readonly IReadOnlyDictionary<string, string[]> CaseDomainFields =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["match-case"] =
            [
                "match", "scoreComponents", "eligibilityChecks", "evaluations",
                "formations", "reviews", "placements"
            ],
            ["creator-case"] =
            [
                "creator", "platforms", "content", "matches", "ingestions", "provenances"
            ],
            ["campaign-case"] =
            [
                "campaign", "placements", "placementRuns", "matches"
            ]
        };

    public static readonly HashSet<string> Ledgers = new(StringComparer.Ordinal)
    {
        "evaluations", "formations", "reviews", "placements", "ingestions", "provenance"
    };

    public static string Sha256Hex(object domainContent)
    {
        var json = JsonSerializer.Serialize(domainContent, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(document.RootElement, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    public static bool TryVerify(JsonElement root, out ExportVerificationDto result, out string error)
    {
        result = default!;
        if (!TryHashDomain(root, out var packKind, out var computed, out error))
        {
            return false;
        }

        if (!root.TryGetProperty("contentSha256", out var declaredElement)
            || declaredElement.ValueKind != JsonValueKind.String)
        {
            error = "Pack is missing contentSha256.";
            return false;
        }

        var declared = (declaredElement.GetString() ?? string.Empty).Trim().ToLowerInvariant();
        if (!Hex64.IsMatch(declared))
        {
            error = "contentSha256 must be a 64-character hex digest.";
            return false;
        }

        result = new ExportVerificationDto(packKind, declared, computed, declared == computed);
        return true;
    }

    public static string ReceiptDetail(ExportVerificationDto result)
    {
        var outcome = result.Matched ? "matched" : "mismatch";
        var prefix = result.ComputedSha256.Length >= 12
            ? result.ComputedSha256[..12]
            : result.ComputedSha256;
        var text = $"{result.PackKind} {outcome} {prefix}";
        return text.Length <= 128 ? text : text[..128];
    }

    public static bool TryHashDomain(JsonElement root, out string packKind, out string digest, out string error)
    {
        packKind = string.Empty;
        digest = string.Empty;
        error = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
        {
            error = "Pack must be a JSON object.";
            return false;
        }

        if (root.TryGetProperty("kind", out var kindElement) && kindElement.ValueKind == JsonValueKind.String)
        {
            var kind = kindElement.GetString() ?? string.Empty;
            if (!CaseDomainFields.TryGetValue(kind, out var fields))
            {
                error = "Unsupported pack kind.";
                return false;
            }

            var domain = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (!root.TryGetProperty(field, out var value))
                {
                    error = $"Pack is missing {field}.";
                    return false;
                }

                domain[field] = value;
            }

            packKind = kind;
            digest = Sha256Hex(domain);
            return true;
        }

        if (root.TryGetProperty("ledger", out var ledgerElement) && ledgerElement.ValueKind == JsonValueKind.String)
        {
            var ledger = (ledgerElement.GetString() ?? string.Empty).Trim().ToLowerInvariant();
            if (!Ledgers.Contains(ledger))
            {
                error = "Unsupported ledger.";
                return false;
            }

            if (!root.TryGetProperty("records", out var records))
            {
                error = "Pack is missing records.";
                return false;
            }

            packKind = ledger;
            digest = Sha256Hex(records);
            return true;
        }

        error = "Pack is missing kind or ledger.";
        return false;
    }
}

public sealed record ExportVerificationDto(
    string PackKind,
    string DeclaredSha256,
    string ComputedSha256,
    bool Matched,
    string? RequestId = null,
    DateTime? VerifiedAt = null);
