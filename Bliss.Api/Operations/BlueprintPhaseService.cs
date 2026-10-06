using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class BlueprintPhaseService(BlissDbContext database, IWebHostEnvironment environment)
{
    public async Task<BlueprintReading> ReadAsync(CancellationToken cancellationToken)
    {
        var assets = ReadAssets();
        var overrides = await LifecycleOverridesAsync(cancellationToken);
        var board = RealEstateCatalog.Board(assets, lifecycleOverrides: overrides);
        await MirrorProductsAsync(board, cancellationToken);
        var manifest = ReadManifest();
        var references = ReferenceLibrary.ParseManifest(manifest, name => AssetExists(name, assets));
        return new BlueprintReading(board, references, ReferenceLibrary.QualityDna, ReferenceLibrary.DoNotProduce, ReferenceLibrary.RegressionBriefs);
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
}

public sealed record BlueprintReading(
    CatalogBoard Catalog,
    IReadOnlyList<AcademyReferenceRecord> References,
    IReadOnlyList<string> QualityDna,
    IReadOnlyList<string> DoNotProduce,
    IReadOnlyList<string> RegressionBriefs);
