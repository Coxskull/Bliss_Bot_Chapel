namespace Bliss.Domain.Demonstrations;

public sealed record OverlayConcept(
    string Id,
    string Name,
    string Headline,
    string Subhead,
    string Detail,
    string CallToAction,
    string AccentHex);

public static class ReferenceProspect
{
    public const string Slug = "abc-pharmacy";
    public const string BusinessName = "ABC Pharmacy";
    public const string Niche = "pharmacy";
    public const string Market = "Panama City";
    public const string Country = "Panama";
    public const string Language = "es";

    public static readonly OverlayConcept[] Concepts =
    [
        new("family-health", "Family Health Concept", "Tu Salud", "Nuestra Prioridad", "Medicamentos · Vitaminas", "VISÍTANOS HOY", "12805C"),
        new("convenience", "Convenience Concept", "Todo lo que", "necesitas en un solo lugar", "Cuidado Personal", "¡VEN HOY!", "0C4DA2"),
        new("wellness", "Wellness Concept", "Vive Bien", "Todos los Días", "Vitaminas y Suplementos", "CUIDA TU BIENESTAR", "0E7C86"),
        new("neighborhood", "Neighborhood Concept", "Tu Farmacia", "de Confianza", "en Panama City", "VISÍTANOS HOY", "12315F")
    ];

    public const string Disclosure =
        "This page was created by Alpha for illustrative business-development purposes. "
        + "ABC Pharmacy has not sponsored, commissioned, approved, or endorsed these concepts. "
        + "Example offers, products, and promotions are illustrative unless identified as verified business information. "
        + "No commercial relationship is implied.";

    public const string ContactTier = "TIER_4";

    public const string ContactRoute =
        "No verified named person and no verified company mailbox are on file. Outreach delivery is not authorized.";

    public const string DecisionMakerStatus = "UNVERIFIED";
}
