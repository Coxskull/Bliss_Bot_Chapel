using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.CreativeAcademy;

public sealed class GlobalVisualDnaGateTests
{
    [Fact]
    public void A_high_score_cannot_override_a_hard_visual_failure()
    {
        var decision = GlobalVisualDnaGate.Evaluate(
            100,
            ["BRAND_DNA_VIOLATION"],
            true,
            false,
            true,
            true,
            true);

        Assert.Equal(GlobalVisualDnaGate.Fail, decision.Status);
        Assert.Contains("BRAND_DNA_VIOLATION", decision.BlockingDefects);
        Assert.False(decision.CampaignReady);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Fact]
    public void Missing_visual_evidence_withholds_approval_even_at_a_perfect_score()
    {
        var decision = GlobalVisualDnaGate.Evaluate(
            100,
            [],
            false,
            true,
            true,
            true,
            true);

        Assert.Equal(GlobalVisualDnaGate.Withheld, decision.Status);
        Assert.False(decision.CampaignReady);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void A_failed_required_release_check_blocks_delivery(
        bool brandDnaCompliant,
        bool originalityConfirmed,
        bool inventoryGeometryVerified,
        bool qrVerified)
    {
        var decision = GlobalVisualDnaGate.Evaluate(
            99,
            [],
            true,
            brandDnaCompliant,
            originalityConfirmed,
            inventoryGeometryVerified,
            qrVerified);

        Assert.Equal(GlobalVisualDnaGate.Fail, decision.Status);
        Assert.False(decision.CampaignReady);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Fact]
    public void A_known_noncritical_defect_requires_revision_even_with_a_high_score()
    {
        var decision = GlobalVisualDnaGate.Evaluate(
            98,
            ["WEAK_SCREEN_IMPACT"],
            true,
            true,
            true,
            true,
            true);

        Assert.Equal(GlobalVisualDnaGate.Revise, decision.Status);
        Assert.Contains("WEAK_SCREEN_IMPACT", decision.BlockingDefects);
        Assert.False(decision.CampaignReady);
    }

    [Fact]
    public void Only_a_complete_clean_record_passes_and_still_does_not_send()
    {
        var decision = GlobalVisualDnaGate.Evaluate(
            100,
            [],
            true,
            true,
            true,
            true,
            true);

        Assert.Equal(GlobalVisualDnaGate.Pass, decision.Status);
        Assert.False(decision.CampaignReady);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Fact]
    public void Unknown_defect_codes_are_rejected_instead_of_silently_accepted()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            GlobalVisualDnaGate.Evaluate(100, ["MADE_UP_DEFECT"], true, true, true, true, true));

        Assert.Contains("Unknown visual defect code", error.Message);
    }

    [Fact]
    public void Scores_outside_the_recorded_range_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GlobalVisualDnaGate.Evaluate(101, [], true, true, true, true, true));
    }
}
