using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class CreativeInventoryTests
{
    [Fact]
    public void A_pair_of_four_with_creator_approval_is_accepted()
    {
        var decision = CreativeInventory.Decide(4, true, false);
        Assert.True(decision.Accepted);
        Assert.False(decision.StackRefused);
        Assert.False(decision.SlotsChanged);
        Assert.False(decision.GreenMeansSend);
        Assert.Equal("NOT_SENT", decision.Delivery);
        Assert.Equal(4, decision.Pair);
        Assert.Equal("The balanced pair of 4 is accepted. Creator approval governs this density.", decision.Result);
        Assert.Contains("A two-over-four stack is not the default", decision.Notice);
        Assert.Contains("Rotation is not configured", decision.Notice);
        Assert.Contains("Green does not send", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
        Assert.DoesNotContain("$", decision.Notice);
        Assert.DoesNotContain("$", decision.Result);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void The_other_balanced_pairs_are_accepted_with_approval(int pair)
    {
        var decision = CreativeInventory.Decide(pair, true, false);
        Assert.True(decision.Accepted);
        Assert.Contains("The balanced pair of " + pair + " is accepted", decision.Result);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Fact]
    public void Creator_refusal_withholds_a_balanced_pair()
    {
        var decision = CreativeInventory.Decide(4, false, false);
        Assert.False(decision.Accepted);
        Assert.Equal("The balanced pair of 4 stays withheld. Creator approval governs this density.", decision.Result);
        Assert.False(decision.SlotsChanged);
        Assert.Equal("NOT_SENT", decision.Delivery);
        Assert.False(decision.GreenMeansSend);
    }

    [Fact]
    public void A_two_over_four_stack_is_refused_even_with_approval()
    {
        var decision = CreativeInventory.Decide(6, true, true);
        Assert.False(decision.Accepted);
        Assert.True(decision.StackRefused);
        Assert.Null(decision.Pair);
        Assert.Equal("The two-over-four stack is refused. It is not the default. None was invented.", decision.Result);
        Assert.False(decision.SlotsChanged);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Fact]
    public void Eight_slots_are_not_a_balanced_pair()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CreativeInventory.Decide(8, true, false));
        Assert.Contains("None is invented", error.Message);
        var stored = CreativeInventory.DescribeStored(8);
        Assert.Contains("Stored slots: 8", stored);
        Assert.Contains("not a balanced pair", stored);
        Assert.Contains("None was invented", stored);
    }

    [Fact]
    public void A_missing_approval_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => CreativeInventory.Decide(4, null, false));
        Assert.Contains("Creator approval is required", error.Message);
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Stored_rows_keep_their_titles_and_do_not_become_a_pair()
    {
        var board = CreativeInventory.ReadStored(
        [
            new StoredSlot("Chapel Studio Diary 1", "PRE_ROLL"),
            new StoredSlot("Chapel Conversations Episode 1", "MID_ROLL"),
            new StoredSlot("Chapel Conversations Episode 1", "PRE_ROLL"),
            new StoredSlot("  ", "POST_ROLL")
        ]);
        Assert.Equal(4, board.SlotCount);
        Assert.False(board.SlotsChanged);
        Assert.Contains("not a balanced pair", board.Stored);
        Assert.Equal(CreativeInventory.Rotation, board.Rotation);
        Assert.Contains("Chapel Conversations Episode 1 keeps MID_ROLL, PRE_ROLL.", board.Contents.Select(item => item.Line));
        Assert.Contains("A blank stored title was skipped. None was invented.", board.Contents.Select(item => item.Line));
        Assert.Equal("NOT_SENT", board.Delivery);
        Assert.False(board.GreenMeansSend);
    }

    [Fact]
    public void Inventory_source_does_not_send_or_invent_a_price()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "CreativeInventory.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("A two-over-four stack is not the default", source);
        Assert.Contains("None is invented", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
