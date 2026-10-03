namespace Bliss.Domain.Demonstrations;

public sealed record ConversationTurn(string Reply, string Signal, bool HumanEscalation, string Gear = "teaching");

public static class DemonstrationConversation
{
    public const string Voice = "Ask Alpha";

    public static ConversationTurn Reply(ProspectFacts facts, string message)
    {
        var raw = Answer(facts, message);
        return Present(facts, raw with { Gear = GearFor(raw.Signal) });
    }

    public static ConversationTurn Present(ProspectFacts facts, ConversationTurn raw)
    {
        var repeated = raw.Signal is not ("NONE" or "")
            && string.Equals(facts.LastSignal, raw.Signal, StringComparison.Ordinal);
        var reply = Address(facts) + raw.Reply;
        if (repeated)
        {
            reply += " Ask Alpha already answered that.";
        }

        reply += " " + Advance(raw.Signal, repeated);
        return new ConversationTurn(reply, raw.Signal, raw.HumanEscalation, raw.Gear);
    }

    private static ConversationTurn Answer(ProspectFacts facts, string message)
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
            if (facts.Freshness == "STALE")
            {
                return new ConversationTurn(
                    $"{facts.BusinessName} has a decision-maker record that is stale and needs reverification. "
                    + "I will not address anyone by a personal name. Outreach delivery is not authorized.",
                    "DECISION_MAKER_QUESTION",
                    false);
            }

            if (facts.PersonalizationAllowed && !string.IsNullOrWhiteSpace(facts.DecisionMakerName))
            {
                return new ConversationTurn(
                    $"The recorded public name for {facts.BusinessName} is {facts.DecisionMakerName}, {facts.DecisionMakerRole}. "
                    + $"Confidence is {facts.Confidence}. The current contact route is {facts.ContactTier}: {facts.ContactRoute}",
                    "DECISION_MAKER_QUESTION",
                    false);
            }

            if (facts.Confidence == "LOW")
            {
                return new ConversationTurn(
                    $"{facts.BusinessName} has an uncertain person on file. The evidence is not strong enough to use a personal name. "
                    + $"The current contact route is {facts.ContactTier}: {facts.ContactRoute}",
                    "DECISION_MAKER_QUESTION",
                    false);
            }

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
                EconomicsPriceSpeech.PricingReply(facts.AcceptedEconomicsAmount, facts.AcceptedEconomicsCurrency),
                "PRICING_QUESTION",
                false);
        }

        if (ContainsAny(normalized, "email", "e-mail", "whatsapp", "send this", "send it", "message them", "text them"))
        {
            var suppressed = facts.Suppressed ? " The prospect is suppressed." : "";
            var preview = facts.PreviewEligible
                ? " The preview adapter can prepare a copy. Transmission remains NOT_SENT."
                : "";
            return new ConversationTurn(
                "A public road is not permission to send." + suppressed + preview + " Delivery remains NOT_SENT.",
                "ROUTE_NOT_PERMISSION",
                false);
        }

        if (ContainsAny(normalized, "how", "work", "alpha", "what is this", "podcast"))
        {
            return new ConversationTurn(
                "Alpha places a short advertisement inside a source video of about 15 seconds. "
                + "The overlay, the QR code, and the disclosure are produced for this private demonstration. "
                + $"{facts.BusinessName} has not commissioned the work, and the page does not mean a campaign is live. "
                + $"The value is a private look at a short placement for {facts.BusinessName}.",
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

    private static string Address(ProspectFacts facts)
    {
        if (facts.PersonalizationAllowed && !string.IsNullOrWhiteSpace(facts.DecisionMakerName))
        {
            return facts.DecisionMakerName.Trim() + ", ";
        }

        return "Thank you. ";
    }

    private static string Advance(string signal, bool repeated) => signal switch
    {
        "EXPLANATION" => repeated
            ? "The next step is a human handoff when you want one."
            : "The next step is to ask what a human handoff requires.",
        "PRICING_QUESTION" => repeated
            ? "I will record a budget only for that handoff."
            : "The next step is a human handoff.",
        "NEGOTIATION" => repeated
            ? "The same envelope remains."
            : "The next step is a human approval of any draft.",
        "DECISION_MAKER_QUESTION" => repeated
            ? "I will not invent a person."
            : "The next step stays on this page. Nothing is sent.",
        "ROUTE_NOT_PERMISSION" => repeated
            ? "Nothing is sent."
            : "The next step is to leave delivery at NOT_SENT.",
        "HUMAN_REQUESTED" => "The next step is that handoff.",
        "INTEREST" => "The next step is a human close.",
        "CONCEPT_QUESTION" => repeated
            ? "The same concepts remain on this page."
            : "The next step is to ask how the placement works.",
        "QUALIFICATION_DETAIL" => repeated
            ? "Those details stay on the prospect file."
            : "The next step is a handoff when you want a person involved.",
        "CLARIFY" => repeated
            ? "I am ready for the demonstration or the handoff."
            : "The next step is one question about the demonstration.",
        _ => "The next step is your question."
    };

    private static string GearFor(string signal) => signal switch
    {
        "HUMAN_REQUESTED" or "INTEREST" => "handoff",
        "PRICING_QUESTION" or "ROUTE_NOT_PERMISSION" or "DECISION_MAKER_QUESTION" or "NEGOTIATION" => "integrity",
        _ => "teaching"
    };
}

public sealed record ProspectFacts(
    string BusinessName,
    string ContactTier,
    string ContactRoute,
    IReadOnlyList<string> BuyingRoles,
    IReadOnlyList<string> ConceptNames,
    string DecisionMakerName = "",
    string DecisionMakerRole = "",
    string Confidence = "UNVERIFIED",
    bool PersonalizationAllowed = false,
    string Freshness = "UNRECORDED",
    bool Suppressed = false,
    string LastSignal = "",
    string AcceptedEconomicsAmount = "",
    string AcceptedEconomicsCurrency = "",
    bool PreviewEligible = false);
