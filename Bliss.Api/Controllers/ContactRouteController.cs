using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/routes")]
public sealed class ContactRouteController(ContactRouteService routes) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await routes.ReadAsync(cancellationToken);
        return Ok(Body(board));
    }

    [HttpPost("transmission")]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Transmission([FromBody] TransmissionRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var decision = await routes.AuthorizeAsync(request?.Authorization, request?.IdempotencyKey, cancellationToken);
            if (!decision.Accepted)
            {
                return BadRequest(new
                {
                    error = decision.Notice,
                    decision.Transmission,
                    decision.Delivery,
                    decision.GreenMeansSend
                });
            }

            return Ok(new
            {
                decision.Duplicate,
                decision.Written,
                decision.IdempotencyKey,
                decision.Adapter,
                decision.Transmission,
                decision.Notice,
                decision.Delivery,
                decision.GreenMeansSend
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = DeliveryPolicy.Transmission, greenMeansSend = false });
        }
    }

    private static object Body(RouteBoard board)
    {
        var reading = board.Reading;
        return new
        {
            reading.Notice,
            reading.Delivery,
            reading.GreenMeansSend,
            reading.CensusClaimed,
            reading.Stored,
            reading.Suppressed,
            reading.Stale,
            reading.MissingEvidence,
            reading.NoRoad,
            reading.Preview,
            reading.Withheld,
            routes = reading.Routes.Select(item => new
            {
                item.BusinessName,
                item.Route,
                item.Adapter,
                item.Transmission,
                item.Notice
            }),
            audits = board.Audits.Select(item => new
            {
                item.IdempotencyKey,
                authorizationLine = ContactRouteAudit.DisplayAuthorization(item.Authorization),
                item.Adapter,
                item.Transmission,
                item.Notice
            })
        };
    }
}

public sealed record TransmissionRequest(string? Authorization, string? IdempotencyKey);
