using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class SourceMediaRulesTests
{
    [Fact]
    public void Only_iso_mp4_signatures_may_reach_native_decoders()
    {
        var ftyp = new byte[] { 0, 0, 0, 20, 0x66, 0x74, 0x79, 0x70,
                                0x69, 0x73, 0x6f, 0x6d, 0, 0, 0, 0,
                                0x69, 0x73, 0x6f, 0x6d };
        Assert.True(SourceMediaRules.IsMp4Upload(ftyp, "valid.mp4"));
        Assert.True(SourceMediaRules.IsMp4Upload(ftyp, "VALID.MP4"));
        Assert.False(SourceMediaRules.IsMp4Upload(ftyp, "playlist.m3u8"));
        Assert.False(SourceMediaRules.IsMp4Upload(ftyp, "source.mov"));
        Assert.False(SourceMediaRules.IsMp4Upload(ftyp, null));
        Assert.False(SourceMediaRules.IsMp4Upload(ftyp, ""));
        Assert.False(SourceMediaRules.IsMp4Upload(ftyp[..9], "incomplete.mp4"));
        var playlist = System.Text.Encoding.UTF8.GetBytes("#EXTM3U\n#EXTINF:2\nasync:rist://example.invalid:7777\n");
        Assert.False(SourceMediaRules.IsMp4Upload(playlist, "forged.mp4"));
        var oversizedBox = ftyp.ToArray();
        oversizedBox[3] = 200;
        Assert.False(SourceMediaRules.IsMp4Upload(oversizedBox, "oversized.mp4"));
    }

    [Fact]
    public void Renamed_exact_duplicate_earns_no_quota_credit()
    {
        var status = SourceMediaRules.Classify(exactDuplicate: true, frameDistance: 0, usableDuration: true);
        Assert.Equal(SourceMediaStatus.Duplicate, status);
        Assert.Equal(0, SourceMediaRules.QuotaCredit(status));
    }

    [Fact]
    public void Near_duplicate_frame_earns_no_quota_credit()
    {
        var status = SourceMediaRules.Classify(exactDuplicate: false, frameDistance: 3, usableDuration: true);
        Assert.Equal(SourceMediaStatus.NearDuplicate, status);
        Assert.Equal(0, SourceMediaRules.QuotaCredit(status));
    }

    [Fact]
    public void Usable_unique_slice_earns_one_credit()
    {
        var status = SourceMediaRules.Classify(exactDuplicate: false, frameDistance: int.MaxValue, usableDuration: true);
        Assert.Equal(SourceMediaStatus.Qualified, status);
        Assert.Equal(1, SourceMediaRules.QuotaCredit(status));
    }

    [Fact]
    public void Short_clip_is_rejected_and_creates_replacement_pressure()
    {
        var gauge = MediaFuelGauge.From(
        [
            (SourceMediaStatus.Qualified, 1),
            (SourceMediaStatus.Duplicate, 0),
            (SourceMediaStatus.Rejected, 0)
        ]);
        Assert.Equal(1, gauge.QualifiedUnique);
        Assert.Equal(2, gauge.ReplacementRequired);
        Assert.Equal(19, gauge.DailyRemaining);
        Assert.Equal("SHORTAGE", gauge.FuelStatus);
    }

    [Fact]
    public void Pharmacy_buying_roles_include_a_marketing_manager()
    {
        var roles = BuyingRoleCatalog.RolesFor("pharmacy");
        Assert.Contains("Marketing manager", roles);
        Assert.Contains("Owner", roles);
    }

    [Fact]
    public void Unsupported_name_is_not_treated_as_a_verified_person()
    {
        var facts = new ProspectFacts(
            "ABC Pharmacy",
            "TIER_4",
            "No verified mailbox is on file.",
            BuyingRoleCatalog.RolesFor("pharmacy"),
            ["Family Health Concept"]);
        var turn = DemonstrationConversation.Reply(facts, "Is Maria González the marketing director?");
        Assert.DoesNotContain("Maria González is", turn.Reply);
        Assert.Contains("not verified", turn.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.False(turn.HumanEscalation);
    }

    [Fact]
    public void Pricing_question_does_not_invent_a_price()
    {
        var facts = new ProspectFacts("ABC Pharmacy", "TIER_4", "none", ["Owner"], ["Family Health Concept"]);
        var turn = DemonstrationConversation.Reply(facts, "How much does this cost?");
        Assert.Contains("cannot invent a price", turn.Reply);
        Assert.Equal("PRICING_QUESTION", turn.Signal);
    }

    [Fact]
    public void Human_request_escalates_and_does_not_mark_a_win()
    {
        var facts = new ProspectFacts("ABC Pharmacy", "TIER_4", "none", ["Owner"], ["Family Health Concept"]);
        var turn = DemonstrationConversation.Reply(facts, "Please have a human call me.");
        Assert.True(turn.HumanEscalation);
        Assert.Contains("not a win", turn.Reply);
    }
}
