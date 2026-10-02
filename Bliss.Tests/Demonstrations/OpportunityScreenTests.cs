using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class OpportunityScreenTests
{
    [Fact]
    public void A_named_public_business_in_an_initial_market_passes()
    {
        var result = OpportunityScreen.Evaluate(
            "restaurant",
            "Panama City",
            "Casa Verde",
            "https://example.com/casa-verde");

        Assert.Equal(100, result.Score);
        Assert.True(result.PassesInitialScreen);
        Assert.True(result.PreservesBusiness);
        Assert.Equal("es", result.Language);
        Assert.Equal("Panama", result.Country);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void A_named_public_business_outside_the_initial_markets_is_preserved()
    {
        var result = OpportunityScreen.Evaluate(
            "restaurant",
            "Quito",
            "Puerto Azul",
            "https://example.com/puerto-azul");

        Assert.Equal(75, result.Score);
        Assert.False(result.PassesInitialScreen);
        Assert.True(result.PreservesBusiness);
        Assert.Contains(result.Reasons, reason => reason.Contains("outside the initial six cities"));
    }

    [Fact]
    public void Missing_public_source_does_not_pass_and_does_not_invent_a_name()
    {
        var result = OpportunityScreen.Evaluate("restaurant", "Panama City", "", "");

        Assert.False(result.PassesInitialScreen);
        Assert.False(result.PreservesBusiness);
        Assert.Contains(result.Reasons, reason => reason.Contains("will not invent"));
        Assert.Contains(result.Reasons, reason => reason.Contains("public http"));
    }

    [Fact]
    public void Restaurant_overlay_uses_the_market_language()
    {
        var spanish = NicheOverlay.One("restaurant", "es", "Panama City");
        var english = NicheOverlay.One("restaurant", "en", "Manila");

        Assert.Equal("La Mesa", spanish.Headline);
        Assert.Equal("Your Table", english.Headline);
        Assert.Contains("Casa Verde", NicheOverlay.Disclosure("Casa Verde"));
    }
}
