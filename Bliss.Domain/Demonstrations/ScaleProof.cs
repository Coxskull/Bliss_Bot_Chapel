using System.Diagnostics;

namespace Bliss.Domain.Demonstrations;

public sealed record ScaleRung(
    int Target,
    int Measured,
    bool Passed,
    bool Claimed,
    long ElapsedMilliseconds,
    string Failure);

public sealed record ScaleDocument(
    IReadOnlyList<ScaleRung> Rungs,
    bool Passed,
    string Notice,
    string Cost,
    string FactoryTarget,
    int AiCalls,
    bool ProductionChanged,
    string Delivery);

/// <summary>
/// Repeated in-memory checks of the recipe and the conversation.
/// A passed rung is a measurement. It is not a claim of stored prospects or sends.
/// </summary>
public static class ScaleProof
{
    public static readonly int[] Rungs = [100, 1000, 10000];

    public const string Notice =
        "The rungs 100, 1000, and 10000 are repeated in-memory checks. A passed check is not a claim of stored prospects or sends. Delivery remains NOT_SENT.";

    public const string Cost =
        "No dollar amount is recorded. None was invented.";

    public const string FactoryTarget =
        "The 15-minute factory target is not claimed. This measurement excludes discovery, enrichment, sending, premium creative, and final campaign production.";

    private static readonly Guid Clip = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly OverlayConcept Concept = new(
        "measurement",
        "Measurement",
        "A short placement",
        "for this fixture",
        "No person is named",
        "Look at the page",
        "123328");

    public static ScaleDocument MeasureAll()
    {
        var rungs = new List<ScaleRung>(Rungs.Length);
        foreach (var target in Rungs)
        {
            rungs.Add(Measure(target));
        }

        return new ScaleDocument(
            rungs,
            rungs.All(item => item.Passed),
            Notice,
            Cost,
            FactoryTarget,
            0,
            false,
            "NOT_SENT");
    }

    public static ScaleRung Measure(int target)
    {
        if (Array.IndexOf(Rungs, target) < 0)
        {
            throw new InvalidOperationException("A scale rung is 100, 1000, or 10000. None is invented.");
        }

        var watch = Stopwatch.StartNew();
        for (var index = 0; index < target; index++)
        {
            var failure = CheckOnce();
            if (failure is not null)
            {
                watch.Stop();
                return new ScaleRung(target, index, false, false, watch.ElapsedMilliseconds, failure);
            }
        }

        watch.Stop();
        return new ScaleRung(target, target, true, false, watch.ElapsedMilliseconds, string.Empty);
    }

    private static string? CheckOnce()
    {
        var recipeA = Recipe("scale-fixture-a", "Scale Fixture A");
        var recipeB = Recipe("scale-fixture-b", "Scale Fixture B");
        if (RecipeRuntime.Check(recipeA).Count > 0 || RecipeRuntime.Check(recipeB).Count > 0)
        {
            return "A recipe check failed.";
        }

        if (recipeA.QrDestination.Contains("scale-fixture-b", StringComparison.Ordinal)
            || recipeB.QrDestination.Contains("scale-fixture-a", StringComparison.Ordinal))
        {
            return "A recipe destination leaked the other fixture.";
        }

        var priceA = DemonstrationConversation.Reply(Facts("Scale Fixture A"), "How much does this cost?");
        if (!priceA.Reply.Contains("cannot invent a price", StringComparison.Ordinal)
            || priceA.Reply.Contains("Scale Fixture B", StringComparison.Ordinal)
            || priceA.Reply.Contains('\u0024')
            || ContainsDigit(priceA.Reply))
        {
            return "A price reply invented a number or leaked the other fixture.";
        }

        var sendA = DemonstrationConversation.Reply(Facts("Scale Fixture A"), "Please send this");
        if (!sendA.Reply.Contains("NOT_SENT", StringComparison.Ordinal)
            || sendA.Reply.Contains("Scale Fixture B", StringComparison.Ordinal))
        {
            return "A send reply left the unsent rule or leaked the other fixture.";
        }

        var suppressed = DemonstrationConversation.Reply(Facts("Scale Fixture A", suppressed: true), "Please send this");
        if (!suppressed.Reply.Contains("suppressed", StringComparison.Ordinal)
            || !suppressed.Reply.Contains("NOT_SENT", StringComparison.Ordinal))
        {
            return "Suppression did not hold.";
        }

        var priceB = DemonstrationConversation.Reply(Facts("Scale Fixture B"), "How much does this cost?");
        if (priceB.Reply.Contains("Scale Fixture A", StringComparison.Ordinal) || ContainsDigit(priceB.Reply))
        {
            return "A price reply leaked the other fixture or stated a number.";
        }

        var repeated = DemonstrationConversation.Reply(
            Facts("Scale Fixture A", lastSignal: "PRICING_QUESTION"),
            "How much does this cost?");
        if (!repeated.Reply.Contains("already answered", StringComparison.Ordinal)
            || ContainsDigit(repeated.Reply))
        {
            return "A repeated question became a new priced action.";
        }

        return null;
    }

    private static DemonstrationRecipe Recipe(string slug, string name) =>
        RecipeRuntime.Compose(
            slug,
            name,
            "Measurement",
            "en",
            name + " has not sponsored this measurement.",
            "https://example.com/demonstrations/" + slug,
            Clip,
            Concept,
            string.Empty);

    private static ProspectFacts Facts(string name, bool suppressed = false, string lastSignal = "") =>
        new(
            name,
            "TIER_4",
            "No verified mailbox is on file.",
            ["owner"],
            ["one concept"],
            Suppressed: suppressed,
            LastSignal: lastSignal);

    private static bool ContainsDigit(string value)
    {
        foreach (var character in value)
        {
            if (character is >= '0' and <= '9')
            {
                return true;
            }
        }

        return false;
    }
}
