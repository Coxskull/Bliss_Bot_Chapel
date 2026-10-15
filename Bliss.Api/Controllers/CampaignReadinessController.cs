using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

public sealed record CampaignReadinessCheck(string Area, string Status, string Evidence);

[ApiController]
[Authorize(Policy = BlissAuthorization.WritePolicy)]
[Route("api/operations/campaign-readiness")]
public sealed class CampaignReadinessController(BlissDbContext db, IWebHostEnvironment environment, BlissAuthenticationOptions authentication,
    BlissRuntimeOptions runtime, ProductionPostureReport posture, OperationalEventStore events) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Read(CancellationToken ct)
    {
        var reachable = false; var migrations = "UNAVAILABLE"; bool? tlsObserved = null;
        try
        {
            reachable = await db.Database.CanConnectAsync(ct);
            if (reachable && db.Database.IsRelational())
            {
                migrations = (await db.Database.GetPendingMigrationsAsync(ct)).Any() ? "PENDING" : "UP_TO_DATE";
                if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
                    tlsObserved = await db.Database.SqlQueryRaw<bool>("SELECT ssl AS \"Value\" FROM pg_stat_ssl WHERE pid = pg_backend_pid()").SingleAsync(ct);
            }
            else if (reachable) migrations = "NOT_RELATIONAL";
        }
        catch (Exception) when (!ct.IsCancellationRequested) { migrations = "UNAVAILABLE"; }
        var document = posture.Document;
        var checks = new[] {
            new CampaignReadinessCheck("AUTHENTICATION", authentication.Enabled ? "CONFIGURED_NOT_ACCEPTED" : "DEVELOPMENT_ONLY", "Hosted OIDC login, role claims, advertiser isolation and independent approval require a deployed check."),
            new CampaignReadinessCheck("DATABASE", reachable ? "REACHABLE" : "UNAVAILABLE", "An actual database connection was checked; this does not establish backup recovery."),
            new CampaignReadinessCheck("MIGRATIONS", migrations, "Schema migration state was read from the current database."),
            new CampaignReadinessCheck("DATABASE_TLS", tlsObserved == true ? "TLS_OBSERVED" : tlsObserved == false ? "NO_TLS_OBSERVED" : "NOT_OBSERVED", "PostgreSQL reports TLS on the checked connection. Certificate and hostname policy must also be VerifyFull for hosted use."),
            new CampaignReadinessCheck("CERTIFICATE_POLICY", document.DatabaseServerCertificateVerified ? "VERIFY_FULL_CONFIGURED" : "NOT_CONFIGURED", "Configuration policy is separate from a live hosted certificate/hostname rejection check."),
            new CampaignReadinessCheck("SESSION_KEYS", document.KeysEncryptedAtRest ? "CONFIGURED_NOT_ACCEPTED" : "NOT_CONFIGURED", "Persistent encrypted keys need a deployed restart and replica test."),
            new CampaignReadinessCheck("PROXY", document.KnownProxiesConfigured ? "CONFIGURED_NOT_ACCEPTED" : "NOT_CONFIGURED", "Forwarded headers must be validated through the trusted deployment proxy."),
            new CampaignReadinessCheck("BACKUPS", document.BackupDeclared ? "DECLARED_NOT_VERIFIED" : "NOT_DECLARED", "Verify the actual backup schedule, retention, restored lineage, RPO and RTO on the hosted service."),
            new CampaignReadinessCheck("OBSERVABILITY", "LOCAL_IN_MEMORY", "Request correlation and bounded operational events are active. Durable external logs and alert delivery are not established here."),
            new CampaignReadinessCheck("HOSTED_RECOVERY", "PENDING_HOSTED_VALIDATION", "The local fictional recovery drill cannot certify hosted backups or recovery targets.") };
        return Ok(new { environment = environment.EnvironmentName, document.ProductionGatesApplied, databaseReachable = reachable,
            tlsObserved, checks, latestRequestId = events.LastRequestId, liveCallsEnabled = runtime.ControlledAi.Enabled,
            hostedAcceptanceClaimed = false, delivery = "NOT_SENT" });
    }
}
