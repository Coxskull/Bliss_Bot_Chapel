using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bliss.Api.Runtime;

/// <summary>
/// Reports whether the media tools and font required by video composition are present.
/// Included in readiness, not liveness, so deployments can distinguish a running API
/// from an instance that cannot produce media.
/// </summary>
public sealed class MediaRuntimeHealthCheck : IHealthCheck
{
    private const string RequiredFont =
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var missing = new List<string>();
        if (!IsExecutableAvailable("ffmpeg"))
        {
            missing.Add("ffmpeg");
        }

        if (!IsExecutableAvailable("ffprobe"))
        {
            missing.Add("ffprobe");
        }

        if (!File.Exists(RequiredFont))
        {
            missing.Add("DejaVuSans-Bold.ttf");
        }

        var result = missing.Count == 0
            ? HealthCheckResult.Healthy("Media runtime dependencies are available.")
            : HealthCheckResult.Unhealthy(
                "Media runtime dependencies missing: " + string.Join(", ", missing));

        return Task.FromResult(result);
    }

    private static bool IsExecutableAvailable(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, executable)))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // Ignore malformed PATH entries; continue checking the rest.
            }
            catch (NotSupportedException)
            {
                // Ignore malformed PATH entries; continue checking the rest.
            }
        }

        return false;
    }
}
