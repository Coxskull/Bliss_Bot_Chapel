using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class BlueprintPhaseService(BlissDbContext database, IWebHostEnvironment environment)
{
    private static readonly Regex ReferenceIdPattern = new("^ACA-[0-9]{3}-V1$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> ShowcaseIds = new(StringComparer.Ordinal)
    {
        RealEstateCatalog.GuideId,
        "ARE-001-V1",
        "ARE-002-V1",
        "ARE-003-V1"
    };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };
    public async Task<BlueprintReading> ReadAsync(CancellationToken cancellationToken)
    {
        var assets = ReadAssets();
        var approval = OwnerAuthorizationFile.Read(ReadAuthorization());
        var geometry = OwnerGeometryFile.Read(ReadGeometry());
        var overrides = new Dictionary<string, string>(approval.LifecycleOverrides, StringComparer.Ordinal);
        foreach (var stored in await LifecycleOverridesAsync(cancellationToken))
        {
            overrides.TryAdd(stored.Key, stored.Value);
        }

        var board = RealEstateCatalog.Board(
            assets,
            approval.CreatorAuthorized,
            lifecycleOverrides: overrides,
            deviceStatus: approval.DeviceStatus,
            platformStatus: approval.PlatformStatus,
            geometry: geometry);
        // A catalog GET is an inspection, never an inventory write or activation.
        // Canonical records require a separate authorized lifecycle operation.
        var manifest = ReadManifest();
        var references = ReferenceLibrary.ParseManifest(manifest, name => AssetExists(name, assets));
        return new BlueprintReading(
            board,
            references,
            ListUnassigned(references),
            ReadOwnerNeeds(),
            ReferenceLibrary.QualityDna,
            ReferenceLibrary.DoNotProduce,
            ReferenceLibrary.RegressionBriefs);
    }

    public IReadOnlyList<AcademyReferenceRecord> ReadReferences()
    {
        var assets = ReadAssets();
        return ReferenceLibrary.ParseManifest(ReadManifest(), name => AssetExists(name, assets));
    }

    public BlueprintFile? OpenReference(string? referenceId)
    {
        if (string.IsNullOrWhiteSpace(referenceId) || !ReferenceIdPattern.IsMatch(referenceId))
        {
            return null;
        }

        var record = ReferenceLibrary.ParseManifest(ReadManifest(), name => AssetExists(name, ReadAssets()))
            .FirstOrDefault(item => item.ReferenceId.Equals(referenceId, StringComparison.Ordinal));
        if (record is null || !record.AssetPresent)
        {
            return null;
        }

        return OpenInboxFile(record.ExpectedFile, claimedOnly: true);
    }

    public BlueprintFile? OpenShowcase(string? showcaseId)
    {
        if (string.IsNullOrWhiteSpace(showcaseId) || !ShowcaseIds.Contains(showcaseId))
        {
            return null;
        }

        var root = Path.Combine(PrototypeRoot(), "advertising-real-estate", "showcase");
        if (showcaseId == RealEstateCatalog.GuideId)
        {
            return OpenContainedFile(root, showcaseId + ".png");
        }

        var folder = Path.Combine(root, showcaseId);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        var file = Directory.EnumerateFiles(folder)
            .FirstOrDefault(path => IsImage(path) && !path.EndsWith(".gitkeep", StringComparison.OrdinalIgnoreCase));
        return file is null ? null : ToBlueprintFile(folder, file);
    }

    public BlueprintFile? OpenUnassigned(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(name, fileName, StringComparison.Ordinal) || !IsImage(name))
        {
            return null;
        }

        var references = ReferenceLibrary.ParseManifest(ReadManifest(), stored => AssetExists(stored, ReadAssets()));
        var allowed = ListUnassigned(references).Any(item => item.FileName.Equals(name, StringComparison.Ordinal));
        return allowed ? OpenInboxFile(name, claimedOnly: false) : null;
    }

    public async Task<JointAcceptanceReading> ReadJointAsync(CancellationToken cancellationToken)
    {
        var reading = await ReadAsync(cancellationToken);
        return JointAcceptance.Read(
            reading.Catalog,
            reading.References,
            reading.Catalog.Slots,
            reading.Catalog.CreatorAuthorized
                ? "Owner authorized the offer on 2026-10-07. A reservation count was not supplied."
                : null,
            reading.OwnerNeeds.Count(item => item.Status == OwnerNeeds.Open));
    }

    public async Task<CatalogTurn> AskAsync(string? message, CancellationToken cancellationToken)
    {
        var reading = await ReadAsync(cancellationToken);
        // Legacy demo route must never promote owner-wide TSV approval to
        // individual creator permission. Creator+product+slot+period evidence
        // is required before any customer-facing offer.
        var turn = RealEstateCatalog.Reply(message, reading.Catalog with { CreatorAuthorized = false });
        database.CatalogConversations.Add(new CatalogConversationRow
        {
            Id = Guid.NewGuid(),
            Intent = turn.Intent,
            Message = (message ?? string.Empty).Trim(),
            Reply = turn.Reply,
            ProductIds = string.Join(",", turn.ProductIds),
            ShowcaseIds = string.Join(",", turn.ShowcaseIds),
            ShowcaseDisplayed = turn.ShowcaseDisplayed,
            HumanEscalation = turn.HumanEscalation,
            InventedProduct = turn.InventedProduct,
            InventedPrice = turn.InventedPrice,
            ModelCalls = turn.ModelCalls,
            CampaignReady = turn.CampaignReady,
            Delivery = turn.Delivery,
            RecordedAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return turn;
    }

    /// <summary>
    /// Read-only, fail-closed evidence lookup. Owner-wide product authorization
    /// cannot establish creator-specific consent or an accepted Economics price.
    /// </summary>
    public async Task<BlueprintCommerceEvidence> AskEvidenceAsync(string message, CancellationToken cancellationToken)
    {
        var stored = await database.RealEstateProducts.AsNoTracking().ToListAsync(cancellationToken);
        var lifecycles = stored.ToDictionary(row => row.ProductId, row => row.Lifecycle, StringComparer.Ordinal);
        var geometry = OwnerGeometryFile.Read(ReadGeometry());
        var board = RealEstateCatalog.Board(
            ReadAssets(), creatorAuthorized: false,
            lifecycleOverrides: lifecycles, geometry: geometry);
        var turn = RealEstateCatalog.Reply(message, board);

        // Unsupported $10 / 70% demands cannot turn into alternate sale offers.
        var candidateIds = turn.Intent == "UNSUPPORTED"
            ? new HashSet<string>(StringComparer.Ordinal)
            : turn.ProductIds.ToHashSet(StringComparer.Ordinal);
        var candidates = new List<BlueprintCommerceCandidate>();
        foreach (var product in board.Products.Where(item => candidateIds.Contains(item.ProductId)))
        {
            var row = stored.FirstOrDefault(item => item.ProductId == product.ProductId);
            if (row is null || row.Lifecycle != RealEstateCatalog.Active
                || row.Version != product.Version
                || row.ShowcaseId != product.ShowcaseId
                || !string.Equals(row.SlotIds, string.Join(",", product.SlotIds), StringComparison.Ordinal))
                continue;

            // Existing physical asset resolver; no separate storage service.
            var image = OpenShowcase(row.ShowcaseId);
            var sha256 = image is null ? null :
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(image.FullPath))).ToLowerInvariant();
            var slots = product.SlotIds.Where(geometry.ContainsKey)
                .Select(slotId => geometry[slotId]).ToArray();
            candidates.Add(new BlueprintCommerceCandidate(
                row.ProductId, row.Version, row.Lifecycle, row.Tier,
                row.Exclusivity, row.MaximumAdvertisers, row.ShowcaseId,
                image is not null, sha256,
                image is null ? null
                    : "/api/operations/blueprint/showcases/" + Uri.EscapeDataString(row.ShowcaseId) + "/image",
                slots.Length == product.SlotIds.Count
                    ? "OWNER_RECOMMENDATION_ONLY" : "GEOMETRY_UNRECORDED",
                slots,
                "CREATOR_SPECIFIC_AUTHORIZATION_MISSING",
                "PRODUCT_SLOT_AVAILABILITY_UNVERIFIED",
                "DEVICE_PLATFORM_VARIANTS_UNVERIFIED",
                "DURATION_FREQUENCY_UNVERIFIED",
                "PRICING_UNAVAILABLE",
                "Economics quotes are not bound to these catalog product IDs.",
                false, "NOT_SENT"));
        }

        // No ProductId <-> AdInventorySlotId -> creator consent / accepted quote
        // lineage exists in the current schema. Do not try to infer it from
        // owner-wide flags, snapshots, a benchmark, or a showcase filename.
        return new BlueprintCommerceEvidence(
            "INSPECTION_ONLY_NOT_A_SELLABLE_OFFER",
            "public.RealEstateProducts / existing showcase files; candidate records are never reservations",
            turn, candidates,
            "The owner-wide catalog authorization does not prove creator-specific slot permission.",
            "Availability and simultaneous advertiser capacity are unverified.",
            "No Economics-accepted price or negotiation boundary is bound to these catalog product IDs.",
            "Showcase art proves a file exists, not permission or a commercial commitment.",
            "LOCALIZATION_REVIEW_REQUIRED",
            false, false, "NOT_SENT", 0);
    }

    public ReferenceRetrieval Retrieve(string? nicheKey, IReadOnlyList<AcademyReferenceRecord> library, IReadOnlyList<string>? reasons) =>
        ReferenceLibrary.Select(nicheKey, library, reasons);

    public async Task<BlueprintAdaptation> AdaptAsync(BlueprintAdaptRequest? request, CancellationToken cancellationToken)
    {
        var reading = await ReadAsync(cancellationToken);
        var productId = (request?.ProductId ?? string.Empty).Trim();
        var product = reading.Catalog.Products.FirstOrDefault(item =>
            item.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            throw new InvalidOperationException("That inventory product is not stored. None was invented.");
        }

        var adaptation = RealEstateCatalog.Adapt(product, reading.Catalog.Slots, request?.ScalePrototype ?? false);
        var similarity = ReferenceLibrary.Compare(
            request?.BrandName,
            request?.Headline,
            request?.Face,
            request?.Product,
            reading.References);
        return new BlueprintAdaptation(
            adaptation,
            similarity,
            CreativeAcademy.WorkerDuties,
            0,
            false,
            "NOT_SENT");
    }

    private async Task<Dictionary<string, string>> LifecycleOverridesAsync(CancellationToken cancellationToken)
    {
        var rows = await database.RealEstateProducts.AsNoTracking().ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.ProductId, item => item.Lifecycle, StringComparer.Ordinal);
    }

    private Dictionary<string, bool> ReadAssets()
    {
        var root = PrototypeRoot();
        var map = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            [RealEstateCatalog.GuideId] = File.Exists(Path.Combine(root, "advertising-real-estate", "showcase", RealEstateCatalog.GuideId + ".png"))
        };
        foreach (var id in new[] { "ARE-001-V1", "ARE-002-V1", "ARE-003-V1" })
        {
            var folder = Path.Combine(root, "advertising-real-estate", "showcase", id);
            map[id] = Directory.Exists(folder) && Directory.EnumerateFiles(folder).Any(file =>
                !file.EndsWith(".gitkeep", StringComparison.OrdinalIgnoreCase));
        }

        return map;
    }

    private bool AssetExists(string name, IReadOnlyDictionary<string, bool> assets)
    {
        if (assets.TryGetValue(Path.GetFileNameWithoutExtension(name), out var known))
        {
            return known;
        }

        var inbox = Path.Combine(PrototypeRoot(), "creative-academy", "inbox", name);
        return File.Exists(inbox);
    }

    private string ReadAuthorization()
    {
        var path = Path.Combine(PrototypeRoot(), "OWNER-AUTHORIZATION.tsv");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private string ReadGeometry()
    {
        var path = Path.Combine(PrototypeRoot(), "OWNER-GEOMETRY.tsv");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private string ReadManifest()
    {
        var path = Path.Combine(PrototypeRoot(), "creative-academy", "MANIFEST.tsv");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private IReadOnlyList<OwnerNeed> ReadOwnerNeeds()
    {
        var path = Path.Combine(PrototypeRoot(), "OWNER-NEEDS.tsv");
        var text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        return OwnerNeeds.Parse(text);
    }

    private string PrototypeRoot()
    {
        var current = new DirectoryInfo(environment.ContentRootPath);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "assets", "alpha-prototypes");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "assets", "alpha-prototypes"));
    }

    private IReadOnlyList<UnassignedUpload> ListUnassigned(IReadOnlyList<AcademyReferenceRecord> references)
    {
        var inbox = Path.Combine(PrototypeRoot(), "creative-academy", "inbox");
        if (!Directory.Exists(inbox))
        {
            return [];
        }

        var claimed = new HashSet<string>(references.Select(item => item.ExpectedFile), StringComparer.Ordinal);
        return Directory.EnumerateFiles(inbox)
            .Select(Path.GetFileName)
            .Where(name => name is not null && IsImage(name) && !claimed.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new UnassignedUpload(name!, ReferenceLibrary.UnassignedReason))
            .ToArray();
    }

    private BlueprintFile? OpenInboxFile(string fileName, bool claimedOnly)
    {
        var name = Path.GetFileName(fileName);
        if (!string.Equals(name, fileName, StringComparison.Ordinal) || !IsImage(name))
        {
            return null;
        }

        if (claimedOnly)
        {
            var claimed = ReferenceLibrary.ParseManifest(ReadManifest(), stored => AssetExists(stored, ReadAssets()))
                .Any(item => item.AssetPresent && item.ExpectedFile.Equals(name, StringComparison.Ordinal));
            if (!claimed)
            {
                return null;
            }
        }

        return OpenContainedFile(Path.Combine(PrototypeRoot(), "creative-academy", "inbox"), name);
    }

    private static BlueprintFile? OpenContainedFile(string directory, string fileName)
    {
        var root = Path.GetFullPath(directory);
        var full = Path.GetFullPath(Path.Combine(root, fileName));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal) || !File.Exists(full) || !IsImage(full))
        {
            return null;
        }

        return new BlueprintFile(full, ContentType(full));
    }

    private static BlueprintFile? ToBlueprintFile(string directory, string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        return name is null ? null : OpenContainedFile(directory, name);
    }

    private static bool IsImage(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path));

    private static string ContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}

public sealed record BlueprintReading(
    CatalogBoard Catalog,
    IReadOnlyList<AcademyReferenceRecord> References,
    IReadOnlyList<UnassignedUpload> Unassigned,
    IReadOnlyList<OwnerNeed> OwnerNeeds,
    IReadOnlyList<string> QualityDna,
    IReadOnlyList<string> DoNotProduce,
    IReadOnlyList<string> RegressionBriefs);

public sealed record UnassignedUpload(string FileName, string Reason);

public sealed record BlueprintFile(string FullPath, string ContentType);

public sealed record BlueprintAdaptRequest(
    string? ProductId,
    bool ScalePrototype,
    string? BrandName,
    string? Headline,
    string? Face,
    string? Product);

public sealed record BlueprintAdaptation(
    InventoryAdaptation Adaptation,
    SimilarityResult Similarity,
    IReadOnlyList<WorkerDuty> Workers,
    int ModelCalls,
    bool CampaignReady,
    string Delivery);

public sealed record BlueprintCommerceCandidate(
    string ProductId,
    string Version,
    string Lifecycle,
    string Tier,
    string Exclusivity,
    int MaximumAdvertisers,
    string ShowcaseId,
    bool ShowcaseAssetRetrieved,
    string? ShowcaseSha256,
    string? ShowcaseImageUrl,
    string GeometryStatus,
    IReadOnlyList<OwnerSlotGeometry> OwnerRecommendedSlots,
    string CreatorAuthorizationStatus,
    string AvailabilityStatus,
    string DeviceCompatibilityStatus,
    string ScheduleStatus,
    string EconomicsPricingStatus,
    string EconomicsSource,
    bool OfferEligible,
    string Delivery);

public sealed record BlueprintCommerceEvidence(
    string Status,
    string InventorySource,
    CatalogTurn Turn,
    IReadOnlyList<BlueprintCommerceCandidate> Candidates,
    string CreatorAuthorizationNotice,
    string AvailabilityNotice,
    string EconomicsNotice,
    string ShowcaseNotice,
    string LocalizationStatus,
    bool OfferEligible,
    bool CampaignReady,
    string Delivery,
    int ModelCalls);
