using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class SourceMediaCoverageTests
{
    [Fact]
    public void A_fingerprinted_slice_covers_only_its_stored_market()
    {
        var reading = SourceMediaCoverage.Read(
        [
            Slice("Panama City", "Panama", SourceMediaStatus.Qualified, 1),
            Slice("Panama City", "Panama", SourceMediaStatus.Duplicate, 0)
        ]);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Contains("not a census", reading.Notice);
        Assert.Contains("None was invented", reading.Notice);
        var market = Assert.Single(reading.Markets);
        Assert.Equal("Panama City", market.Market);
        Assert.Equal("Panama", market.CountryLine);
        Assert.Equal(1, market.QualifiedSlices);
        Assert.Contains("stored rows", market.Notice);
        Assert.Equal(1, reading.Fuel.QualifiedUnique);
        Assert.Equal(1, reading.Fuel.Duplicates);
        Assert.Equal(1, reading.Fuel.ReplacementRequired);
        Assert.Empty(reading.Withheld);
    }

    [Fact]
    public void A_missing_fingerprint_is_not_counted_as_coverage()
    {
        var slice = Slice("Quito", "Ecuador", SourceMediaStatus.Qualified, 1) with { FrameHash = "" };
        var reading = SourceMediaCoverage.Read([slice]);

        Assert.Empty(reading.Markets);
        var held = Assert.Single(reading.Withheld);
        Assert.Equal(SourceMediaCoverage.MissingFingerprint, held.Reason);
        Assert.Equal(1, held.Count);
        Assert.Equal(1, reading.Fuel.QualifiedUnique);
    }

    [Fact]
    public void A_blank_market_is_not_invented()
    {
        var reading = SourceMediaCoverage.Read(
            [Slice("  ", "Panama", SourceMediaStatus.Qualified, 1)]);

        Assert.Empty(reading.Markets);
        Assert.Contains(reading.Withheld, item => item.Reason == SourceMediaCoverage.BlankMarket);
    }

    [Fact]
    public void Missing_provenance_and_a_missing_list_are_refused()
    {
        var reading = SourceMediaCoverage.Read(
            [Slice("Santo Domingo", "Dominican Republic", SourceMediaStatus.Qualified, 1) with { Provenance = " " }]);
        Assert.Empty(reading.Markets);
        Assert.Contains(reading.Withheld, item => item.Reason == SourceMediaCoverage.MissingProvenance);

        var error = Assert.Throws<InvalidOperationException>(() => SourceMediaCoverage.Read(null));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Coverage_source_does_not_send_or_download()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "SourceMediaCoverage.cs"));
        Assert.Contains("not a census", source);
        Assert.DoesNotContain("podcast", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }

    private static StoredSlice Slice(string market, string country, string status, int credit) => new(
        market,
        country,
        status,
        credit,
        new string('a', 64),
        new string('b', 16),
        "Original Alpha studio slice. Not a copied podcast.");
}
