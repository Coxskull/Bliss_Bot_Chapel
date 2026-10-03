using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class RotationAbundanceTests
{
    [Fact]
    public void Four_stored_advertisers_leave_two_slots_open_in_a_pair_of_six()
    {
        var board = RotationAbundance.Read(6, Advertisers(), true, 8);
        Assert.Equal(6, board.TheoreticalSlots);
        Assert.Equal(4, board.PlacedAdvertisers);
        Assert.Equal(2, board.OpenSlots);
        Assert.True(board.Accepted);
        Assert.False(board.SlotsChanged);
        Assert.False(board.GreenMeansSend);
        Assert.Equal("NOT_SENT", board.Delivery);
        Assert.Equal(8, board.SlotCount);
        Assert.Equal("Theoretical slots: 6. Placed advertisers: 4. Open slots: 2.", board.Counts);
        Assert.Equal("The rotation is accepted. Open slots remain. Creator approval governs the rotation.", board.Result);
        Assert.Equal("One advertiser is not required for every theoretical slot. None was invented.", board.Abundance);
        Assert.Equal("This pass is one rotation. A later period is not configured. None was invented.", board.Period);
        Assert.Contains("not required for every slot", board.Notice);
        Assert.Contains("Green does not send", board.Notice);
        Assert.Contains("NOT_SENT", board.Notice);
        Assert.Equal(Advertisers().OrderBy(name => name, StringComparer.Ordinal).ToArray(), board.Advertisers);
        Assert.DoesNotContain("Invented Advertiser", board.Advertisers);
        Assert.DoesNotContain("$", board.Notice);
        Assert.DoesNotContain("$", board.Abundance);
    }

    [Fact]
    public void A_full_pair_still_allows_open_slots()
    {
        var board = RotationAbundance.Read(4, Advertisers(), true, 8);
        Assert.Equal(0, board.OpenSlots);
        Assert.True(board.Accepted);
        Assert.Equal("The rotation is accepted. Open slots were allowed. Creator approval governs the rotation.", board.Result);
        Assert.Contains("not required for every theoretical slot", board.Abundance);
        Assert.Equal("NOT_SENT", board.Delivery);
    }

    [Fact]
    public void Creator_refusal_withholds_the_rotation_and_keeps_the_open_slots()
    {
        var board = RotationAbundance.Read(6, Advertisers(), false, 8);
        Assert.False(board.Accepted);
        Assert.Equal(2, board.OpenSlots);
        Assert.Equal("The rotation stays withheld. Creator approval governs the rotation.", board.Result);
        Assert.Equal(4, board.Advertisers.Count);
        Assert.False(board.SlotsChanged);
        Assert.Equal("NOT_SENT", board.Delivery);
    }

    [Fact]
    public void A_missing_list_is_not_treated_as_zero_advertisers()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RotationAbundance.Read(6, null, true, 8));
        Assert.Contains("None is invented", error.Message);
        Assert.Contains("Stored advertisers are required", error.Message);
    }

    [Fact]
    public void Extra_advertisers_and_a_missing_approval_are_refused()
    {
        var overflow = Assert.Throws<InvalidOperationException>(() =>
            RotationAbundance.Read(2, Advertisers(), true, 8));
        Assert.Contains("None is invented", overflow.Message);
        var approval = Assert.Throws<InvalidOperationException>(() =>
            RotationAbundance.Read(6, Advertisers(), null, 8));
        Assert.Contains("Creator approval is required", approval.Message);
        var pair = Assert.Throws<InvalidOperationException>(() =>
            RotationAbundance.Read(8, Advertisers(), true, 8));
        Assert.Contains("2, 4, or 6", pair.Message);
    }

    [Fact]
    public void A_blank_name_is_skipped_and_a_repeat_is_kept_once()
    {
        var board = RotationAbundance.Read(6, ["Harbor Audio Labs", " ", "Harbor Audio Labs", "Sunrise Wellness Co."], true, 8);
        Assert.Equal(2, board.PlacedAdvertisers);
        Assert.Equal(4, board.OpenSlots);
        Assert.Equal(["Harbor Audio Labs", "Sunrise Wellness Co."], board.Advertisers);
        Assert.Contains("blank stored name was skipped", board.Skipped);
        Assert.Contains("kept once", board.Skipped);
        Assert.Contains("None was invented", board.Skipped);
    }

    [Fact]
    public void Preview_does_not_claim_a_rotation()
    {
        var board = RotationAbundance.Preview(Advertisers(), 8);
        Assert.Equal("A rotation has not been read.", board.Result);
        Assert.Null(board.TheoreticalSlots);
        Assert.Null(board.OpenSlots);
        Assert.Equal(4, board.PlacedAdvertisers);
        Assert.Equal("Stored advertisers: 4. Stored slots: 8.", board.Counts);
        Assert.False(board.Accepted);
        Assert.False(board.SlotsChanged);
        Assert.Equal("NOT_SENT", board.Delivery);
    }

    [Fact]
    public void Rotation_source_does_not_send_or_invent_a_price()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "RotationAbundance.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("not required for every slot", source);
        Assert.Contains("None is invented", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }

    private static string[] Advertisers() =>
    [
        "TEST Restaurant Santo Domingo",
        "Harbor Audio Labs",
        "TEST Dental Manila",
        "Sunrise Wellness Co."
    ];
}
