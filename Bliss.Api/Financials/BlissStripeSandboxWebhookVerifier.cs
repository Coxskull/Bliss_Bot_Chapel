using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bliss.Api.Financials;

public sealed record BlissStripeVerifiedEvent(
    string EventId, string EventType, string ObjectId, string PayloadSha256, string Disposition)
{
    // A signed webhook alone is NOT authority to release creative inventory, start a campaign,
    // issue commission, or mark an advertiser order paid.
    public bool AuthorizesFulfillment => false;
}

public static class BlissStripeSandboxWebhookVerifier
{
    public static BlissStripeVerifiedEvent Verify(
        string payload, string signature, string signingSecret, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(payload) || Encoding.UTF8.GetByteCount(payload) > 262144
            || string.IsNullOrWhiteSpace(signature) || signature.Length > 4096
            || string.IsNullOrWhiteSpace(signingSecret) || !signingSecret.StartsWith("whsec_", StringComparison.Ordinal))
            throw new ArgumentException("Bounded signed sandbox event required.");

        var parts = signature.Split(',').Select(s => s.Trim().Split('=', 2)).ToArray();
        if (parts.Any(p => p.Length != 2 || string.IsNullOrWhiteSpace(p[1])
            || p[0] is not ("t" or "v1")))
            throw new ArgumentException("Malformed Stripe signature header.");

        var timestamps = parts.Where(p => p[0] == "t").Select(p => p[1]).ToArray();
        var signatures = parts.Where(p => p[0] == "v1").Select(p => p[1]).ToArray();
        if (timestamps.Length != 1 || signatures.Length is < 1 or > 8
            || !long.TryParse(timestamps[0], NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp)
            || Math.Abs((decimal)now.ToUnixTimeSeconds() - timestamp) > 300)
            throw new ArgumentException("Invalid Stripe signature timestamp.");

        var signedBytes = Encoding.UTF8.GetBytes(timestamps[0] + "." + payload);
        var actual = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), signedBytes);
        var valid = false;
        foreach (var value in signatures)
        {
            try
            {
                var candidate = Convert.FromHexString(value);
                valid |= candidate.Length == actual.Length
                         && CryptographicOperations.FixedTimeEquals(candidate, actual);
            }
            catch (FormatException) { }
        }
        if (!valid) throw new ArgumentException("Stripe signature invalid.");

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.GetProperty("livemode").ValueKind != JsonValueKind.False
                || root.TryGetProperty("account", out _))
                throw new ArgumentException("Only non-Connect sandbox Stripe events accepted.");

            var eventId = root.GetProperty("id").GetString();
            var type = root.GetProperty("type").GetString();
            var obj = root.GetProperty("data").GetProperty("object");
            var objectId = obj.GetProperty("id").GetString();
            if (!ValidId(eventId, "evt_", 120) || string.IsNullOrWhiteSpace(type)
                || type.Length > 100 || type.Any(char.IsControl)
                || !ValidId(objectId, null, 160))
                throw new ArgumentException("Invalid Stripe event identity.");

            // Signed provider status is evidence, NOT independent authority to fulfill.
            // Even a successful payment requires a separately verified merchant/quote
            // binding, amount, currency, and an atomic idempotent reconciliation.
            var disposition = type switch
            {
                "checkout.session.expired" or "checkout.session.async_payment_failed"
                    or "payment_intent.payment_failed" or "payment_intent.canceled"
                    or "charge.failed" => "NO_FULFILLMENT",
                "checkout.session.completed" or "checkout.session.async_payment_succeeded"
                    when PropertyIs(obj, "payment_status", "paid") => "RECONCILIATION_REQUIRED",
                "payment_intent.succeeded" when PropertyIs(obj, "status", "succeeded")
                    && obj.TryGetProperty("amount_received", out var amount)
                    && amount.ValueKind == JsonValueKind.Number && amount.GetInt64() > 0
                    => "RECONCILIATION_REQUIRED",
                _ => "REVIEW_ONLY"
            };
            return new BlissStripeVerifiedEvent(eventId!, type!, objectId!,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), disposition);
        }
        catch (JsonException ex) { throw new ArgumentException("Invalid Stripe event JSON.", ex); }
        catch (KeyNotFoundException ex) { throw new ArgumentException("Missing Stripe event fields.", ex); }
        catch (InvalidOperationException ex) { throw new ArgumentException("Invalid Stripe event field types.", ex); }
    }

    private static bool PropertyIs(JsonElement obj, string field, string expected)
        => obj.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool ValidId(string? id, string? prefix, int maximum)
        => !string.IsNullOrWhiteSpace(id) && id.Length <= maximum && !id.Any(char.IsControl)
            && (prefix is null || id.StartsWith(prefix, StringComparison.Ordinal));
}
