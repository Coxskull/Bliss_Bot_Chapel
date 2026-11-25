using System.Security.Claims;
using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

// Read-only operator evidence, not a paid submission or worker switch.
[ApiController]
[Authorize]
[Route("api/internal/creative-readiness")]
public sealed class StaffCreativeReadinessController(
    BlissDbContext db, BlissAuthenticationOptions auth,
    BlissRuntimeOptions runtime, ControlledAiService ai) : ControllerBase
{
    public static bool IsStaff(ClaimsPrincipal user, BlissAuthenticationOptions auth)
    {
        if (!auth.Enabled || !auth.SupabaseEnabled || !auth.StaffCreativeReadinessRequested
            || user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(auth.Authority)
            || string.IsNullOrWhiteSpace(auth.RoleClaimType)) return false;
        var forbidden = new[] { BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
            BlissAuthenticationOptions.AdvertiserSelfServiceMarkerClaimType,
            BlissAuthenticationOptions.CreatorRegistrationPendingMarkerClaimType,
            auth.AdvertiserIdClaimType, auth.CreatorIdClaimType, auth.ReviewerWorkspaceClaimType };
        if (user.Claims.Any(c => forbidden.Contains(c.Type, StringComparer.OrdinalIgnoreCase))) return false;
        var subjects = user.Claims.Where(c => string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (subjects.Length != 1 || subjects[0].Type != "sub"
            || string.IsNullOrWhiteSpace(subjects[0].Value)
            || !auth.ApprovedTestSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)
            || !auth.ApprovedRegistrationOperatorSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)
            || auth.ApprovedAdvertiserSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)
            || auth.ApprovedCreatorSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)) return false;
        var roles = user.Claims.Where(c => string.Equals(c.Type, auth.RoleClaimType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase)).ToArray();
        return roles.Length == 1 && roles[0].Type == auth.RoleClaimType
            && roles[0].Issuer == auth.Authority && roles[0].OriginalIssuer == auth.Authority
            && (roles[0].Value == auth.OperatorRole || roles[0].Value == auth.AdminRole);
    }

    [HttpGet]
    public async Task<IActionResult> Read(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsStaff(User, auth)) return StatusCode(403);
        // Read aggregate metadata only. Never materialize prompts, image bytes,
        // provider tokens, tenant identifiers or individual queue records.
        var queued = await db.ControlledAiRuns.CountAsync(x => x.Status == "QUEUED", ct);
        var inFlight = await db.ControlledAiRuns.CountAsync(x => x.Status == "RUNNING" || x.Status == "CANCEL_REQUESTED", ct);
        var anomaly = await db.ControlledAiRuns.AnyAsync(x => x.CostStatus == "OVER_RESERVATION", ct)
            || (runtime.ControlledAi.ImagePilotEnabled && await db.ControlledAiRuns.AnyAsync(x =>
                x.Kind == "IMAGE" && x.Model == "gpt-image-2.5-flare"
                && x.Status != "QUEUED" && x.Status != "RUNNING" && x.CostStatus != "PROVIDER_REPORTED", ct));
        return Ok(new {
            restrictedPreview = runtime.RestrictedPreview,
            workerRegistration = runtime.RestrictedPreview ? "DISABLED_BY_PREVIEW" : "CONFIGURED_NOT_ATTESTED",
            imageProviderConfigured = ai.Ready("IMAGE"),
            queuedCount = queued, inFlightCount = inFlight,
            localBillingAnomaly = anomaly,
            externalBillingReconciled = false,
            providerCall = "NOT_REQUESTED", release = "HELD", delivery = "NOT_SENT",
            dispatchAuthorized = false
        });
    }
}
