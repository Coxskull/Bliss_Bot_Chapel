using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class MarketplaceBalanceTests
{
    [Fact]
    public void Stored_advertiser_rows_ahead_of_stored_creator_rows_are_not_a_census()
    {
        var board = MarketplaceBalance.Read(
            ["Sunrise Wellness Co.", "Harbor Audio Labs"],
            ["Test Creator"]);

        Assert.Equal(2, board.AdvertiserCount);
        Assert.Equal(1, board.CreatorCount);
        Assert.False(board.CensusClaimed);
        Assert.False(board.GreenMeansSend);
        Assert.Equal("NOT_SENT", board.Delivery);
        Assert.Equal("Stored advertisers: 2. Stored creators: 1.", board.Counts);
        Assert.Equal("Stored advertiser pressure is ahead of stored creator pressure.", board.Pressure);
        Assert.Equal("Revenue is not recorded. None was invented.", board.Revenue);
        Assert.Equal("Inventory is not recorded. None was invented.", board.Inventory);
        Assert.Contains("not a market census", board.Notice);
        Assert.Contains("None was invented", board.Notice);
        Assert.Contains("Green does not send", board.Notice);
        Assert.Contains("NOT_SENT", board.Notice);
        Assert.Equal(["Harbor Audio Labs", "Sunrise Wellness Co."], board.Advertisers);
        Assert.Equal(["Test Creator"], board.Creators);
        Assert.DoesNotContain("$", board.Notice);
        Assert.DoesNotContain("$", board.Revenue);
    }

    [Fact]
    public void Equal_stored_counts_are_level()
    {
        var board = MarketplaceBalance.Read(["One Advertiser"], ["One Creator"]);
        Assert.Equal("Stored advertiser pressure and stored creator pressure are level.", board.Pressure);
        Assert.Equal("Stored advertisers: 1. Stored creators: 1.", board.Counts);
        Assert.False(board.CensusClaimed);
        Assert.Equal("NOT_SENT", board.Delivery);
    }

    [Fact]
    public void Stored_creator_rows_ahead_of_stored_advertiser_rows()
    {
        var board = MarketplaceBalance.Read(["One Advertiser"], ["Creator A", "Creator B"]);
        Assert.Equal(1, board.AdvertiserCount);
        Assert.Equal(2, board.CreatorCount);
        Assert.Equal("Stored creator pressure is ahead of stored advertiser pressure.", board.Pressure);
        Assert.Equal("Revenue is not recorded. None was invented.", board.Revenue);
        Assert.False(board.GreenMeansSend);
    }

    [Fact]
    public void A_missing_count_is_not_treated_as_zero()
    {
        var board = MarketplaceBalance.Read(null, ["Test Creator"]);
        Assert.Null(board.AdvertiserCount);
        Assert.Equal(1, board.CreatorCount);
        Assert.Equal("Stored advertisers: not recorded. Stored creators: 1.", board.Counts);
        Assert.Equal("The balance is not claimed. None was invented.", board.Pressure);
        Assert.Empty(board.Advertisers);
        Assert.False(board.CensusClaimed);
        Assert.Equal("NOT_SENT", board.Delivery);
    }

    [Fact]
    public void A_blank_stored_name_is_skipped()
    {
        var board = MarketplaceBalance.Read(["  ", "Harbor Audio Labs"], ["Test Creator"]);
        Assert.Equal(1, board.AdvertiserCount);
        Assert.Equal(["Harbor Audio Labs"], board.Advertisers);
        Assert.Equal("A blank stored name was skipped. None was invented.", board.Skipped);
        Assert.Equal("Stored advertiser pressure and stored creator pressure are level.", board.Pressure);
    }

    [Fact]
    public void Balance_source_does_not_invent_a_price_or_send()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "MarketplaceBalance.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("None was invented", source);
        Assert.Contains("not a market census", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
