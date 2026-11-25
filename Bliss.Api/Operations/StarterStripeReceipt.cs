using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bliss.Api.Operations;

// Authenticated notification evidence only. Never authorizes fulfillment or declares revenue.
public sealed record StarterStripeReceipt(string EventId, string SessionId, string PaymentIntentId,
    string? QuoteReference, long AmountCents, string Currency, string EventType)
{
    public const string PaymentLinkId = "plink_1UfWXNH6XjUXn6bha65jeI1t";
    public const int MaxBodyBytes = 256 * 1024;
    public static bool Verify(ReadOnlySpan<byte> body, string signature, string secret, DateTimeOffset now)
    {
        if (body.Length == 0 || body.Length > MaxBodyBytes || signature.Length > 4096 ||
            !secret.StartsWith("whsec_", StringComparison.Ordinal) || secret.Length < 16) return false;
        var fields = signature.Split(',');
        var timestamps = fields.Where(x => x.StartsWith("t=", StringComparison.Ordinal)).ToArray();
        if (timestamps.Length != 1 || !long.TryParse(timestamps[0].AsSpan(2), NumberStyles.None,
                CultureInfo.InvariantCulture, out var timestamp)) return false;
        var seconds = now.ToUnixTimeSeconds();
        if (timestamp < seconds - 300 || timestamp > seconds + 300) return false;
        var prefix = Encoding.ASCII.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed.AsSpan(prefix.Length));
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed);
        foreach (var field in fields.Where(x => x.StartsWith("v1=", StringComparison.Ordinal) && x.Length == 67))
        {
            try { if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(field[3..]))) return true; }
            catch (FormatException) { }
        }
        return false;
    }

    public static StarterStripeReceipt? Parse(ReadOnlyMemory<byte> body)
    {
        using var doc = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
        var root = doc.RootElement;
        static string? S(JsonElement x, string key) =>
            x.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        static bool Live(JsonElement x) => x.TryGetProperty("livemode", out var value) && value.ValueKind == JsonValueKind.True;
        static bool Id(string? value, string prefix) => value is not null && value.Length <= 128 &&
            value.StartsWith(prefix, StringComparison.Ordinal) && Regex.IsMatch(value[prefix.Length..], "^[A-Za-z0-9_]+$");
        if (!Live(root) || S(root, "object") != "event") return null;
        var type = S(root, "type");
        if (type is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded")) return null;
        var session = root.GetProperty("data").GetProperty("object");
        if (S(session, "object") != "checkout.session" || !Live(session) ||
            S(session, "payment_link") != PaymentLinkId || S(session, "mode") != "payment" ||
            S(session, "status") != "complete" || S(session, "payment_status") != "paid" ||
            S(session, "currency") != "usd" || !session.TryGetProperty("amount_total", out var amount) ||
            !amount.TryGetInt64(out var cents) || cents != 14900) return null;
        var eventId = S(root, "id"); var sessionId = S(session, "id"); var paymentId = S(session, "payment_intent");
        if (!Id(eventId, "evt_") || !Id(sessionId, "cs_live_") || !Id(paymentId, "pi_"))
            throw new FormatException("Missing Stripe receipt identifier.");
        var reference = S(session, "client_reference_id");
        if (reference is null || !Regex.IsMatch(reference, "^[A-Za-z0-9_-]{1,64}$")) reference = null;
        return new(eventId!, sessionId!, paymentId!, reference, cents, "usd", type!);
    }
}
