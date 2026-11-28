using Bliss.Api.Operations;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Data.Common;

namespace Bliss.Api.Controllers;

// This exact route uses provider signature authentication, not browser cookies.
// Outside /api because browser CSRF middleware must not be bypassed for other routes.
[ApiController]
[AllowAnonymous]
[Route("webhooks/stripe/starter")]
public sealed class StarterStripeWebhookController(IConfiguration configuration, BlissDbContext db) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(StarterStripeReceipt.MaxBodyBytes)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var secret = configuration["Runtime:StarterStripeWebhook:SigningSecret"];
        if (!configuration.GetValue<bool>("Runtime:StarterStripeWebhook:Enabled") ||
            string.IsNullOrWhiteSpace(secret) || !secret.StartsWith("whsec_", StringComparison.Ordinal))
            return StatusCode(503);
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signatures) || signatures.Count != 1)
            return BadRequest();
        if (Request.ContentLength > StarterStripeReceipt.MaxBodyBytes) return StatusCode(413);
        using var body = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await Request.Body.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (body.Length + count > StarterStripeReceipt.MaxBodyBytes) return StatusCode(413);
            await body.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        var bytes = body.ToArray();
        if (!StarterStripeReceipt.Verify(bytes, signatures.ToString(), secret, DateTimeOffset.UtcNow))
            return BadRequest();
        StarterStripeReceipt? receipt;
        try { receipt = StarterStripeReceipt.Parse(bytes); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        { return BadRequest(); }
        if (receipt is null) return Ok(new { received = true, recorded = false });
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = """
                    INSERT INTO public.alpha_starter_stripe_receipts
                    (event_id, session_id, payment_intent_id, quote_reference, amount_cents, currency, event_type)
                    VALUES (@event, @session, @intent, @quote, @amount, @currency, @type)
                    ON CONFLICT DO NOTHING
                    """;
                cmd.CommandTimeout = 10;
                void Add(string name, object? value, System.Data.DbType type)
                { var p = cmd.CreateParameter(); p.ParameterName = name; p.DbType = type; p.Value = value ?? DBNull.Value; cmd.Parameters.Add(p); }
                Add("event", receipt.EventId, System.Data.DbType.String);
                Add("session", receipt.SessionId, System.Data.DbType.String);
                Add("intent", receipt.PaymentIntentId, System.Data.DbType.String);
                Add("quote", receipt.QuoteReference, System.Data.DbType.String);
                Add("amount", receipt.AmountCents, System.Data.DbType.Int64);
                Add("currency", receipt.Currency, System.Data.DbType.String);
                Add("type", receipt.EventType, System.Data.DbType.String);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        catch (Exception e) when (e is DbException or InvalidOperationException or TimeoutException)
        { return StatusCode(503); } // No 2xx until receipt is durable; Stripe can retry.
        return Ok(new { received = true, recorded = true, fulfillmentAuthorized = false });
    }
}
