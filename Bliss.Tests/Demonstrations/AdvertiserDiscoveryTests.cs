using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class AdvertiserDiscoveryTests
{
    [Fact]
    public void A_public_source_is_stored_once_and_a_duplicate_url_is_not_a_second_prospect()
    {
        var first = AdvertiserDiscovery.Decide(
            "Casa Verde",
            "https://example.com/casa-verde",
            [],
            true);
        Assert.True(first.Accepted);
        Assert.True(first.Written);
        Assert.False(first.Duplicate);
        Assert.Equal(AdvertiserDiscovery.ScoredState, first.State);
        Assert.False(first.GreenMeansSend);
        Assert.Equal("NOT_SENT", first.Delivery);
        Assert.Contains("crawler did not run", first.Notice);

        var stored = new StoredProspect(
            "casa-verde",
            "Casa Verde",
            "Panama City",
            "https://example.com/casa-verde/",
            100,
            AdvertiserDiscovery.ScoredState);
        var again = AdvertiserDiscovery.Decide(
            "Casa Verde Norte",
            "https://Example.com/casa-verde",
            [stored],
            true);
        Assert.True(again.Duplicate);
        Assert.False(again.Written);
        Assert.Equal("casa-verde", again.MatchedSlug);
        Assert.Contains("second prospect was not written", again.Notice);

        var sameName = AdvertiserDiscovery.Decide(
            "Casa Verde",
            "https://example.com/other-page",
            [stored],
            true);
        Assert.True(sameName.Duplicate);
        Assert.False(sameName.Written);
        Assert.Equal("casa-verde", sameName.MatchedSlug);
    }

    [Fact]
    public void A_named_public_source_below_100_is_preserved()
    {
        var decision = AdvertiserDiscovery.Decide(
            "Puerto Azul",
            "https://example.com/puerto-azul",
            [],
            false);
        Assert.True(decision.Written);
        Assert.Equal(AdvertiserDiscovery.PreservedState, decision.State);
        Assert.Contains("preserved", decision.Notice);
        Assert.Contains("No demonstration was manufactured", decision.Notice);
    }

    [Fact]
    public void A_blank_name_and_a_missing_source_are_refused()
    {
        var blank = AdvertiserDiscovery.Decide("  ", "https://example.com/blank", [], true);
        Assert.False(blank.Accepted);
        Assert.False(blank.Written);
        Assert.Equal(AdvertiserDiscovery.BlankName, blank.Notice);

        var symbols = AdvertiserDiscovery.Decide("!!!", "https://example.com/symbols", [], true);
        Assert.False(symbols.Accepted);
        Assert.Equal(AdvertiserDiscovery.UnusableName, symbols.Notice);

        var missing = AdvertiserDiscovery.Decide("Casa Verde", "not a url", [], true);
        Assert.False(missing.Accepted);
        Assert.Equal(AdvertiserDiscovery.MissingSource, missing.Notice);
    }

    [Fact]
    public void The_reading_counts_only_stored_public_sources()
    {
        var reading = AdvertiserDiscovery.Read(
        [
            new StoredProspect("casa-verde", "Casa Verde", "Panama City", "https://example.com/casa-verde", 100, "DEMONSTRATION_PREPARED"),
            new StoredProspect("puerto-azul", "Puerto Azul", "Quito", "https://example.com/puerto-azul", 75, AdvertiserDiscovery.PreservedState),
            new StoredProspect("abc-pharmacy", "ABC Pharmacy", "Panama City", "", 0, ""),
            new StoredProspect("", "  ", "Panama City", "https://example.com/nameless", 100, AdvertiserDiscovery.ScoredState)
        ]);

        Assert.False(reading.GreenMeansSend);
        Assert.False(reading.CensusClaimed);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Contains("not a census", reading.Notice);
        Assert.Contains("crawler did not run", reading.Notice);
        Assert.Equal(2, reading.Stored);
        Assert.Equal(1, reading.Scored);
        Assert.Equal(1, reading.Preserved);
        Assert.Equal("Casa Verde", reading.Prospects[0].BusinessName);
        Assert.Equal("https://example.com/casa-verde", reading.Prospects[0].SourceUrl);
        Assert.Equal("DEMONSTRATION_PREPARED", reading.Prospects[0].State);
        Assert.Equal("Puerto Azul", reading.Prospects[1].BusinessName);
        Assert.Equal(AdvertiserDiscovery.PreservedState, reading.Prospects[1].State);
        Assert.Contains(reading.Withheld, item => item.Reason == AdvertiserDiscovery.MissingSource && item.Count == 1);
        Assert.Contains(reading.Withheld, item => item.Reason == AdvertiserDiscovery.BlankName && item.Count == 1);
        Assert.DoesNotContain(reading.Prospects, item => item.BusinessName.Contains("250", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_list_is_refused()
    {
        var read = Assert.Throws<InvalidOperationException>(() => AdvertiserDiscovery.Read(null));
        Assert.Contains("None is invented", read.Message);
        var decide = Assert.Throws<InvalidOperationException>(() => AdvertiserDiscovery.Decide("Casa Verde", "https://example.com/casa-verde", null, true));
        Assert.Contains("None is invented", decide.Message);
    }

    [Fact]
    public void Discovery_source_does_not_crawl_or_send()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "AdvertiserDiscovery.cs"));
        Assert.Contains("not a census", source);
        Assert.Contains("crawler did not run", source);
        Assert.DoesNotContain("250,000", source);
        Assert.DoesNotContain("250000", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
