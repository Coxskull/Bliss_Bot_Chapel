using System.Security.Cryptography;
using System.Text.Json;

namespace Bliss.Api.Runtime;

public static class ExportIntegrity
{
    public const string HeaderName = "X-Content-SHA256";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string Sha256Hex(object domainContent)
    {
        var json = JsonSerializer.Serialize(domainContent, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(document.RootElement, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }
}
