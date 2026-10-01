namespace Bliss.Domain.Demonstrations;

public static class NicheOverlay
{
    public static OverlayConcept One(string niche, string language, string market)
    {
        var spanish = string.Equals(language, "es", StringComparison.OrdinalIgnoreCase);
        return (niche ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "restaurant" => Concept(
                "table",
                "Table Concept",
                spanish ? "La Mesa" : "Your Table",
                spanish ? "Te Espera" : "Is Ready",
                market,
                spanish ? "RESERVA HOY" : "RESERVE TODAY",
                "8A3B12"),
            "coffee shop" => Concept(
                "cup",
                "Cup Concept",
                spanish ? "Tu Café" : "Your Coffee",
                spanish ? "De la esquina" : "On the corner",
                market,
                spanish ? "PASA HOY" : "STOP IN TODAY",
                "6B3A1F"),
            "car dealer" => Concept(
                "vehicle",
                "Vehicle Concept",
                spanish ? "Tu Próximo" : "Your Next",
                spanish ? "Vehículo" : "Vehicle",
                market,
                spanish ? "VEN HOY" : "VISIT TODAY",
                "0C4DA2"),
            "real estate" => Concept(
                "home",
                "Home Concept",
                spanish ? "Tu Próximo" : "Your Next",
                spanish ? "Hogar" : "Home",
                market,
                spanish ? "CONOCE MÁS" : "LEARN MORE",
                "12315F"),
            "law firm" => Concept(
                "counsel",
                "Counsel Concept",
                spanish ? "Orientación" : "Clear",
                spanish ? "Clara" : "Counsel",
                market,
                spanish ? "HABLA HOY" : "TALK TODAY",
                "1E3A5F"),
            "grocery" => Concept(
                "fresh",
                "Fresh Concept",
                spanish ? "Lo del día" : "Today's",
                spanish ? "En un solo lugar" : "In one place",
                market,
                spanish ? "VISÍTANOS HOY" : "VISIT TODAY",
                "1F7A4D"),
            _ => Concept(
                "care",
                "Care Concept",
                spanish ? "Tu Salud" : "Your Health",
                spanish ? "Nuestra Prioridad" : "Comes First",
                market,
                spanish ? "VISÍTANOS HOY" : "VISIT TODAY",
                "12805C")
        };
    }

    public static string Disclosure(string businessName) =>
        "This page was created by Alpha for illustrative business-development purposes. "
        + businessName
        + " has not sponsored, commissioned, approved, or endorsed these concepts. "
        + "Example offers, products, and promotions are illustrative unless identified as verified business information. "
        + "No commercial relationship is implied.";

    private static OverlayConcept Concept(
        string id,
        string name,
        string headline,
        string subhead,
        string detail,
        string callToAction,
        string accent) =>
        new(id, name, headline, subhead, detail, callToAction, accent);
}
