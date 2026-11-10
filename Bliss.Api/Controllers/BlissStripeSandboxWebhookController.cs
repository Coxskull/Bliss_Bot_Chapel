using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Api.Controllers;

/// <summary>
/// Isolated sandbox ingress. Intentionally refuses acknowledgement until a durable
/// Bliss-specific event journal and fulfillment gate are wired and tested.
/// Never use the legacy Alpha order fulfillment handler here.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks/bliss/stripe")]
public sealed class BlissStripeSandboxWebhookController(IConfiguration configuration, ILogger<BlissStripeSandboxWebhookController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // Fail closed in production and unless explicitly enabled for isolated staging.
        if (!configuration.GetValue<bool>("BlissStripeWebhook:SandboxEnabled")
            || !configuration.GetValue<bool>("Runtime:RestrictedPreview"))
            return NotFound();

        var secret = configuration["BlissStripeWebhook:SigningSecret"];
        if (string.IsNullOrWhiteSpace(secret) || !secret.StartsWith("whsec_", StringComparison.Ordinal))
            return StatusCode(503);

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrEmpty(payload) || !Verify(payload, Request.Headers["Stripe-Signature"].ToString(), secret))
            return BadRequest();

        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (!root.TryGetProperty("livemode", out var live) || live.ValueKind != JsonValueKind.False)
                return BadRequest();
            if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
                return BadRequest();

            // No raw payloads, customer data, secrets, or payment IDs in logs.
            logger.LogInformation("Validated Bliss sandbox Stripe event type {EventType}; durable receipt unavailable.", type.GetString());
            // Stripe must retry; never report success without durable processing.
            Response.Headers.RetryAfter = "60";
            return StatusCode(503);
        }
        catch (JsonException)
        {
            return BadRequest();
        }
    }

    private static bool Verify(string payload, string signature, string secret)
    {
        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in signature.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.StartsWith("t=", StringComparison.Ordinal)) timestamp = part[2..];
            if (part.StartsWith("v1=", StringComparison.Ordinal)) signatures.Add(part[3..]);
        }
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || signatures.Count == 0) return false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (seconds > now + 300 || seconds < now - 300) return false;
        var signedPayload = Encoding.UTF8.GetBytes(timestamp + "." + payload);
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signedPayload);
        foreach (var candidate in signatures)
        {
            if (candidate.Length != 64) continue;
            try
            {
                var bytes = Convert.FromHexString(candidate);
                if (CryptographicOperations.FixedTimeEquals(digest, bytes)) return true;
            }
            catch (FormatException) { }
        }
        return false;
    }
}
