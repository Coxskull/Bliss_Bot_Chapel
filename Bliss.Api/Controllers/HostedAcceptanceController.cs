using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/hosted")]
public sealed class HostedAcceptanceController(HostedAcceptanceService readings) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await readings.ReadAsync(cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Store([FromBody] HostedAcceptanceRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await readings.StoreAsync(request?.ReadingKey, request?.ClaimHosted ?? false, cancellationToken);
            var board = await readings.ReadAsync(cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                delivery = "NOT_SENT",
                greenMeansSend = false,
                hostedAcceptanceClaimed = false,
                identityContacted = false,
                backupDrillRun = false
            });
        }
    }

    private static object Body(HostedBoard board, HostedWrite? write) => new
    {
        notice = write?.Reading.Notice ?? (board.History.Count > 0 ? board.History[^1].Notice : HostedAcceptance.Notice),
        delivery = "NOT_SENT",
        greenMeansSend = false,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        environmentName = board.EnvironmentName,
        productionGatesApplied = board.Posture.ProductionGatesApplied,
        hostedDatabaseConfigured = board.Posture.HostedDatabaseConfigured,
        databaseServerCertificateVerified = board.Posture.DatabaseServerCertificateVerified,
        identityProviderHttps = board.Posture.IdentityProviderHttps,
        backupDeclared = board.Posture.BackupDeclared,
        secretMaterialExternal = board.Posture.SecretMaterialExternal,
        roleClaimsDistinct = board.Posture.RoleClaimsDistinct,
        hostedAcceptanceClaimed = false,
        identityContacted = false,
        backupDrillRun = false,
        history = board.History.Select(item => new
        {
            item.ReadingKey,
            item.EnvironmentName,
            item.ProductionGatesApplied,
            item.HostedDatabaseConfigured,
            item.DatabaseServerCertificateVerified,
            item.IdentityProviderHttps,
            item.BackupDeclared,
            item.SecretMaterialExternal,
            item.RoleClaimsDistinct,
            item.HostedAcceptanceClaimed,
            item.IdentityContacted,
            item.BackupDrillRun,
            item.Delivery
        })
    };
}

public sealed record HostedAcceptanceRequest(string? ReadingKey, bool? ClaimHosted);
