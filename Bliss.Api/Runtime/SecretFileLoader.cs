using Microsoft.Extensions.Configuration;

namespace Bliss.Api.Runtime;

/// <summary>
/// Reads three deployment secrets from mounted files. The file path is configuration.
/// The file contents are not written to appsettings.
/// </summary>
public static class SecretFileLoader
{
    public const int MaximumSecretBytes = 8192;

    public static readonly string[] Keys =
    [
        "ConnectionStrings:DefaultConnection",
        "Authentication:ClientSecret",
        "Runtime:DataProtectionCertificatePassword",
        "Runtime:CreativeGeneration:ApiToken"
    ];

    public static void Apply(ConfigurationManager configuration, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        foreach (var key in Keys)
        {
            var loaded = Read(key, name => configuration[name], contentRoot);
            if (loaded is not null)
            {
                configuration[key] = loaded;
            }
        }
    }

    public static string? Read(string key, Func<string, string?> get, string contentRoot)
    {
        var path = get(key + "_FILE")?.Trim() ?? string.Empty;
        if (path.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(get(key)))
        {
            throw new InvalidOperationException(
                key + " must be supplied either as an environment value or as a secret file, not both.");
        }

        if (!Path.IsPathRooted(path))
        {
            throw new InvalidOperationException("The secret file path for " + key + " must be absolute.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException("The secret file for " + key + " is not readable.");
        }

        var resolved = Resolve(path);
        var root = Path.GetFullPath(contentRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (resolved.Equals(Path.GetFullPath(contentRoot), StringComparison.Ordinal)
            || resolved.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Secret files must be mounted outside the application directory.");
        }

        var info = new FileInfo(resolved);
        if (info.Length > MaximumSecretBytes)
        {
            throw new InvalidOperationException("The secret file for " + key + " is larger than a single secret.");
        }

        var text = File.ReadAllText(resolved).TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("The secret file for " + key + " is empty.");
        }

        return text;
    }

    private static string Resolve(string path)
    {
        var full = Path.GetFullPath(path);
        var link = File.ResolveLinkTarget(full, returnFinalTarget: true);
        return link is null ? full : Path.GetFullPath(link.FullName);
    }
}
