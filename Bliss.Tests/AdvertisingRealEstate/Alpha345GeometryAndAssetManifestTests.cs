using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.AdvertisingRealEstate;

namespace Bliss.Tests.AdvertisingRealEstate;

public sealed class Alpha345GeometryAndAssetManifestTests
{
    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "assets", "alpha-prototypes", "OWNER-GEOMETRY.tsv")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Owner geometry fixture not present in checkout.");
    }

    [Fact]
    public void Real_owner_geometry_union_is_measured_without_double_count_or_inventing_protected_rights()
    {
        var path = Path.Combine(RepositoryRoot(), "assets", "alpha-prototypes", "OWNER-GEOMETRY.tsv");
        var all = OwnerGeometryFile.Read(File.ReadAllText(path));
        Assert.Equal(7, all.Count);

        var premiumOne = CatalogGeometrySafety.Inspect(
            new[] { all["LEFT_VERTICAL"], all["BOTTOM_FULL"] });
        Assert.Equal("PROTECTED_CREATOR_REGION_UNVERIFIED", premiumOne.Status);
        Assert.Equal(576000, premiumOne.UnionAllocatedPixels);
        Assert.Equal(2778, premiumOne.FrameOccupancyBasisPoints);
        Assert.False(premiumOne.DeclaredCreatorRegionProtected);
        Assert.Contains("NOT percent of sellable", premiumOne.Notice);

        // An explicitly declared center is a TEST-ONLY reference area; this
        // does not claim the actual creator signed authorization or device fit.
        var protectedCenter = new OwnerSlotGeometry(
            "TEST_ONLY_CREATOR_CONTENT", "FIXTURE", 1920, 1080,
            1280, 720, 320, 180, 921600, "Synthetic test-only", "N/A", "TEST");
        var checkedOne = CatalogGeometrySafety.Inspect(
            new[] { all["LEFT_VERTICAL"], all["BOTTOM_FULL"] }, protectedCenter);
        Assert.Equal("VALID_AGAINST_DECLARED_PROTECTED_REGION", checkedOne.Status);
        Assert.True(checkedOne.DeclaredCreatorRegionProtected);

        var premiumTwo = CatalogGeometrySafety.Inspect(new[]
        {
            all["LEFT_VERTICAL"], all["BOTTOM_LEFT"],
            all["RIGHT_VERTICAL"], all["BOTTOM_RIGHT"]
        }, protectedCenter);
        Assert.Equal(921600, premiumTwo.UnionAllocatedPixels);
        Assert.Equal(4444, premiumTwo.FrameOccupancyBasisPoints);
        Assert.True(premiumTwo.DeclaredCreatorRegionProtected);

        var entry = CatalogGeometrySafety.Inspect(new[]
        {
            all["TOP_LEFT"], all["TOP_RIGHT"],
            all["BOTTOM_LEFT"], all["BOTTOM_RIGHT"]
        }, protectedCenter);
        Assert.Equal(460800, entry.UnionAllocatedPixels);
        Assert.Equal(2222, entry.FrameOccupancyBasisPoints);
        Assert.True(entry.DeclaredCreatorRegionProtected);
    }

    [Fact]
    public void Overlapping_rectangles_are_not_double_counted_and_cannot_pass_the_geometry_gate()
    {
        var a = new OwnerSlotGeometry("A", "TEST", 10, 10, 6, 10, 0, 0, 60, "Synthetic", "N/A", "TEST");
        var b = new OwnerSlotGeometry("B", "TEST", 10, 10, 6, 10, 4, 0, 60, "Synthetic", "N/A", "TEST");
        var report = CatalogGeometrySafety.Inspect(new[] { a, b });
        Assert.Equal("OVERLAP_REVIEW_REQUIRED", report.Status);
        Assert.Equal(100, report.UnionAllocatedPixels);  // not 120
        Assert.Equal(10000, report.FrameOccupancyBasisPoints);
        Assert.False(report.DeclaredCreatorRegionProtected);

        var invalid = CatalogGeometrySafety.Inspect(new[]
        {
            b with { OriginX = 7, Area = 60 }
        });
        Assert.Equal("GEOMETRY_INVALID", invalid.Status);
        Assert.Null(invalid.FrameOccupancyBasisPoints);
    }

    [Fact]
    public void Asset_register_has_four_verified_git_blob_objects_and_separates_quality_from_inventory()
    {
        var root = RepositoryRoot();
        var document = Path.Combine(root, "docs", "architecture", "ALPHA-345-ASSET-REGISTER.json");
        using var json = JsonDocument.Parse(File.ReadAllText(document));
        var assets = json.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        var showcases = assets.Where(x => x.GetProperty("assetRole").GetString() is
            "INVENTORY_SHOWCASE" or "EDUCATIONAL_GUIDE").ToArray();
        var quality = assets.Where(x => x.GetProperty("assetRole").GetString() ==
            "CREATIVE_QUALITY_REFERENCE").ToArray();
        Assert.Equal(4, showcases.Length);
        Assert.Equal(5, quality.Length);
        Assert.Equal(3, quality.Count(x => x.GetProperty("fileStatus").GetString() == "VERIFIED_LIBRARY_BYTES"));
        Assert.Equal(2, quality.Count(x => x.GetProperty("fileStatus").GetString() == "EXACT_ASSET_NOT_LOCATED"));

        foreach (var entry in showcases)
        {
            var relative = entry.GetProperty("storageReference").GetString()!;
            var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(file), relative);
            var bytes = File.ReadAllBytes(file);
            var header = Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
            var blob = new byte[header.Length + bytes.Length];
            Buffer.BlockCopy(header, 0, blob, 0, header.Length);
            Buffer.BlockCopy(bytes, 0, blob, header.Length, bytes.Length);
            var actual = "git-sha1:" + Convert.ToHexString(SHA1.HashData(blob)).ToLowerInvariant();
            Assert.Equal(actual, entry.GetProperty("contentDigest").GetString());
            Assert.NotEqual("APPROVED_FOR_LIVE_USE", entry.GetProperty("approvalStatus").GetString());
        }
    }
}
