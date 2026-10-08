using Bliss.Domain.MissionControl;

namespace Bliss.Tests.MissionControl;

public sealed class EvidenceIdentityTests
{
    [Fact]
    public void An_id_uses_the_date_and_a_random_suffix()
    {
        var utc = new DateTime(2026, 10, 7, 23, 0, 0, DateTimeKind.Utc);
        var first = EvidenceIdentity.Format(utc, [1, 2, 3, 4, 5, 6]);
        var second = EvidenceIdentity.Format(utc, [9, 8, 7, 6, 5, 4]);
        Assert.StartsWith("ALPHA-EV-20261007-", first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.True(EvidenceIdentity.IsWellFormed(first));
        Assert.Throws<InvalidOperationException>(() => EvidenceIdentity.Format(utc, [1, 2, 3]));
    }

    [Fact]
    public void A_secret_and_a_self_grade_are_refused()
    {
        Assert.Throws<InvalidOperationException>(() => EvidenceIdentity.RequireSafe("password=secret", "note"));
        var manifest = Sample();
        Assert.Throws<InvalidOperationException>(() =>
            EvidenceIdentity.Review(manifest, manifest.SubmittedBy, "REVIEWER", "FAIL", DateTime.UtcNow));
        Assert.Throws<InvalidOperationException>(() =>
            EvidenceIdentity.Review(manifest with { OwnerDecisionRequired = true }, "ChatGPT", "REVIEWER", "PASS", DateTime.UtcNow));
        var reviewed = EvidenceIdentity.Review(
            manifest with { Files = [new EvidenceFileRef(manifest.EvidenceId + "-REPORT.pdf", "REPORT", new string('a', 64))] },
            "ChatGPT",
            "REVIEWER",
            "BLOCKED",
            DateTime.UtcNow);
        Assert.Equal(manifest.EvidenceId, reviewed.EvidenceId);
        Assert.Equal("BLOCKED", reviewed.FinalReviewResult);
    }

    [Fact]
    public void Random_selection_stays_inside_the_existing_catalog()
    {
        var selected = EvidenceIdentity.SelectValidation(3);
        Assert.Contains(EvidenceIdentity.Catalog, item => item.Key == selected.Key);
        Assert.Equal(selected.Key, EvidenceIdentity.SelectValidation(3).Key);
        Assert.True(EvidenceIdentity.RequiresOwnerDecision("economics"));
        Assert.False(EvidenceIdentity.RequiresOwnerDecision("evidence-retrieval"));
    }

    private static EvidenceManifest Sample() => new(
        "ALPHA-EV-20261007-H7K4Q9",
        "MC-EVIDENCE-ID",
        "Mission Control retrieval probe",
        "evidence-retrieval",
        DateTime.UtcNow,
        null,
        "Erwin",
        "Development",
        "UNRECORDED",
        "UNRECORDED",
        [],
        EvidenceIdentity.NotReady,
        string.Empty,
        string.Empty,
        EvidenceIdentity.Unrecorded,
        false,
        EvidenceIdentity.NotReviewed,
        string.Empty,
        null,
        EvidenceIdentity.NotReviewed,
        null,
        string.Empty,
        EvidenceIdentity.NotConnected,
        EvidenceIdentity.SubmittedFolder + "/ALPHA-EV-20261007-H7K4Q9");
}
