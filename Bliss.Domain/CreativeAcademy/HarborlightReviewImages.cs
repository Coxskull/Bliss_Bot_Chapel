using System.Security.Cryptography;
using System.Text.Json;

namespace Bliss.Domain.CreativeAcademy;

/// <summary>
/// A stored Harborlight file the owner can look at. Opening it does not assign a grade.
/// </summary>
public sealed record HarborlightStoredImage(
    string Role,
    string Label,
    string RelativePath,
    string Sha256,
    string Notice);

public static class HarborlightReviewImages
{
    public const string OriginalNotice = "Stored original. Showing it does not assign a grade.";
    public const string AdaptedNotice = "Purpose-built test preview. It was not delivered and it is not a grade.";
    public const string ReferenceNotice = "Retrieved comparison file. It was not sent to the provider. Showing it is not a copy judgment.";

    public static IReadOnlyList<HarborlightStoredImage> ParseEvidence(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var similarity = HarborlightPixelSimilarity.ParseEvidence(json);
        var original = root.GetProperty("original");
        var adaptation = root.GetProperty("adaptation");
        var images = new List<HarborlightStoredImage>
        {
            new(
                "original",
                "Original",
                RequireHarborlightPath(ReadString(original, "path"), "harborlight"),
                RequireHash(ReadString(original, "sha256")),
                OriginalNotice),
            new(
                "adapted",
                "Adapted test preview",
                RequireHarborlightPath(ReadString(adaptation, "path"), "harborlight"),
                RequireHash(ReadString(adaptation, "sha256")),
                AdaptedNotice)
        };

        foreach (var reference in similarity.References)
        {
            var file = RequireFileName(reference.File);
            images.Add(new HarborlightStoredImage(
                reference.ReferenceId,
                reference.ReferenceId,
                RequireHarborlightPath(
                    "assets/alpha-prototypes/creative-academy/inbox/" + file,
                    "inbox"),
                RequireHash(reference.Sha256),
                ReferenceNotice));
        }

        return images;
    }

    public static string OpenVerified(string json, string repositoryRoot, string role)
    {
        var requested = (role ?? string.Empty).Trim();
        var image = ParseEvidence(json)
            .SingleOrDefault(item => string.Equals(item.Role, requested, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("That Harborlight image is not stored. None was substituted.");
        var root = Path.GetFullPath(repositoryRoot);
        var full = Path.GetFullPath(Path.Combine(root, image.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal) || !File.Exists(full))
        {
            throw new InvalidOperationException("The stored Harborlight image is not in the repository. None was substituted.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
        if (!string.Equals(hash, image.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The stored Harborlight image hash does not match. None was substituted.");
        }

        return full;
    }

    public static string MediaType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => throw new InvalidOperationException("The stored Harborlight image is not a JPEG or PNG. None was substituted.")
    };

    private static string RequireHarborlightPath(string path, string folder)
    {
        var normalized = path.Replace('\\', '/').Trim();
        var segments = normalized.Split('/');
        if (Path.IsPathRooted(path)
            || segments.Any(segment => segment is "" or "." or "..")
            || !normalized.StartsWith("assets/alpha-prototypes/creative-academy/" + folder + "/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Harborlight image path is not stored. None was substituted.");
        }

        return normalized;
    }

    private static string RequireFileName(string file)
    {
        if (!string.Equals(file, Path.GetFileName(file), StringComparison.Ordinal)
            || file is "." or ".."
            || file.Contains('/')
            || file.Contains('\\'))
        {
            throw new InvalidOperationException("The comparison file name is not stored. None was substituted.");
        }

        return file;
    }

    private static string RequireHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("The Harborlight image hash is not stored. None was invented.");
        }

        return hash;
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The Harborlight image field " + property + " is not stored. None was invented.");
        }

        return value.GetString() ?? string.Empty;
    }
}
