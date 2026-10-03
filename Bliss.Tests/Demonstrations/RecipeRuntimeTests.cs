using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class RecipeRuntimeTests
{
    [Fact]
    public void A_restaurant_recipe_matches_the_page_qr_and_the_disclosure()
    {
        var concept = NicheOverlay.One("restaurant", "es", "Panama City");
        var recipe = RecipeRuntime.Compose(
            "mesa-norte",
            "Mesa Norte",
            "Panama City",
            "es",
            NicheOverlay.Disclosure("Mesa Norte"),
            "https://example.com/demonstrations/mesa-norte",
            Guid.NewGuid(),
            concept,
            "");

        Assert.Equal("overlay-1", recipe.Version);
        Assert.Equal("La Mesa", recipe.Headline);
        Assert.Empty(RecipeRuntime.Check(recipe));
        Assert.False(RecipeRuntime.PermanentCompositeRequired(recipe.Version));
        Assert.Contains("player is the served picture", RecipeRuntime.PlayerNotice);
        Assert.Contains("not required", RecipeRuntime.PlayerNotice);
        Assert.Contains("None was written", RecipeRuntime.PlayerNotice);
        Assert.Contains("NOT_SENT", RecipeRuntime.PlayerNotice);
    }

    [Fact]
    public void A_qr_that_leaves_the_prospect_page_fails_qa()
    {
        var recipe = Sample() with { QrDestination = "https://example.com/other" };

        Assert.Contains(RecipeRuntime.Check(recipe), reason => reason.Contains("QR destination"));
    }

    [Fact]
    public void A_recipe_that_names_a_person_fails_qa_and_is_not_a_composite()
    {
        var recipe = Sample() with { DecisionMakerName = "Ana Ruiz", Headline = "Ana Ruiz" };

        var reasons = RecipeRuntime.Check(recipe);
        Assert.Contains(reasons, reason => reason.Contains("will not invent one"));
    }

    [Fact]
    public void The_player_source_does_not_send_or_call_a_model()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "RecipeRuntime.cs"));
        Assert.Contains("permanent composite is not required", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }

    private static DemonstrationRecipe Sample()
    {
        var concept = NicheOverlay.One("restaurant", "es", "Panama City");
        return RecipeRuntime.Compose(
            "mesa-norte",
            "Mesa Norte",
            "Panama City",
            "es",
            NicheOverlay.Disclosure("Mesa Norte"),
            "https://example.com/demonstrations/mesa-norte",
            Guid.NewGuid(),
            concept,
            "");
    }
}
