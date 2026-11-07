namespace Bliss.Domain.AdvertisingRealEstate;

/// <summary>Offline-only proof inputs. These do NOT create database authority.</summary>
public sealed record CreatorProductProof(
    string ProductId, string ProductVersion, string CreatorId, string AuthorizationVersion,
    string SourceReference, bool Granted);

public sealed record InventoryAvailabilityProof(
    string ProductId, string ProductVersion, string State, int AvailableAdvertiserPlaces,
    string SourceReference, string Version);

public sealed record DevicePlatformProof(
    string Platform, string Device, string Version, string State);

public sealed record ShowcaseAssetProof(
    string ShowcaseId, string Version, string Sha256, string SourceReference, bool Approved);

public sealed record CommercialTrace(string Step, string Result, string Reference);

public sealed record CommercialInventoryReview(
    string Mode, string AvailabilityStatus, string ProductId, string ProductVersion,
    string? ShowcaseId, CommercialGeometryEvidence? Geometry,
    string PricingStatus, bool HumanEscalation, bool ShowcaseAllowed,
    bool ReservationAuthorized, bool CommitmentAuthorized, bool DeliveryVerified,
    string DeliveryStatus, string SafeReply, IReadOnlyList<CommercialTrace> Trace);

/// <summary>
/// Bridges the existing RealEstateCatalog to a strictly fictional Ask Alpha
/// retrieval *test*. It does not fetch from a DB or pretend that mock approval
/// records are signed creator permissions. A real service must provide the
/// current authoritative records and verified Economics quote readback later.
/// </summary>
public static class CommercialInventoryReviewGate
{
    public static CommercialInventoryReview Evaluate(
        CatalogBoard board,
        string? requestedProductId,
        IReadOnlyDictionary<string, OwnerSlotGeometry>? geometry,
        IReadOnlyList<ProtectedCreatorRegion>? protectedRegions,
        long? sellableArea,
        CreatorProductProof? creator,
        InventoryAvailabilityProof? availability,
        DevicePlatformProof? compatibility,
        ShowcaseAssetProof? showcase,
        int requestedOccurrences = 5,
        bool needsExclusivity = false)
    {
        ArgumentNullException.ThrowIfNull(board);
        var trace = new List<CommercialTrace>();
        CommercialInventoryReview Reject(string state, string step, string evidence, CatalogProduct? selected = null,
            CommercialGeometryEvidence? geometryEvidence = null)
        {
            trace.Add(new(step, state, evidence));
            return new("OFFLINE_SYNTHETIC_ONLY", state, selected?.ProductId ?? string.Empty,
                selected?.Version ?? string.Empty, null, geometryEvidence,
                "PRICING_UNAVAILABLE", true, false, false, false, false,
                "NOT_SENT", "No authorized offer or price is available. Human review is required. Delivery remains NOT_SENT.",
                trace.ToArray());
        }

        // Preserve the existing catalog identity and lifecycle. Never auto-create a product.
        var products = board.Products.Where(p => p.ProductId == requestedProductId).Take(2).ToArray();
        if (products.Length != 1)
            return Reject("UNKNOWN", "ActiveProductFilter", "No unique stored Inventory Product identity.");
        var product = products[0];
        if (product.Lifecycle != RealEstateCatalog.Active)
            return Reject("UNAVAILABLE", "ActiveProductFilter", "Stored product lifecycle is not ACTIVE.", product);
        trace.Add(new("ActiveProductFilter", "PASS_SYNTHETIC", product.ProductId + "/" + product.Version));

        if (!board.CreatorAuthorized || creator is null || !creator.Granted
            || creator.ProductId != product.ProductId || creator.ProductVersion != product.Version
            || string.IsNullOrWhiteSpace(creator.CreatorId) || string.IsNullOrWhiteSpace(creator.SourceReference)
            || string.IsNullOrWhiteSpace(creator.AuthorizationVersion))
            return Reject("NEEDS_REVIEW", "CreatorAuthorizationFilter",
                "Creator identity/versioned permission absent, mismatched or not granted.", product);
        trace.Add(new("CreatorAuthorizationFilter", "PASS_SYNTHETIC", creator.SourceReference));

        if (availability is null || availability.ProductId != product.ProductId
            || availability.ProductVersion != product.Version || availability.State != "AVAILABLE"
            || string.IsNullOrWhiteSpace(availability.SourceReference)
            || string.IsNullOrWhiteSpace(availability.Version))
            return Reject("UNKNOWN", "AvailabilityFilter", "Availability/version proof missing or not AVAILABLE.", product);
        if (availability.AvailableAdvertiserPlaces < 1
            || availability.AvailableAdvertiserPlaces > product.MaximumAdvertisers
            || requestedOccurrences < 1 || requestedOccurrences > 1000)
            return Reject("UNAVAILABLE", "AvailabilityFilter", "No valid capacity or requested occurrence policy.", product);
        if (needsExclusivity && (product.MaximumAdvertisers != 1
            || !product.Exclusivity.StartsWith("EXCLUSIVE", StringComparison.Ordinal)))
            return Reject("UNAVAILABLE", "ExclusivityFilter", "Shared inventory cannot promise exclusive display.", product);
        trace.Add(new("AvailabilityFilter", "PASS_SYNTHETIC", availability.SourceReference));

        if (compatibility is null || compatibility.State != "SUPPORTED"
            || string.IsNullOrWhiteSpace(compatibility.Version)
            || string.IsNullOrWhiteSpace(compatibility.Device)
            || string.IsNullOrWhiteSpace(compatibility.Platform)
            // The stored geometry is the current desktop frame only. A named
            // mobile variant must be separately approved, not auto-resized.
            || !string.Equals(compatibility.Device, "desktop", StringComparison.OrdinalIgnoreCase)
            || product.DeviceStatus != "OWNER_APPROVED" || product.PlatformStatus != "OWNER_APPROVED")
            return Reject("NEEDS_REVIEW", "PlatformCompatibilityFilter",
                "Unverified platform, device or version. Mobile is not inferred from desktop.", product);
        trace.Add(new("PlatformCompatibilityFilter", "PASS_SYNTHETIC",
            compatibility.Platform + "/" + compatibility.Device + "/" + compatibility.Version));

        var checkedGeometry = CommercialGeometryGuard.Review(product, geometry, protectedRegions, sellableArea);
        if (!checkedGeometry.Validated)
            return Reject("NEEDS_REVIEW", "GeometryVariantResolution", checkedGeometry.Status, product, checkedGeometry);
        trace.Add(new("GeometryVariantResolution", "PASS_SYNTHETIC", checkedGeometry.GeometryVersion));

        var sourceShowcase = board.Showcases.Where(a => a.ShowcaseId == product.ShowcaseId).Take(2).ToArray();
        if (sourceShowcase.Length != 1 || sourceShowcase[0].Role == "EDUCATIONAL"
            || sourceShowcase[0].Lifecycle != RealEstateCatalog.Active
            || !sourceShowcase[0].AssetPresent
            || showcase is null || !showcase.Approved || showcase.ShowcaseId != product.ShowcaseId
            || string.IsNullOrWhiteSpace(showcase.Version)
            || string.IsNullOrWhiteSpace(showcase.SourceReference)
            || !IsSha256(showcase.Sha256))
            return Reject("NEEDS_REVIEW", "ShowcaseAssetResolution",
                "An approved asset digest and matching active product showcase are required.", product, checkedGeometry);
        trace.Add(new("ShowcaseAssetResolution", "PASS_SYNTHETIC",
            showcase.ShowcaseId + "/" + showcase.Version + "/" + showcase.Sha256.ToLowerInvariant()));

        // Crucially, a caller-supplied price or a fictional fixture is NOT a
        // verified quote. The existing EconomicsAcceptedPriceReader is the only
        // accepted-value readback in the API; no binding lookup occurs here.
        trace.Add(new("EconomicsQuoteLookup", "PRICING_UNAVAILABLE",
            "No verified Economics quote/version bound to this exact product, geometry and requested occurrences: " + requestedOccurrences + "."));
        trace.Add(new("NegotiationBoundaryValidation", "HUMAN_ESCALATION_REQUIRED",
            "No authorized price or negotiation envelope; no reservation can be created."));
        return new("OFFLINE_SYNTHETIC_ONLY", "NEEDS_REVIEW",
            product.ProductId, product.Version, showcase.ShowcaseId, checkedGeometry,
            "PRICING_UNAVAILABLE", true, true, false, false, false, "NOT_SENT",
            "Synthetic reference only: stored Inventory Product " + product.ProductId
            + " has a matching example showcase. Creator/availability/geometry checks were simulated; "
            + "Economics has no verified quote for this product and cannot state a price. "
            + "No reservation, commitment or delivery occurred.", trace.ToArray());
    }

    private static bool IsSha256(string? value) =>
        value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);
}
