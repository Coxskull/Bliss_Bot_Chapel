using System.Security.Cryptography;
using System.Text;
using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;

namespace Bliss.Domain.Economics;

/// <summary>
/// Product-specific association from external review. NO SUCH AUTHORITATIVE
/// association currently exists in the Economics Quote schema. This contract
/// is for a versioned OFFLINE fixture only and must never represent a signed
/// real-world commercial approval.
/// </summary>
public sealed record SyntheticQuoteProductBinding(
    string ProductId, string ProductVersion, string GeometryVersion,
    int RequestedOccurrences, Guid CreatorId, Guid AdvertiserOpportunityId,
    Guid QuoteId, Guid QuoteVersionId,
    string SourceReference, string SourceVersion,
    IReadOnlyList<SyntheticQuoteSlotBinding> Slots);

public sealed record SyntheticQuoteSlotBinding(
    string CatalogSlotId, Guid AdInventorySlotId, Guid RateRecommendationId);

public sealed record CommercialEconomicsReadOnlyDecision(
    string Mode, string Status, string QuoteId, string QuoteVersionId,
    string ProductId, string ProductVersion, string GeometryVersion,
    int RequestedOccurrences, string Currency,
    string QuoteEvidenceFingerprintSha256,
    bool VersionAndAmountConsistent, bool RealProductBindingVerified,
    bool CanStatePriceToCustomer, bool AutomatedNegotiationAuthorized,
    bool ReservationAuthorized, bool PaymentAuthorized,
    string DeliveryStatus, IReadOnlyList<string> Checks,
    string SafeCustomerReply);

/// <summary>
/// Read-only, offline inspection of the EXISTING Quote, QuoteVersion,
/// QuoteApprovalDecision, QuoteOutcome, QuoteLineItem and RateRecommendation
/// ledger object models. This never calls QuoteService.RecordOutcomeAsync,
/// never creates a quote, and NEVER promotes a synthetic association to a
/// commercial price for Ask Alpha.
/// </summary>
public static class CommercialEconomicsReadOnlyGate
{
    public const string LinkedFixture = "QUOTE_LEDGER_COHERENT_SYNTHETIC_ONLY";
    public const string Unavailable = "PRICING_UNAVAILABLE";

    public static CommercialEconomicsReadOnlyDecision Inspect(
        CommercialInventoryReview? inventory, CatalogProduct? product,
        Quote? quote, QuoteVersion? version, QuoteApprovalDecision? approval,
        QuoteOutcome? outcome, IReadOnlyList<RateRecommendation>? recommendations,
        SyntheticQuoteProductBinding? binding)
    {
        var checks = new List<string>();
        CommercialEconomicsReadOnlyDecision Withheld(string failure) =>
            new("OFFLINE_SYNTHETIC_ONLY", Unavailable,
                quote?.Id.ToString() ?? "UNVERIFIED",
                version?.Id.ToString() ?? "UNVERIFIED",
                product?.ProductId ?? "UNVERIFIED", product?.Version ?? "UNVERIFIED",
                inventory?.Geometry?.GeometryVersion ?? "UNVERIFIED",
                binding?.RequestedOccurrences ?? 0, "UNVERIFIED", "",
                false, false, false, false, false, false, "NOT_SENT",
                [.. checks, failure],
                "Economics has no verified product-bound accepted quote. Ask Alpha cannot state a price, negotiate, reserve or commit. Human review is required. Delivery remains NOT_SENT.");

        if (inventory is null || product is null
            || inventory.Mode != "OFFLINE_SYNTHETIC_ONLY"
            || inventory.Geometry?.Validated != true
            || inventory.ProductId != product.ProductId
            || inventory.ProductVersion != product.Version
            || !inventory.ShowcaseAllowed
            || product.Lifecycle != RealEstateCatalog.Active)
            return Withheld("INVENTORY_OR_GEOMETRY_NOT_VERIFIED");
        checks.Add("EXISTING_INVENTORY_IDENTITY_MATCHED_SYNTHETIC");

        if (binding is null || quote is null || version is null
            || approval is null || outcome is null || recommendations is null
            || !binding.SourceReference.StartsWith("synthetic://", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(binding.SourceVersion)
            || binding.RequestedOccurrences is < 1 or > 1000
            || binding.CreatorId == Guid.Empty || binding.AdvertiserOpportunityId == Guid.Empty
            || binding.ProductId != product.ProductId || binding.ProductVersion != product.Version
            || binding.GeometryVersion != inventory.Geometry!.GeometryVersion
            || binding.QuoteId != quote.Id || binding.QuoteVersionId != version.Id)
            return Withheld("SYNTHETIC_SOURCE_PROOF_MISSING_OR_MISMATCHED");
        checks.Add("PRODUCT_GEOMETRY_QUANTITY_SOURCE_BOUND_SYNTHETIC");

        if (quote.Id == Guid.Empty || version.Id == Guid.Empty
            || quote.Status != QuoteStatuses.Accepted
            || quote.AdvertiserOpportunityId != binding.AdvertiserOpportunityId
            || quote.CurrentVersionNumber != version.VersionNumber
            || version.QuoteId != quote.Id
            || approval.Decision != QuoteApprovalDecisions.Approved
            || approval.QuoteId != quote.Id || approval.QuoteVersionId != version.Id
            || string.IsNullOrWhiteSpace(approval.ReviewerLabel)
            || string.IsNullOrWhiteSpace(approval.Rationale)
            || outcome.Response != QuoteOutcomeResponses.Accepted
            || outcome.QuoteId != quote.Id || outcome.QuoteVersionId != version.Id
            || outcome.NewQuoteVersionId is not null
            || outcome.Amount != version.TotalAmount
            || version.TotalAmount <= 0
            || version.SubtotalAmount != version.TotalAmount)
            return Withheld("IMMUTABLE_ACCEPTED_QUOTE_VERSION_OR_OUTCOME_INVALID");
        checks.Add("EXISTING_ECONOMICS_ACCEPTED_VERSION_AND_HUMAN_DECISION_MATCH");

        var currency = quote.CurrencyCode.Trim().ToUpperInvariant();
        if (EconomicsPriceSpeech.Speak(EconomicsPriceSpeech.FormatAmount(version.TotalAmount), currency) is null
            || !string.Equals(version.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(outcome.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase)
            || version.LineItems.Count == 0
            || version.LineItems.Count != product.SlotIds.Count
            || binding.Slots is null || binding.Slots.Count != product.SlotIds.Count
            || recommendations.Count != product.SlotIds.Count)
            return Withheld("QUOTE_CURRENCY_OR_LINE_CARDINALITY_MISMATCH");
        checks.Add("ACCEPTED_ECONOMICS_CURRENCY_AND_CARDINALITY_MATCH");

        var slotKeys = new HashSet<string>(StringComparer.Ordinal);
        var linkedRecs = new HashSet<Guid>();
        var linkedSlots = new HashSet<Guid>();
        foreach (var slot in binding.Slots)
        {
            if (!product.SlotIds.Contains(slot.CatalogSlotId, StringComparer.Ordinal)
                || !slotKeys.Add(slot.CatalogSlotId)
                || slot.AdInventorySlotId == Guid.Empty || !linkedSlots.Add(slot.AdInventorySlotId)
                || slot.RateRecommendationId == Guid.Empty || !linkedRecs.Add(slot.RateRecommendationId))
                return Withheld("INVENTORY_SLOT_MAPPING_INVALID");
            var recs = recommendations.Where(r => r.Id == slot.RateRecommendationId).Take(2).ToArray();
            if (recs.Length != 1 || recs[0].CreatorId != binding.CreatorId
                || recs[0].AdvertiserOpportunityId != binding.AdvertiserOpportunityId
                || recs[0].AdInventorySlotId != slot.AdInventorySlotId
                || (recs[0].DurationSeconds is not null
                    && !product.DurationSeconds.Contains(recs[0].DurationSeconds.Value))
                || !string.Equals(recs[0].CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
                return Withheld("RATE_RECOMMENDATION_CREATOR_OR_SLOT_LINK_INVALID");
        }
        checks.Add("EXACT_PRODUCT_SLOT_AND_CREATOR_RECOMMENDATION_LINKS_MATCH_SYNTHETIC");

        var mappedLines = new HashSet<Guid>();
        decimal subtotal = 0;
        foreach (var line in version.LineItems)
        {
            if (line.Id == Guid.Empty || !mappedLines.Add(line.Id)
                || line.QuoteVersionId != version.Id
                || !linkedRecs.Contains(line.RateRecommendationId)
                || line.Quantity != binding.RequestedOccurrences
                || line.UnitAmount <= 0
                || line.LineAmount != line.Quantity * line.UnitAmount
                || !string.Equals(line.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
                return Withheld("OCCURRENCE_QUANTITY_OR_LINE_AMOUNT_MISMATCH");
            subtotal = checked(subtotal + line.LineAmount);
        }
        if (version.LineItems.Select(x => x.RateRecommendationId).Distinct().Count() != linkedRecs.Count
            || subtotal != version.SubtotalAmount)
            return Withheld("CURRENT_VERSION_LINE_TOTAL_OR_DISTINCT_RECOMMENDATION_MISMATCH");
        checks.Add("CURRENT_VERSION_OCCURRENCES_AND_LEDGER_TOTAL_RECONCILED");

        // Audit fingerprint contains only IDs, versions and currency. Neither
        // this digest nor a caller-supplied "synthetic://" reference is a
        // signature or proof that the quote is associated in a real database.
        var evidence = string.Join("|", [
            quote.Id.ToString("N"), version.Id.ToString("N"),
            product.ProductId, product.Version, binding.GeometryVersion,
            binding.RequestedOccurrences.ToString(System.Globalization.CultureInfo.InvariantCulture),
            currency,
            string.Join(",", binding.Slots.OrderBy(s => s.CatalogSlotId, StringComparer.Ordinal)
                .Select(s => s.CatalogSlotId + ":" + s.AdInventorySlotId.ToString("N")
                    + ":" + s.RateRecommendationId.ToString("N")))
        ]);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
        return new("OFFLINE_SYNTHETIC_ONLY", LinkedFixture, quote.Id.ToString(),
            version.Id.ToString(), product.ProductId, product.Version,
            binding.GeometryVersion, binding.RequestedOccurrences, currency,
            fingerprint, true, false, false, false, false, false,
            "NOT_SENT", [.. checks, "NOT_SIGNED_NOT_ACCEPTED_FOR_LIVE_CUSTOMER_USE"],
            "Fictional Economics ledger is internally consistent for this stored inventory product, but the product binding is synthetic. Ask Alpha cannot state a price or negotiate, reserve or commit. Human approval is required. Delivery remains NOT_SENT.");
    }
}
