using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class AcquisitionEventTests
{
    [Fact]
    public void A_page_open_is_recorded_without_naming_a_watcher()
    {
        var result = AcquisitionEvents.Accept("PAGE_OPENED", null);

        Assert.True(result.Accepted);
        Assert.Equal("PAGE_OPENED", result.Kind);
        Assert.Contains("not named", result.Observation);
        Assert.Equal(0, result.AiCalls);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void An_opinion_is_not_an_event()
    {
        var result = AcquisitionEvents.Accept("INTERESTED", null);

        Assert.False(result.Accepted);
        Assert.Equal(AcquisitionEvents.OpinionRefusal, result.Error);
        Assert.Equal(0, result.AiCalls);
    }

    [Fact]
    public void A_delivery_claim_is_not_recorded()
    {
        var result = AcquisitionEvents.Accept("SENT", null);

        Assert.False(result.Accepted);
        Assert.Equal(AcquisitionEvents.DeliveryRefusal, result.Error);
    }

    [Fact]
    public void A_named_watcher_is_refused()
    {
        var result = AcquisitionEvents.Accept("PAGE_OPENED", "Ana Ruiz");

        Assert.False(result.Accepted);
        Assert.Equal(AcquisitionEvents.WatcherRefusal, result.Error);
    }

    [Fact]
    public void A_blank_kind_asks_for_the_catalog()
    {
        var result = AcquisitionEvents.Accept("  ", null);

        Assert.False(result.Accepted);
        Assert.Equal(AcquisitionEvents.CatalogRefusal, result.Error);
    }
}
