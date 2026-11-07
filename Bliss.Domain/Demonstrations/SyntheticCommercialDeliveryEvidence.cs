using System.Security.Cryptography;
using System.Text;
using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Domain.Demonstrations;

/// <summary>
/// Immutable OFFLINE receipt. The real CampaignPlacementRun and
/// CampaignObservation ledgers are the existing production persistence.
/// This synthetic record never authorizes writing either ledger.
/// </summary>
public sealed record SyntheticCommercialDeliveryReceipt(
    string Schema, string Mode, string ReceiptId,
    string ProductId, string ProductVersion, string GeometryVersion,
    string EconomicsQuoteVersion, string CreatorId, string AdvertiserId,
    string CreativeCandidateSha256, int ScheduledDurationSeconds,
    int ScheduledOccurrences, string AttemptedStage, string VerifiedStage,
    int RealObservedOccurrences, string DeliveryStatus,
    string SnapshotSha256, bool ActualDisplayVerified,
    bool ReservationAuthorized, bool PaymentAuthorized,
    bool ExternalUseAuthorized, IReadOnlyList<string> Exceptions);

/// <summary>
/// Deterministic simulation using the existing inventory/Academy evidence
/// results. No channel adapter, media display, invoice or database invocation.
/// A claimed synthetic DISPLAY_VERIFIED does not become real delivery.
/// </summary>
public static class SyntheticCommercialDeliveryEvidence
{
    private static readonly IReadOnlySet<string> Stages = new HashSet<string>(
    [
        "RESERVED", "SCHEDULED", "CREATIVE_APPROVED", "RENDERED",
        "DISPLAY_STARTED", "DISPLAY_VERIFIED", "PARTIALLY_DELIVERED",
        "DELIVERED", "FAILED", "DISPUTED"
    ], StringComparer.Ordinal);

    public static SyntheticCommercialDeliveryReceipt Simulate(
        CommercialInventoryReview inventory,
        CommercialCreativeQaDecision creative,
        string creatorId, string advertiserId,
        int scheduledDurationSeconds, int scheduledOccurrences,
        string attemptedStage)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(creative);
        if (inventory.Mode != "OFFLINE_SYNTHETIC_ONLY"
            || creative.Mode != "OFFLINE_SYNTHETIC_ONLY")
            throw new InvalidOperationException("Live commercial records cannot be converted to synthetic delivery proof.");
        if (!Stages.Contains(attemptedStage))
            throw new ArgumentException("Unsupported proposed delivery stage.", nameof(attemptedStage));
        if (string.IsNullOrWhiteSpace(inventory.ProductId)
            || string.IsNullOrWhiteSpace(inventory.ProductVersion)
            || string.IsNullOrWhiteSpace(creatorId) || string.IsNullOrWhiteSpace(advertiserId)
            || scheduledDurationSeconds is < 1 or > 3600
            || scheduledOccurrences is < 1 or > 1000)
            throw new InvalidOperationException("Synthetic versioned delivery evidence is incomplete.");
        var geometryVersion = inventory.Geometry?.GeometryVersion ?? "UNVERIFIED";
        if (string.IsNullOrWhiteSpace(geometryVersion)) geometryVersion = "UNVERIFIED";
        var pricingVersion = "PRICING_UNAVAILABLE";
        var candidate = creative.CandidateSha256;
        if (string.IsNullOrWhiteSpace(candidate)) candidate = "UNVERIFIED";
        var fields = new[]
        {
            "AGM345-SYNTHETIC-DELIVERY/V1", inventory.ProductId,
            inventory.ProductVersion, geometryVersion, pricingVersion,
            creatorId, advertiserId, candidate,
            scheduledDurationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            scheduledOccurrences.ToString(System.Globalization.CultureInfo.InvariantCulture),
            attemptedStage
        };
        // Length prefixes remove delimiter ambiguity. No mutable DB object,
        // image, source approval or live runtime state enters this hash.
        var canonical = string.Concat(fields.Select(v =>
            v.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + v));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new SyntheticCommercialDeliveryReceipt(
            "AGM345-SYNTHETIC-DELIVERY/V1",
            "OFFLINE_SYNTHETIC_ONLY", "SYNTHETIC-" + digest[..20],
            inventory.ProductId, inventory.ProductVersion,
            geometryVersion, pricingVersion, creatorId, advertiserId,
            candidate, scheduledDurationSeconds, scheduledOccurrences,
            attemptedStage, "NOT_VERIFIED", 0, "NOT_SENT", digest,
            false, false, false, false,
        [
            "SYNTHETIC — the attempted stage is NOT an observed display.",
            "No verified Economics quote; no valid reservation or placement authorization.",
            "No actual media player impression, timestamped display observation or customer delivery occurred."
        ]);
    }
}
