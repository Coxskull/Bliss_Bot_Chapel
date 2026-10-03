using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class FlowControlTests
{
    [Fact]
    public void Excess_legitimate_prospects_stay_queued_and_none_are_discarded()
    {
        var board = FlowControl.Regulate(
            [
                new("puerto-azul", "Puerto Azul", true),
                new("mesa-norte", "Mesa Norte", false),
                new("casa-verde", "Casa Verde", false),
                new("abc-pharmacy", "ABC Pharmacy", false)
            ],
            1);

        Assert.Equal(4, board.Preserved);
        Assert.Equal(1, board.Released);
        Assert.Equal(2, board.Held);
        Assert.Equal(1, board.Withheld);
        Assert.Equal(0, board.Discarded);
        Assert.Equal("ABC Pharmacy", board.Assignments.Single(item => item.Lane == FlowControl.Released).BusinessName);
        Assert.Contains(board.Assignments, item => item.BusinessName == "Mesa Norte" && item.Lane == FlowControl.Held);
        Assert.Contains(board.Assignments, item => item.BusinessName == "Puerto Azul" && item.Lane == FlowControl.Withheld);
        Assert.Contains("None was discarded", board.Notice);
        Assert.Contains("Green does not send", board.Notice);
        Assert.Contains("NOT_SENT", board.Notice);
        Assert.Contains("not an Economics price", board.CapacityNotice);
        Assert.False(board.GreenMeansSend);
        Assert.DoesNotMatch(@"\d", board.Notice);
    }

    [Fact]
    public void A_larger_opening_count_does_not_invent_prospects()
    {
        var board = FlowControl.Regulate(
            [new("mesa-norte", "Mesa Norte", false)],
            100);

        Assert.Equal(1, board.Preserved);
        Assert.Equal(1, board.Released);
        Assert.Equal(0, board.Held);
        Assert.Equal(0, board.Discarded);
        Assert.Single(board.Assignments);
    }

    [Fact]
    public void A_missing_capacity_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            FlowControl.Regulate([new("mesa-norte", "Mesa Norte", false)], null));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Flow_source_does_not_send()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "FlowControl.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("None was discarded", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
