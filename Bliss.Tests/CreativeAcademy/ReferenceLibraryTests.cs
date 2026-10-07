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
    public void Retrieval_returns_stored_notes_and_keeps_a_superseded_reference_readable()
    {
        var library = new List<AcademyReferenceRecord>
        {
            new("ACA-011-V1", 11, "auto-parts", "Auto-Parts Store", "Auto Parts.jpeg", "SUPERSEDED", "UPLOADED", true, "counter lighting", "Old Counter"),
            new("ACA-011-V2", 11, "auto-parts", "Auto-Parts Store", "Auto Parts.jpeg", "ACTIVE", "UPLOADED", true, "product realism", "Exact bay")
        };

        var retrieval = ReferenceLibrary.Select("auto-parts", library, ["counter lighting"]);

        Assert.Equal(2, library.Count);
        Assert.Equal(ReferenceLibrary.Superseded, library[0].Lifecycle);
        Assert.Equal("RETRIEVED", retrieval.Status);
        Assert.Single(retrieval.Selected);
        Assert.Equal("ACA-011-V2", retrieval.Selected[0].ReferenceId);
        Assert.Equal("product realism", retrieval.Selected[0].Learn);
        Assert.Equal("Exact bay", retrieval.Selected[0].DoNotCopy);
        Assert.Equal(ReferenceLibrary.Unclassified, library[0].QualityStatus);
        Assert.All(ReferenceLibrary.UnclassifiedAttributes(), item => Assert.Equal(ReferenceLibrary.Unclassified, item.Grade));
        Assert.Contains(ReferenceLibrary.HumanReview, ReferenceLibrary.Lifecycles);
    }

    [Fact]
    public void Owner_needs_keep_a_blank_entry_blank()
    {
        const string file = """
            needId	status	ownerEntry	need
            ACA-005-FILE	OPEN		Upload the used-car prototype.
            BIND-004	SUPPLIED	Owner confirmed the new-car file	Filename binding.
            MYSTERY	MAYBE		This status is not a supplied answer.
            """;

        var needs = OwnerNeeds.Parse(file);

        Assert.Equal(3, needs.Count);
        Assert.Equal(OwnerNeeds.Open, needs[0].Status);
        Assert.Equal(string.Empty, needs[0].OwnerEntry);
        Assert.Equal(OwnerNeeds.Supplied, needs[1].Status);
        Assert.Equal("Owner confirmed the new-car file", needs[1].OwnerEntry);
        Assert.Equal(OwnerNeeds.Open, needs[2].Status);
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
