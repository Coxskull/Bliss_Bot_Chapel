using Bliss.Api.Runtime;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bliss.Tests.Api;

public sealed class MediaRuntimeHealthCheckTests
{
    [Fact]
    public async Task Readiness_reports_media_dependencies_available_in_ci_runtime()
    {
        var check = new MediaRuntimeHealthCheck();

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("Media runtime dependencies are available.", result.Description);
    }
}
