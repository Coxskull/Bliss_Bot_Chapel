using System.Text.RegularExpressions;
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
        var overrides = await LifecycleOverridesAsync(cancellationToken);
        var board = RealEstateCatalog.Board(assets, lifecycleOverrides: overrides);
        await MirrorProductsAsync(board, cancellationToken);
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

    public async Task<CatalogTurn> AskAsync(string? message, CancellationToken cancellationToken)
    {
        var reading = await ReadAsync(cancellationToken);
        var turn = RealEstateCatalog.Reply(message, reading.Catalog);
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

    public ReferenceRetrieval Retrieve(string? nicheKey, IReadOnlyList<AcademyReferenceRecord> library, IReadOnlyList<string>? reasons) =>
        ReferenceLibrary.Select(nicheKey, library, reasons);

    private async Task<Dictionary<string, string>> LifecycleOverridesAsync(CancellationToken cancellationToken)
    {
        var rows = await database.RealEstateProducts.AsNoTracking().ToListAsync(cancellationToken);
        return rows.ToDictionary(item => item.ProductId, item => item.Lifecycle, StringComparer.Ordinal);
    }

    private async Task MirrorProductsAsync(CatalogBoard board, CancellationToken cancellationToken)
    {
        var existing = await database.RealEstateProducts.ToListAsync(cancellationToken);
        foreach (var product in board.Products)
        {
            var row = existing.FirstOrDefault(item => item.ProductId == product.ProductId);
            if (row is null)
            {
                database.RealEstateProducts.Add(new RealEstateProductRow
                {
                    ProductId = product.ProductId,
                    Version = product.Version,
                    Tier = product.Tier,
                    Exclusivity = product.Exclusivity,
                    MaximumAdvertisers = product.MaximumAdvertisers,
                    SlotIds = string.Join(",", product.SlotIds),
                    ShowcaseId = product.ShowcaseId,
                    Lifecycle = product.Lifecycle,
                    OccupancyStatus = product.OccupancyStatus,
                    DeviceStatus = product.DeviceStatus,
                    PlatformStatus = product.PlatformStatus,
                    Notice = product.Notice
                });
            }
        }

        if (database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }
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
