using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class FactoryBatchTests
{
    [Fact]
    public void A_preserved_business_and_a_checked_recipe_pass_without_a_price()
    {
        var manifest = FactoryBatch.Inspect(new FactoryLibrary(
            [
                Preserved("Puerto Azul"),
                Produced("Mesa Norte", "mesa-norte", GoodConcept("table"))
            ],
            1));

        Assert.Equal("PASSED", manifest.Status);
        Assert.Equal(1, manifest.PreservedCount);
        Assert.Equal(1, manifest.ConceptCount);
        Assert.Equal(0, manifest.AiCalls);
        Assert.Empty(manifest.Exceptions);
        Assert.Contains("No dollar amount", manifest.Cost);
        Assert.Contains("Economics", manifest.Cost);
        Assert.DoesNotContain("$", manifest.Cost);
    }

    [Fact]
    public void A_qr_that_leaves_the_page_is_an_exception()
    {
        var concept = GoodConcept("table") with { QrDestination = "https://example.com/other" };
        var manifest = FactoryBatch.Inspect(new FactoryLibrary([Produced("Mesa Norte", "mesa-norte", concept)], 1));

        Assert.Equal("EXCEPTIONS", manifest.Status);
        Assert.Contains(manifest.Exceptions, item => item.Contains("QR destination"));
        Assert.Equal(0, manifest.AiCalls);
    }

    [Fact]
    public void A_preserved_business_with_a_demonstration_is_an_exception()
    {
        var prospect = Preserved("Puerto Azul") with { Concepts = [GoodConcept("table")] };
        var manifest = FactoryBatch.Inspect(new FactoryLibrary([prospect], 1));

        Assert.Equal("EXCEPTIONS", manifest.Status);
        Assert.Contains(manifest.Exceptions, item => item.Contains("preserved and has a demonstration"));
    }

    [Fact]
    public void Overlay_1_does_not_require_a_permanent_composite()
    {
        var concept = GoodConcept("table") with { FlattenedFileExists = false };
        var manifest = FactoryBatch.Inspect(new FactoryLibrary([Produced("Mesa Norte", "mesa-norte", concept)], 1));

        Assert.Equal("PASSED", manifest.Status);
        Assert.Empty(manifest.Exceptions);
        Assert.Equal(0, manifest.AiCalls);
    }

    [Fact]
    public void A_missing_flattened_composite_is_an_exception_for_an_older_picture()
    {
        var concept = GoodConcept("table") with { RecipeVersion = "", FlattenedFileExists = false };
        var manifest = FactoryBatch.Inspect(new FactoryLibrary([Produced("Mesa Norte", "mesa-norte", concept)], 1));

        Assert.Contains(manifest.Exceptions, item => item.Contains("flattened composite is missing"));
    }

    private static FactoryProspect Preserved(string name) => new(
        OpportunityScreen.Slug(name),
        name,
        "Quito",
        "en",
        NicheOverlay.Disclosure(name),
        "",
        "PRESERVED",
        false,
        []);

    private static FactoryProspect Produced(string name, string slug, FactoryConcept concept) => new(
        slug,
        name,
        "Panama City",
        "es",
        NicheOverlay.Disclosure(name),
        "",
        "DEMONSTRATION_PREPARED",
        false,
        [concept]);

    private static FactoryConcept GoodConcept(string id) => new(
        id,
        RecipeRuntime.Version,
        "PASSED",
        "La Mesa",
        "Te Espera",
        "Panama City",
        "RESERVA HOY",
        "https://example.com/demonstrations/mesa-norte",
        Guid.NewGuid(),
        true,
        true,
        true);
}
