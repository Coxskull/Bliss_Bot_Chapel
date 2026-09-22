using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/runtime")]
public sealed class RuntimeStatusController(
    IWebHostEnvironment environment,
    BlissAuthenticationOptions authentication,
    BlissRuntimeOptions runtime,
    OperationalEventStore events,
    HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<RuntimeStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            cancellationToken);

        return Ok(new RuntimeStatusDto(
            environment.EnvironmentName,
            authentication.Enabled,
            "Healthy",
            report.Status.ToString(),
            report.Entries.TryGetValue("database", out var database)
                ? database.Description
                : "Database check not registered.",
            !string.IsNullOrWhiteSpace(runtime.DataProtectionKeysPath),
            runtime.WriteRateLimitPermitLimit,
            runtime.AuthenticationRateLimitPermitLimit,
            runtime.RateLimitWindowSeconds,
            events.LastRequestId,
            events.Recent().Select(entry => new RuntimeEventDto(
                entry.OccurredAt,
                entry.Kind,
                entry.Method,
                entry.Path,
                entry.StatusCode,
                entry.RequestId,
                entry.Detail)).ToList(),
            events.LastVerification));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("throttle-check")]
    public IActionResult VerifyWriteThrottle() => NoContent();
}

public sealed record RuntimeStatusDto(
    string Environment,
    bool AuthenticationEnabled,
    string ProcessStatus,
    string DatabaseStatus,
    string? DatabaseDescription,
    bool PersistentKeysConfigured,
    int WriteRateLimitPermitLimit,
    int AuthenticationRateLimitPermitLimit,
    int RateLimitWindowSeconds,
    string? LastRequestId,
    IReadOnlyList<RuntimeEventDto> RecentEvents,
    ExportVerificationDto? LastVerification = null);

public sealed record RuntimeEventDto(
    DateTime OccurredAt,
    string Kind,
    string Method,
    string Path,
    int StatusCode,
    string RequestId,
    string? Detail = null);
