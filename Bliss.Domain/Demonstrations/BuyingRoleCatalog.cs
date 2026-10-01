namespace Bliss.Domain.Demonstrations;

public static class BuyingRoleCatalog
{
    private static readonly Dictionary<string, string[]> Roles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pharmacy"] = ["Owner", "General manager", "Commercial manager", "Marketing manager"],
        ["grocery"] = ["Owner", "General manager", "Commercial manager", "Marketing manager"],
        ["restaurant"] = ["Owner", "General manager", "Marketing manager", "Brand manager"],
        ["coffee shop"] = ["Owner", "General manager", "Marketing manager"],
        ["car dealer"] = ["Dealer principal", "General manager", "Marketing director", "Digital marketing manager"],
        ["real estate"] = ["Owner", "Marketing manager", "Broker"],
        ["law firm"] = ["Managing partner", "Marketing manager"],
        ["dental"] = ["Owner/practitioner", "Practice administrator", "Practice manager", "Marketing manager"],
        ["medical"] = ["Owner/practitioner", "Practice administrator", "Practice manager", "Marketing manager"]
    };

    public static IReadOnlyList<string> RolesFor(string niche)
    {
        var key = (niche ?? string.Empty).Trim();
        return Roles.TryGetValue(key, out var roles)
            ? roles
            : ["Owner", "General manager", "Marketing manager"];
    }

    public static IReadOnlyCollection<string> Niches => Roles.Keys.ToArray();
}
