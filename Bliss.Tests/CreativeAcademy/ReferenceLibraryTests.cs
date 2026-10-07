using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

public sealed class ReferenceLibraryTests
{
    [Fact]
    public void A_manifest_without_files_stays_awaiting_upload_and_is_not_retrieved()
    {
        const string manifest = """
            referenceId	nicheNumber	nicheKey	nicheName	expectedFile	lifecycle	status	learn	doNotCopy
            ACA-011-V1	11	auto-parts	Auto-Parts Store	ACA-011-V1.png	CANDIDATE	AWAITING_UPLOAD		
            """;

        var library = ReferenceLibrary.ParseManifest(manifest, _ => false);
        var retrieval = ReferenceLibrary.Select("auto-parts", library, ["lighting"]);

        Assert.Single(library);
        Assert.Equal(ReferenceLibrary.AwaitingUpload, library[0].UploadStatus);
        Assert.False(library[0].AssetPresent);
        Assert.Equal("NICHE_REFERENCE_NOT_ACTIVE", retrieval.Status);
        Assert.Empty(retrieval.Selected);
        Assert.Equal(0, retrieval.ModelCalls);
        Assert.Equal("NOT_SENT", retrieval.Delivery);
        Assert.Equal(16, ReferenceLibrary.DoNotProduce.Count);
        Assert.Equal(12, ReferenceLibrary.RegressionBriefs.Count);
        Assert.Contains("Premium Dominant Presence", ReferenceLibrary.QualityDna);
    }

    [Fact]
    public void An_uploaded_jpeg_stays_candidate_and_is_not_retrieved()
    {
        const string manifest = """
            referenceId	nicheNumber	nicheKey	nicheName	expectedFile	lifecycle	status	learn	doNotCopy	mappingNote
            ACA-001-V1	1	pharmacy	Pharmacy	Pharmacy.jpeg	CANDIDATE	UPLOADED			Owner file kept.
            """;

        var library = ReferenceLibrary.ParseManifest(manifest, name => name == "Pharmacy.jpeg");
        var retrieval = ReferenceLibrary.Select("pharmacy", library, ["lighting"]);

        Assert.True(library[0].AssetPresent);
        Assert.Equal(ReferenceLibrary.Uploaded, library[0].UploadStatus);
        Assert.Equal(ReferenceLibrary.Candidate, library[0].Lifecycle);
        Assert.Equal("Pharmacy.jpeg", library[0].ExpectedFile);
        Assert.Equal(string.Empty, library[0].Learn);
        Assert.Equal(string.Empty, library[0].DoNotCopy);
        Assert.Equal("Owner file kept.", library[0].MappingNote);
        Assert.Equal("NICHE_REFERENCE_NOT_ACTIVE", retrieval.Status);
        Assert.Empty(retrieval.Selected);
    }

    [Fact]
    public void Retrieval_selects_only_active_files_and_records_why()
    {
        var library = new List<AcademyReferenceRecord>
        {
            new("ACA-011-V1", 11, "auto-parts", "Auto-Parts Store", "ACA-011-V1.png", "ACTIVE", "UPLOADED", true, "product realism", "exact counter"),
            new("ACA-008-V1", 8, "automotive", "Automotive Service", "ACA-008-V1.png", "ACTIVE", "UPLOADED", true, "lighting craftsmanship", "exact bay"),
            new("ACA-001-V1", 1, "pharmacy", "Pharmacy", "ACA-001-V1.png", "ACTIVE", "UPLOADED", true, "typography", "VidaCare"),
            new("ACA-016-V1", 16, "bakery", "Bakery", "ACA-016-V1.png", "CANDIDATE", "UPLOADED", true, "lighting craftsmanship", "Maison Fleur")
        };

        var retrieval = ReferenceLibrary.Select("auto-parts", library, ["lighting craftsmanship", "typography"]);

        Assert.Equal("RETRIEVED", retrieval.Status);
        Assert.Equal(3, retrieval.Selected.Count);
        Assert.Equal("niche match", retrieval.Selected[0].Reason);
        Assert.Equal("lighting craftsmanship", retrieval.Selected[1].Reason);
        Assert.Equal("typography", retrieval.Selected[2].Reason);
        Assert.DoesNotContain(retrieval.Selected, item => item.ReferenceId == "ACA-016-V1");
    }

    [Fact]
    public void A_do_not_copy_identity_fails_originality()
    {
        var library = new List<AcademyReferenceRecord>
        {
            new("ACA-001-V1", 1, "pharmacy", "Pharmacy", "ACA-001-V1.png", "ACTIVE", "UPLOADED", true, "color power", "VidaCare")
        };

        var copied = ReferenceLibrary.Judge("VidaCare Pharmacy", "Pickup today", library);
        var original = ReferenceLibrary.Judge("Norte Salud", "Retira tu receta", library);

        Assert.Equal("REFERENCE_TOO_SIMILAR", copied.Status);
        Assert.Equal("PASS", original.Status);
        Assert.False(original.CampaignReady);
        Assert.Equal("NOT_SENT", original.Delivery);
    }
}
