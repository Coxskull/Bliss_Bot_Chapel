using System.Security.Cryptography;
using System.Text.Json;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

public sealed class HarborlightFreeAttemptEvidenceTests
{
    [Fact]
    public void Generated_attempt_preserves_retrieval_originality_and_fail_closed_statuses()
    {
        var root = RepositoryRoot();
        var evidencePath = Path.Combine(
            root,
            "assets",
            "alpha-prototypes",
            "creative-academy",
            "harborlight",
            "HV-001-evidence.json");
        using var evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
        var record = evidence.RootElement;

        Assert.Equal("GENERATED_PENDING_HUMAN_REVIEW", record.GetProperty("status").GetString());
        Assert.False(record.GetProperty("retrieval").GetProperty("manuallySelected").GetBoolean());
        Assert.False(record.GetProperty("retrieval").GetProperty("referenceAssetsSentToProvider").GetBoolean());
        Assert.Equal("GQD-1", record.GetProperty("qualityDna").GetProperty("version").GetString());
        Assert.Equal("UNCLASSIFIED", record.GetProperty("qualityDna").GetProperty("gradeStatus").GetString());
        Assert.Equal("UNREPORTED_BY_PROVIDER", record.GetProperty("provider").GetProperty("model").GetString());
        Assert.Equal("UNREPORTED_BY_PROVIDER", record.GetProperty("provider").GetProperty("jobId").GetString());
        Assert.Equal("UNREPORTED_BY_PROVIDER", record.GetProperty("provider").GetProperty("costStatus").GetString());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("provider").GetProperty("actualCost").ValueKind);
        Assert.False(record.GetProperty("provider").GetProperty("newSubscriptionPurchased").GetBoolean());

        var original = Resolve(root, record.GetProperty("original").GetProperty("path").GetString());
        var adapted = Resolve(root, record.GetProperty("adaptation").GetProperty("path").GetString());
        Assert.Equal(
            record.GetProperty("original").GetProperty("sha256").GetString(),
            Sha256(original));
        Assert.Equal(
            record.GetProperty("adaptation").GetProperty("sha256").GetString(),
            Sha256(adapted));
        Assert.Equal((1280, 720), ReadJpegDimensions(File.ReadAllBytes(original)));
        Assert.Equal((1920, 1080), ReadPngDimensions(File.ReadAllBytes(adapted)));
        var adaptation = record.GetProperty("adaptation");
        Assert.Equal("PURPOSE_BUILT_TEST_PREVIEW", adaptation.GetProperty("status").GetString());
        Assert.False(adaptation.GetProperty("prototypeScaled").GetBoolean());
        var banner = adaptation.GetProperty("bottomBanner");
        Assert.Equal("PURPOSE_BUILT_FROM_ORIGINAL_MARK", banner.GetProperty("method").GetString());
        Assert.Equal("Prescription pickup, ready when you are.", banner.GetProperty("headline").GetString());
        Assert.True(banner.GetProperty("headlineComplete").GetBoolean());
        Assert.False(banner.GetProperty("photographCrop").GetBoolean());

        var manifest = Path.Combine(
            root,
            "assets",
            "alpha-prototypes",
            "creative-academy",
            "MANIFEST.tsv");
        var inbox = Path.GetDirectoryName(manifest)!;
        var references = ReferenceLibrary.ParseManifest(
            File.ReadAllText(manifest),
            name => File.Exists(Path.Combine(inbox, "inbox", name)));
        var retrieval = ReferenceLibrary.Select(
            "pharmacy",
            references,
            ["product realism", "lighting and depth", "typography"]);
        Assert.Equal("RETRIEVED", retrieval.Status);
        Assert.Equal(
            ["ACA-001-V1", "ACA-002-V1", "ACA-006-V1", "ACA-008-V1"],
            retrieval.Selected.Select(item => item.ReferenceId).ToArray());

        var originality = ReferenceLibrary.Compare(
            "Harborlight Pharmacy",
            "Prescription pickup, ready when you are.",
            "",
            "",
            references);
        Assert.Equal("PASS", originality.Status);
        Assert.False(originality.CampaignReady);
        Assert.Equal("NOT_SENT", originality.Delivery);

        var checks = record.GetProperty("checks");
        Assert.Equal("NOT_RUN", checks.GetProperty("pixelSimilarity").GetString());
        Assert.Equal("NOT_RUN", checks.GetProperty("visualQa").GetString());
        Assert.Equal("NOT_REQUESTED", checks.GetProperty("humanReview").GetString());
        Assert.Equal("BASELINE_NOT_RECORDED", checks.GetProperty("regression").GetString());
        Assert.False(checks.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", checks.GetProperty("delivery").GetString());
        Assert.Equal("OPEN", record.GetProperty("amendments").GetProperty("creativeAcademy").GetString());
        Assert.Equal("OPEN", record.GetProperty("amendments").GetProperty("advertisingRealEstate").GetString());
        Assert.Equal("UNCLAIMED", record.GetProperty("amendments").GetProperty("hostedAcceptance").GetString());
    }

    private static string Resolve(string root, string? relative)
    {
        Assert.False(string.IsNullOrWhiteSpace(relative));
        var path = Path.GetFullPath(Path.Combine(root, relative!));
        Assert.StartsWith(root, path, StringComparison.Ordinal);
        Assert.True(File.Exists(path));
        return path;
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static (int Width, int Height) ReadPngDimensions(byte[] bytes)
    {
        Assert.True(bytes.Length >= 24);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        return (
            ReadBigEndian(bytes, 16, 4),
            ReadBigEndian(bytes, 20, 4));
    }

    private static (int Width, int Height) ReadJpegDimensions(byte[] bytes)
    {
        Assert.True(bytes.Length > 4);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
        var offset = 2;
        while (offset + 8 < bytes.Length)
        {
            if (bytes[offset++] != 0xFF)
            {
                continue;
            }

            var marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9)
            {
                continue;
            }

            var length = ReadBigEndian(bytes, offset, 2);
            if (marker is >= 0xC0 and <= 0xC3)
            {
                return (
                    ReadBigEndian(bytes, offset + 5, 2),
                    ReadBigEndian(bytes, offset + 3, 2));
            }

            offset += length;
        }

        throw new InvalidOperationException("The JPEG dimensions were not found.");
    }

    private static int ReadBigEndian(byte[] bytes, int offset, int count)
    {
        var value = 0;
        for (var index = 0; index < count; index++)
        {
            value = (value << 8) | bytes[offset + index];
        }

        return value;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BlissBotChapel.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
