namespace Bliss.Domain.Demonstrations;

public sealed record ConversationTurn(string Reply, string Signal, bool HumanEscalation);

public static class DemonstrationConversation
{
    public static ConversationTurn Reply(ProspectFacts facts, string message)
    {
        var text = (message ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new ConversationTurn(
                "Tell me what you want to know about this private demonstration.",
                "NONE",
                false);
        }

        var normalized = text.ToLowerInvariant();
        if (ContainsAny(normalized, "human", "person", "call me", "meeting", "representative", "speak to"))
        {
            return new ConversationTurn(
                $"I will prepare a human handoff for {facts.BusinessName}. A person still has to authorize any contract, custom price, or campaign. This is not a win.",
                "HUMAN_REQUESTED",
                true);
        }

        if (ContainsAny(normalized, "who is", "decision", "manager", "director", "owner name", "contact person"))
        {
            return new ConversationTurn(
                $"{facts.BusinessName} is the prospect. The likely buying roles are {string.Join(", ", facts.BuyingRoles)}. "
                + "A named decision-maker is not verified, so I will not address anyone by a personal name. "
                + $"The current contact route is {facts.ContactTier}: {facts.ContactRoute}",
                "DECISION_MAKER_QUESTION",
                false);
        }

        if (ContainsAny(normalized, "price", "pricing", "cost", "how much", "budget", "quote"))
        {
            return new ConversationTurn(
                "I can discuss the shape of a podcast placement, and I cannot invent a price. "
                + "Custom pricing and a binding quote stay with a human. If you share an approximate budget, timing, and market, I will record them for that handoff.",
                "PRICING_QUESTION",
                false);
        }

        if (ContainsAny(normalized, "how", "work", "alpha", "what is this", "podcast"))
        {
            return new ConversationTurn(
                "Alpha places a short advertisement inside a source video of about 15 seconds. "
                + "The overlay, the QR code, and the disclosure are produced for this private demonstration. "
                + $"{facts.BusinessName} has not commissioned the work, and the page does not mean a campaign is live.",
                "EXPLANATION",
                false);
        }

        if (ContainsAny(normalized, "concept", "video", "overlay", "example"))
        {
            var names = facts.ConceptNames.Count == 0
                ? "the concept on this page"
                : string.Join(", ", facts.ConceptNames);
            return new ConversationTurn(
                $"The concepts on this page are {names}. Each one uses an approved source slice plus an overlay made for {facts.BusinessName}.",
                "CONCEPT_QUESTION",
                false);
        }

        if (ContainsAny(normalized, "interested", "let's talk", "lets talk", "sign", "move forward", "we want"))
        {
            return new ConversationTurn(
                $"I have recorded interest from {facts.BusinessName}. That is not a contract and it is not a win. A human closes binding commitments.",
                "INTEREST",
                false);
        }

        if (ContainsAny(normalized, "audience", "timing", "geography", "city", "when", "where", "objective", "goal"))
        {
            return new ConversationTurn(
                "I recorded that detail for the prospect file. It does not authorize a campaign. Tell me the audience, geography, approximate budget, or timing if you want them included in the handoff.",
                "QUALIFICATION_DETAIL",
                false);
        }

        return new ConversationTurn(
            $"I can explain the {facts.BusinessName} demonstration, the concepts, or how a human handoff works. I will not invent a contact or a price.",
            "CLARIFY",
            false);
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(text.Contains);
}

public sealed record ProspectFacts(
    string BusinessName,
    string ContactTier,
    string ContactRoute,
    IReadOnlyList<string> BuyingRoles,
    IReadOnlyList<string> ConceptNames);
