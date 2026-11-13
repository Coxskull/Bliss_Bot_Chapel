using System.Text;
using Bliss.Api.Financials;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bliss.Api.Controllers;

/// <summary>
/// Bliss-only, staging-only Stripe webhook ingress. Durable receipt acceptance is
/// separate from financial reconciliation; no webhook directly fulfills an order.
/// The legacy Alpha backend is not used.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks/bliss/stripe")]
public sealed class BlissStripeSandboxWebhookController(
    IConfiguration config,
    IWebHostEnvironment environment,
    BlissRuntimeOptions runtime,
    BlissStripeSandboxReceiptStore store,
    ILogger<BlissStripeSandboxWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // Restricted Preview blocks API writes; production and non-staging are not authorized.
        if (!environment.IsStaging() || runtime.RestrictedPreview
            || !config.GetValue<bool>("BlissStripeWebhook:SandboxEnabled"))
            return NotFound();

        var secret = config["BlissStripeWebhook:SigningSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);
        BlissStripeVerifiedEvent verified;
        try
        {
            verified = BlissStripeSandboxWebhookVerifier.Verify(
                raw, Request.Headers["Stripe-Signature"].ToString(),
                secret, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException)
        {
            return BadRequest("Invalid sandbox webhook.");
        }

        try
        {
            var inserted = await store.Record(verified, ct);
            // Stripe 2xx means the signed event is durably RECORDED, not fulfilled.
            // Reconciliation must separately validate account, order authority, amounts,
            // currencies, and payment state before any paid-only business effect.
            logger.LogInformation("Bliss sandbox webhook receipt saved. Type={EventType}, New={New}",
                verified.EventType, inserted);
            return inserted
                ? Accepted(new { recorded = true, fulfillmentAuthorized = false })
                : Ok(new { duplicate = true, fulfillmentAuthorized = false });
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            logger.LogWarning("Bliss staging webhook journal is unavailable; Stripe should retry.");
            Response.Headers.RetryAfter = "60";
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
