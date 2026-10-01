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
        Assert.Equal("es", result.Language);
        Assert.Equal("Panama", result.Country);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Missing_public_source_does_not_pass_and_does_not_invent_a_name()
    {
        var result = OpportunityScreen.Evaluate("restaurant", "Panama City", "", "");

        Assert.False(result.PassesInitialScreen);
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
