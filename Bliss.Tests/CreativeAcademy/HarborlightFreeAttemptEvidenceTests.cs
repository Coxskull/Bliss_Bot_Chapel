using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        Assert.Equal("MEASURED", checks.GetProperty("pixelSimilarity").GetString());
        Assert.Equal("NOT_RUN", checks.GetProperty("visualQa").GetString());
        Assert.Equal("NOT_REQUESTED", checks.GetProperty("humanReview").GetString());
        Assert.Equal("BASELINE_NOT_RECORDED", checks.GetProperty("regression").GetString());
        Assert.False(checks.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", checks.GetProperty("delivery").GetString());
        Assert.Equal("OPEN", record.GetProperty("amendments").GetProperty("creativeAcademy").GetString());
        Assert.Equal("OPEN", record.GetProperty("amendments").GetProperty("advertisingRealEstate").GetString());
        Assert.Equal("UNCLAIMED", record.GetProperty("amendments").GetProperty("hostedAcceptance").GetString());

        var sheet = HarborlightReview.ParseEvidence(File.ReadAllText(evidencePath));
        var prepared = HarborlightReview.Prepare(
            "HV-001",
            record.GetProperty("original").GetProperty("sha256").GetString()!,
            record.GetProperty("adaptation").GetProperty("sha256").GetString()!);
        Assert.Equal(prepared.AttemptId, sheet.AttemptId);
        Assert.Equal(prepared.Status, sheet.Status);
        Assert.Equal(prepared.OriginalSha256, sheet.OriginalSha256);
        Assert.Equal(prepared.AdaptedSha256, sheet.AdaptedSha256);
        Assert.Equal(prepared.Notice, sheet.Notice);
        Assert.Equal(ReferenceLibrary.QualityDna, sheet.Attributes.Select(item => item.Name).ToArray());
        Assert.All(sheet.Attributes, item => Assert.Equal(ReferenceLibrary.Unclassified, item.Grade));
        Assert.Equal(HarborlightReview.CopyCheckNames, sheet.CopyChecks.Select(item => item.Name).ToArray());
        Assert.All(sheet.CopyChecks, item => Assert.Equal(HarborlightReview.NotReviewed, item.Status));
        Assert.All(sheet.Questions, item => Assert.Equal(HarborlightReview.Unrecorded, item.Status));
        Assert.Equal(HarborlightReview.NotRequested, sheet.HumanReview);
        Assert.False(sheet.CampaignReady);
        Assert.Equal("NOT_SENT", sheet.Delivery);
        Assert.Equal("BASELINE_NOT_RECORDED", sheet.Regression);

        var similarity = HarborlightPixelSimilarity.ParseEvidence(File.ReadAllText(evidencePath));
        Assert.Equal(HarborlightPixelSimilarity.Measured, similarity.Status);
        Assert.Equal(HarborlightPixelSimilarity.NotJudged, similarity.Judgment);
        Assert.Equal(HarborlightPixelSimilarity.NotAssigned, similarity.VisualGrade);
        Assert.Equal(
            ["ACA-001-V1", "ACA-002-V1", "ACA-006-V1", "ACA-008-V1"],
            similarity.References.Select(item => item.ReferenceId).ToArray());
        foreach (var distance in similarity.References)
        {
            Assert.InRange(distance.MeanAbsoluteError, 0, 255);
            Assert.InRange(distance.AverageHashDistance, 0, 64);
            Assert.Equal(Sha256(Path.Combine(inbox, "inbox", distance.File)), distance.Sha256);
        }

        var trace = HarborlightWorkflow.ParseEvidence(File.ReadAllText(evidencePath));
        Assert.Equal(HarborlightWorkflow.Expected, trace);
        Assert.Equal("NOT_RUN", trace.Single(step => step.Step == "VISUAL_QA").Status);
        Assert.Equal("NOT_REQUESTED", trace.Single(step => step.Step == "HUMAN_REVIEW").Status);
        Assert.Equal("NOT_SENT", trace.Single(step => step.Step == "DELIVERY").Status);
        Assert.Equal("BASELINE_NOT_RECORDED", trace.Single(step => step.Step == "REGRESSION").Status);
    }

    [Fact]
    public void Review_images_open_only_the_stored_files()
    {
        var json = File.ReadAllText(EvidencePath());
        var images = HarborlightReviewImages.ParseEvidence(json);
        Assert.Equal(
            ["original", "adapted", "ACA-001-V1", "ACA-002-V1", "ACA-006-V1", "ACA-008-V1"],
            images.Select(item => item.Role).ToArray());
        Assert.All(images, item => Assert.Contains("not", item.Notice, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(HarborlightReviewImages.OriginalNotice, images[0].Notice);
        Assert.Equal(HarborlightReviewImages.AdaptedNotice, images[1].Notice);
        Assert.All(images.Skip(2), item => Assert.Equal(HarborlightReviewImages.ReferenceNotice, item.Notice));

        var root = RepositoryRoot();
        var original = HarborlightReviewImages.OpenVerified(json, root, "original");
        var adapted = HarborlightReviewImages.OpenVerified(json, root, "adapted");
        var reference = HarborlightReviewImages.OpenVerified(json, root, "ACA-001-V1");
        Assert.Equal(Sha256(original), images[0].Sha256);
        Assert.Equal(Sha256(adapted), images[1].Sha256);
        Assert.Equal(Sha256(reference), images[2].Sha256);
        Assert.Equal("image/jpeg", HarborlightReviewImages.MediaType(original));
        Assert.Equal("image/png", HarborlightReviewImages.MediaType(adapted));

        var unknown = Assert.Throws<InvalidOperationException>(() =>
            HarborlightReviewImages.OpenVerified(json, root, "accepted"));
        Assert.Contains("not stored", unknown.Message, StringComparison.OrdinalIgnoreCase);

        var node = JsonNode.Parse(json)!;
        var mismatchHash = new string('a', 64);
        node["original"]!["sha256"] = mismatchHash;
        node["pixelSimilarity"]!["originalSha256"] = mismatchHash;
        var mismatch = Assert.Throws<InvalidOperationException>(() =>
            HarborlightReviewImages.OpenVerified(node.ToJsonString(), root, "original"));
        Assert.Contains("hash does not match", mismatch.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(json, File.ReadAllText(EvidencePath()));
    }

    [Fact]
    public void Failure_record_names_one_stored_attempt_and_refuses_an_invented_retry()
    {
        var json = File.ReadAllText(EvidencePath());
        var record = HarborlightFailures.ParseEvidence(json);
        Assert.Equal(HarborlightFailures.NoneStored, record.Status);
        Assert.Equal(1, record.StoredAttempts);
        Assert.Equal(0, record.StoredFailureFiles);
        Assert.Equal(HarborlightFailures.RetriesNotRun, record.Retries);
        Assert.Contains("none was invented", record.Notice, StringComparison.OrdinalIgnoreCase);

        var storedNames = Directory.GetFiles(Path.GetDirectoryName(EvidencePath())!)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["HV-001-adapted-preview.png", "HV-001-evidence.json", "HV-001-original.jpg"],
            storedNames);

        var node = JsonNode.Parse(json)!;
        node["failuresAndRetries"]!["retries"] = "SUCCEEDED";
        var error = Assert.Throws<InvalidOperationException>(() => HarborlightFailures.ParseEvidence(node.ToJsonString()));
        Assert.Contains("None was invented", error.Message, StringComparison.Ordinal);
        Assert.All(
            HarborlightReview.ParseEvidence(json).Attributes,
            item => Assert.Equal(ReferenceLibrary.Unclassified, item.Grade));
    }

    [Fact]
    public void Owner_grade_gate_applies_only_supplied_attributes()
    {
        var json = File.ReadAllText(EvidencePath());
        var prepared = HarborlightReview.ParseEvidence(json);
        var empty = HarborlightReview.ApplyOwnerGrades(prepared, []);
        Assert.Equal(HarborlightReview.Prepared, empty.Status);
        Assert.Equal(prepared.Notice, empty.Notice);
        Assert.All(empty.Attributes, item => Assert.Equal(ReferenceLibrary.Unclassified, item.Grade));

        var applied = HarborlightReview.ApplyOwnerGrades(
            prepared,
            [new QualityAttribute("color power", "STRONG")]);
        Assert.Equal(HarborlightReview.OwnerEntryApplied, applied.Status);
        Assert.Equal("STRONG", applied.Attributes.Single(item => item.Name == "color power").Grade);
        Assert.Equal(15, applied.Attributes.Count(item => item.Grade == ReferenceLibrary.Unclassified));
        Assert.Equal(HarborlightReview.NotRequested, applied.HumanReview);
        Assert.False(applied.CampaignReady);
        Assert.Equal("NOT_SENT", applied.Delivery);
        Assert.Equal("BASELINE_NOT_RECORDED", applied.Regression);

        Assert.Contains(
            "UNCLASSIFIED",
            Assert.Throws<InvalidOperationException>(() =>
                HarborlightReview.ApplyOwnerGrades(prepared, [new QualityAttribute("color power", "UNCLASSIFIED")])).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "unknown",
            Assert.Throws<InvalidOperationException>(() =>
                HarborlightReview.ApplyOwnerGrades(prepared, [new QualityAttribute("invented attribute", "STRONG")])).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "twice",
            Assert.Throws<InvalidOperationException>(() =>
                HarborlightReview.ApplyOwnerGrades(prepared, [
                    new QualityAttribute("color power", "STRONG"),
                    new QualityAttribute("color power", "SUPPORTING")
                ])).Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "REFERENCE_STRENGTH",
            Assert.Throws<InvalidOperationException>(() =>
                HarborlightReview.ApplyOwnerGrades(prepared, [new QualityAttribute("color power", "ACCEPTED")])).Message,
            StringComparison.Ordinal);

        var stored = HarborlightReview.ParseEvidence(File.ReadAllText(EvidencePath()));
        Assert.Equal(HarborlightReview.Prepared, stored.Status);
        Assert.All(stored.Attributes, item => Assert.Equal(ReferenceLibrary.Unclassified, item.Grade));
        Assert.Equal(json, File.ReadAllText(EvidencePath()));
    }

    [Fact]
    public void Workflow_trace_refuses_an_invented_acceptance()
    {
        var node = JsonNode.Parse(File.ReadAllText(EvidencePath()))!;
        var review = node["workflowTrace"]!.AsArray()
            .Single(step => step!["step"]!.GetValue<string>() == "HUMAN_REVIEW")!;
        review["status"] = "ACCEPTED";

        var error = Assert.Throws<InvalidOperationException>(() => HarborlightWorkflow.ParseEvidence(node.ToJsonString()));
        Assert.Contains("not invented", error.Message, StringComparison.Ordinal);
    }

    private static string EvidencePath()
    {
        return Path.Combine(
            RepositoryRoot(),
            "assets",
            "alpha-prototypes",
            "creative-academy",
            "harborlight",
            "HV-001-evidence.json");
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
