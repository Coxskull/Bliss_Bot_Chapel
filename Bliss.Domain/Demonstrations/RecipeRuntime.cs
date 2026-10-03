namespace Bliss.Domain.Demonstrations;

public sealed record DemonstrationRecipe(
    string Version,
    string Slug,
    string BusinessName,
    string Market,
    string Language,
    string Headline,
    string Subhead,
    string Detail,
    string CallToAction,
    string QrDestination,
    string Disclosure,
    Guid SourceClipId,
    string DecisionMakerName);

public static class RecipeRuntime
{
    public const string Version = "overlay-1";

    public const string PlayerNotice =
        "The player is the served picture. The recipe is drawn on the approved source slice. A permanent composite is not required. None was written. Green does not send. Delivery remains NOT_SENT.";

    public static bool PermanentCompositeRequired(string? recipeVersion) =>
        !string.Equals((recipeVersion ?? string.Empty).Trim(), Version, StringComparison.Ordinal);

    public static DemonstrationRecipe Compose(
        string slug,
        string businessName,
        string market,
        string language,
        string disclosure,
        string qrDestination,
        Guid sourceClipId,
        OverlayConcept concept,
        string? decisionMakerName) =>
        new(
            Version,
            slug,
            businessName,
            market,
            language,
            concept.Headline,
            concept.Subhead,
            concept.Detail,
            concept.CallToAction,
            qrDestination,
            disclosure,
            sourceClipId,
            decisionMakerName ?? string.Empty);

    public static IReadOnlyList<string> Check(DemonstrationRecipe recipe)
    {
        var reasons = new List<string>();
        if (recipe.Version != Version)
        {
            reasons.Add("The recipe version is not the approved overlay.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Headline)
            || string.IsNullOrWhiteSpace(recipe.Subhead)
            || string.IsNullOrWhiteSpace(recipe.CallToAction))
        {
            reasons.Add("The recipe is missing a headline, subhead, or call to action.");
        }

        if (recipe.SourceClipId == Guid.Empty)
        {
            reasons.Add("The recipe has no approved source slice.");
        }

        if (recipe.Language is not ("en" or "es"))
        {
            reasons.Add("The recipe language is not the prospect language.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Market))
        {
            reasons.Add("The recipe is missing the prospect market.");
        }

        var slug = (recipe.Slug ?? string.Empty).Trim();
        var destination = (recipe.QrDestination ?? string.Empty).Trim();
        if (slug.Length < 3 || !destination.EndsWith("/demonstrations/" + slug, StringComparison.Ordinal))
        {
            reasons.Add("The QR destination is not this prospect page.");
        }

        var disclosure = recipe.Disclosure ?? string.Empty;
        if (string.IsNullOrWhiteSpace(recipe.BusinessName)
            || !disclosure.Contains(recipe.BusinessName, StringComparison.Ordinal)
            || !disclosure.Contains("has not sponsored", StringComparison.Ordinal))
        {
            reasons.Add("The disclosure does not name the business and the missing sponsorship.");
        }

        var person = (recipe.DecisionMakerName ?? string.Empty).Trim();
        if (person.Length >= 2)
        {
            var fields = new[] { recipe.Headline, recipe.Subhead, recipe.Detail, recipe.CallToAction };
            if (fields.Any(field => (field ?? string.Empty).Contains(person, StringComparison.OrdinalIgnoreCase)))
            {
                reasons.Add("The recipe names a person. Alpha will not invent one on the overlay.");
            }
        }

        return reasons;
    }
}
