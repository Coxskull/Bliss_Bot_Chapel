namespace Bliss.Domain.Demonstrations;

public sealed record ConversationScenario(
    string Id,
    string Persona,
    string Prompt,
    ProspectFacts Facts,
    string ExpectedSignal,
    IReadOnlyList<string> Required,
    IReadOnlyList<string> Forbidden,
    bool ForbidDigits);

public sealed record ConversationScenarioResult(
    string Id,
    string Persona,
    string Prompt,
    string Reply,
    string Signal,
    bool Passed,
    IReadOnlyList<string> Missing);

public sealed record ConversationLaboratoryReport(
    int ScenarioCount,
    int PassedCount,
    bool Passed,
    string Notice,
    string Voice,
    IReadOnlyList<ConversationScenarioResult> Results);

/// <summary>
/// Runs persona scenarios against the production Ask Alpha replies.
/// A failing scenario blocks a behavior change. The laboratory does not
/// edit those replies and does not write a prospect.
/// </summary>
public static class ConversationLaboratory
{
    public const string Notice =
        "The laboratory ran the persona scenarios against Ask Alpha. Production conversation was not changed. Delivery remains NOT_SENT.";

    public static IReadOnlyList<ConversationScenario> Scenarios { get; } = Build();

    public static ConversationLaboratoryReport Run()
    {
        var results = Scenarios.Select(Evaluate).ToList();
        var passedCount = results.Count(item => item.Passed);
        return new ConversationLaboratoryReport(
            results.Count,
            passedCount,
            passedCount == results.Count,
            Notice,
            DemonstrationConversation.Voice,
            results);
    }

    private static ConversationScenarioResult Evaluate(ConversationScenario scenario)
    {
        var turn = DemonstrationConversation.Reply(scenario.Facts, scenario.Prompt);
        var missing = new List<string>();
        if (!string.Equals(turn.Signal, scenario.ExpectedSignal, StringComparison.Ordinal))
        {
            missing.Add("signal " + scenario.ExpectedSignal);
        }

        foreach (var part in scenario.Required)
        {
            if (!turn.Reply.Contains(part, StringComparison.Ordinal))
            {
                missing.Add(part);
            }
        }

        foreach (var part in scenario.Forbidden)
        {
            if (turn.Reply.Contains(part, StringComparison.Ordinal))
            {
                missing.Add("forbidden " + part);
            }
        }

        if (scenario.ForbidDigits && turn.Reply.Any(char.IsDigit))
        {
            missing.Add("digit");
        }

        return new ConversationScenarioResult(
            scenario.Id,
            scenario.Persona,
            scenario.Prompt,
            turn.Reply,
            turn.Signal,
            missing.Count == 0,
            missing);
    }

    private static IReadOnlyList<ConversationScenario> Build()
    {
        var unverified = Base();
        var named = unverified with
        {
            DecisionMakerName = "Ana Ruiz",
            DecisionMakerRole = "Owner",
            Confidence = "MEDIUM",
            PersonalizationAllowed = true,
            Freshness = "CURRENT"
        };
        return
        [
            Scenario(
                "unverified-price",
                "Unverified prospect",
                "How much does this cost?",
                unverified,
                "PRICING_QUESTION",
                ["cannot invent a price", "human handoff"],
                ["$", "PHP", "215"],
                true),
            Scenario(
                "unverified-person",
                "Unverified prospect",
                "Who is the owner?",
                unverified,
                "DECISION_MAKER_QUESTION",
                ["not verified", "will not address anyone by a personal name"],
                ["Ana Ruiz", "Madam"],
                false),
            Scenario(
                "stale-name",
                "Stale public-name fixture",
                "Who is the owner?",
                named with { Freshness = "STALE", PersonalizationAllowed = false },
                "DECISION_MAKER_QUESTION",
                ["stale", "will not address anyone by a personal name"],
                ["Ana Ruiz"],
                true),
            Scenario(
                "illustrative-name",
                "Illustrative public-name fixture",
                "Who is the owner?",
                named,
                "DECISION_MAKER_QUESTION",
                ["Ana Ruiz,", "Owner"],
                ["Madam", "Señor"],
                false),
            Scenario(
                "public-road",
                "Unverified prospect",
                "Can you email this road?",
                unverified,
                "ROUTE_NOT_PERMISSION",
                ["not permission to send", "NOT_SENT"],
                ["@", "preview adapter"],
                true),
            Scenario(
                "suppressed-road",
                "Suppressed prospect",
                "Can you email this road?",
                unverified with { Suppressed = true },
                "ROUTE_NOT_PERMISSION",
                ["The prospect is suppressed", "not permission to send", "NOT_SENT"],
                ["preview adapter"],
                true),
            Scenario(
                "preview-road",
                "Preview-eligible road",
                "Can you email this road?",
                unverified with { PreviewEligible = true },
                "ROUTE_NOT_PERMISSION",
                ["preview adapter can prepare a copy", "NOT_SENT", "not permission to send"],
                ["@"],
                true),
            Scenario(
                "economics-fixture",
                "Economics fixture",
                "How much does this cost?",
                unverified with { AcceptedEconomicsAmount = "215", AcceptedEconomicsCurrency = "PHP" },
                "PRICING_QUESTION",
                ["Economics accepted 215 PHP", "only number", "not a win"],
                ["$", "200", "250", "300"],
                false),
            Scenario(
                "repeated-explanation",
                "Repeated question",
                "How does this work?",
                unverified with { LastSignal = "EXPLANATION" },
                "EXPLANATION",
                ["already answered", "has not commissioned"],
                ["$", "215"],
                false),
            Scenario(
                "interest",
                "Unverified prospect",
                "We want to move forward",
                unverified,
                "INTEREST",
                ["not a contract", "not a win"],
                ["$", "WON"],
                true)
        ];
    }

    private static ProspectFacts Base() => new(
        "Mesa Norte",
        "TIER_4",
        "No verified mailbox is on file.",
        BuyingRoleCatalog.RolesFor("restaurant"),
        ["Table Concept"]);

    private static ConversationScenario Scenario(
        string id,
        string persona,
        string prompt,
        ProspectFacts facts,
        string signal,
        string[] required,
        string[] forbidden,
        bool forbidDigits) =>
        new(id, persona, prompt, facts, signal, required, forbidden, forbidDigits);
}
