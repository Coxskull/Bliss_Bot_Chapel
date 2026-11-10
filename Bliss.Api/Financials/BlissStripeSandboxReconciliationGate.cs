using System.Text.Json;
using Bliss.Infrastructure.Financials;

namespace Bliss.Api.Financials;

/// <summary>
/// Read-only commercial evidence preflight for the existing immutable Bliss quote
/// and provider binding. Never posts financial journals, marks payments settled,
/// publishes campaigns, or releases Global STOP.
/// </summary>
public static class BlissStripeSandboxReconciliationGate
{
    public static BlissStripeReconciliationReading Evaluate(
        BlissStripeVerifiedEvent receipt,
        PreparedPaymentOperation authorizedOperation,
        string frozenFundingReference,
        string boundPaymentIntentId,
        string providerPaymentIntentJson)
    {
        if (receipt is null || authorizedOperation is null
            || string.IsNullOrWhiteSpace(frozenFundingReference)
            || string.IsNullOrWhiteSpace(boundPaymentIntentId)
            || string.IsNullOrWhiteSpace(providerPaymentIntentJson))
            return Refuse("MISSING_AUTHORITY");

        if (receipt.EventType != "payment_intent.succeeded"
            || receipt.Disposition != "RECONCILIATION_REQUIRED"
            || receipt.ObjectId != boundPaymentIntentId
            || !boundPaymentIntentId.StartsWith("pi_", StringComparison.Ordinal))
            return Refuse("EVENT_NOT_AUTHORITATIVE");

        if (authorizedOperation.Id == Guid.Empty
            || authorizedOperation.QuoteId == Guid.Empty
            || authorizedOperation.Scope.MerchantId == Guid.Empty
            || authorizedOperation.Scope.CampaignId == Guid.Empty
            || authorizedOperation.RecipientAccountId == Guid.Empty
            || !Enum.IsDefined(authorizedOperation.Scope.Module)
            || authorizedOperation.AmountMinor <= 0
            || authorizedOperation.Currency != "USD"
            || frozenFundingReference.Length > 120)
            return Refuse("INVALID_QUOTE_SCOPE");

        try
        {
            using var doc = JsonDocument.Parse(providerPaymentIntentJson);
            var pi = doc.RootElement;
            if (pi.ValueKind != JsonValueKind.Object
                || GetString(pi, "object") != "payment_intent"
                || pi.GetProperty("livemode").ValueKind != JsonValueKind.False
                || pi.TryGetProperty("account", out _)
                || GetString(pi, "id") != boundPaymentIntentId
                || GetString(pi, "status") != "succeeded"
                || GetAmount(pi, "amount") != authorizedOperation.AmountMinor
                || GetAmount(pi, "amount_received") != authorizedOperation.AmountMinor
                || GetString(pi, "currency") != authorizedOperation.Currency.ToLowerInvariant())
                return Refuse("PROVIDER_PAYMENT_MISMATCH");

            var metadata = pi.GetProperty("metadata");
            if (metadata.ValueKind != JsonValueKind.Object
                || GetString(metadata, "merchant_id") != authorizedOperation.Scope.MerchantId.ToString()
                || GetString(metadata, "campaign_id") != authorizedOperation.Scope.CampaignId.ToString()
                || GetString(metadata, "module") != authorizedOperation.Scope.Module.ToString()
                || GetString(metadata, "funding_reference") != frozenFundingReference)
                return Refuse("IMMUTABLE_BINDING_MISMATCH");

            // A matching provider JSON document is only a *candidate* until its
            // origin (authenticated Stripe GET on the approved test account) and
            // its mapping to the frozen quote/intent DB rows are independently
            // verified, followed by an atomic idempotent commercial settlement.
            return new("EVIDENCE_MATCHES_MANUAL_REVIEW_REQUIRED", false);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or OverflowException)
        {
            return Refuse("MALFORMED_PROVIDER_EVIDENCE");
        }
    }

    private static BlissStripeReconciliationReading Refuse(string reason) => new(reason, false);

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private static long? GetAmount(JsonElement root, string name)
        => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out var value) ? value : null;
}

public sealed record BlissStripeReconciliationReading(string Disposition, bool AuthorizesFulfillment);
