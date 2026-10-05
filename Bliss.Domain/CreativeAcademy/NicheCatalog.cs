namespace Bliss.Domain.CreativeAcademy;

public sealed record NicheEntry(
    int Number,
    string Key,
    string Name,
    string Status,
    string AnchorKind,
    string TeacherKey,
    bool CreatedThisRun,
    string Notice);

/// <summary>
/// The ordered 50-niche roster. Created niches are not repeated.
/// A niche without a stored anchor stays not created.
/// </summary>
public static class NicheCatalog
{
    public const string Created = "CREATED";
    public const string NotCreated = "NOT_CREATED";
    public const string LocalAnchor = "LOCAL";
    public const string ExternalAnchor = "EXTERNAL";
    public const string None = "NONE";
    public const string AlreadyCreated = "ALREADY_CREATED";
    public const string NicheNotCreated = "NICHE_NOT_CREATED";

    private const string ExternalNotice =
        "Already created. The prototype was not repeated and no substitute image was invented.";

    private const string OpenNotice =
        "Not created. No quality anchor was invented.";

    public static IReadOnlyList<NicheEntry> All { get; } =
    [
        Local(1, "pharmacy", "Pharmacy / Drugstore", CreativeAcademy.PharmacyTeacher,
            "Already created. Local master vidacare-master-01 was not repeated."),
        Local(2, "supermarket", "Supermarket / Hypermarket", CreativeAcademy.SupermarketTeacher,
            "Already created. Local supplied example freshmart-supplied was not repeated and was not promoted to a master prototype."),
        External(3, "shopping-mall", "Shopping Mall / Shopping Center"),
        External(4, "new-car", "New-Car Dealership"),
        External(5, "used-car", "Used-Car Dealership"),
        External(6, "restaurant", "Restaurant / Casual Dining"),
        Local(7, "fitness", "Gym / Fitness Center", CreativeAcademy.FitnessTeacher,
            "Already created. Local reference nova-fit-reference was not repeated."),
        Local(8, "automotive", "Automotive Service / Mechanic Repair Shop", CreativeAcademy.AutomotiveTeacher,
            "Already created. Local reference taller-ruta-reference was not repeated."),
        External(9, "dental", "Dental Office / Dental Clinic"),
        Local(10, "motorcycle", "Motorcycle / Moped Dealership", CreativeAcademy.MotorcycleTeacher,
            "Created in this run as brava-moto-reference. The visual benchmark is unrecorded. It teaches quality and is not an approved campaign.",
            true),
        Open(11, "auto-parts", "Auto-Parts Store"),
        Open(12, "gas-station", "Gas Station / Convenience Store"),
        Open(13, "fast-food", "Fast-Food Restaurant"),
        Open(14, "pizza", "Pizza Restaurant"),
        Open(15, "coffee-shop", "Coffee Shop / Café"),
        Open(16, "bakery", "Bakery / Patisserie"),
        Open(17, "sports-bar", "Sports Bar / Nightlife Venue"),
        Open(18, "hotel", "Hotel / Resort"),
        Open(19, "travel", "Travel / Tourism Business"),
        Open(20, "real-estate", "Real-Estate Agency / Property Developer"),
        Open(21, "law", "Law Firm / Legal Practice"),
        Open(22, "medical", "Medical Clinic / Doctor's Office"),
        Open(23, "optical", "Optical / Eyewear Store"),
        Open(24, "beauty-salon", "Beauty Salon"),
        Open(25, "barber", "Barber Shop"),
        Open(26, "nail-salon", "Nail Salon"),
        Open(27, "spa", "Spa / Beauty / Aesthetics"),
        Open(28, "fashion", "Clothing / Fashion Retailer"),
        Open(29, "sneakers", "Athletic Shoes / Sneaker Retailer"),
        Open(30, "jewelry", "Jewelry / Watch Retailer"),
        Open(31, "electronics", "Electronics Retailer"),
        Open(32, "mobile-phone", "Mobile-Phone / Accessories Store"),
        Open(33, "furniture", "Furniture / Home Furnishing"),
        Open(34, "hardware", "Home Improvement / Hardware"),
        Open(35, "appliance", "Appliance Retailer"),
        Open(36, "department", "Department / Variety Store"),
        Open(37, "toys", "Toy / Children's Retailer"),
        Open(38, "pet", "Pet Store / Pet Care"),
        Open(39, "education", "Education / Vocational Training"),
        Open(40, "financial", "Financial Services / Insurance"),
        Open(41, "soda", "Soda / Soft-Drink Brand"),
        Open(42, "bottled-water", "Bottled-Water Brand"),
        Open(43, "juice", "Fruit-Juice / Beverage Brand"),
        Open(44, "coffee-brand", "Coffee Product Brand"),
        Open(45, "chocolate", "Chocolate / Confectionery"),
        Open(46, "candy", "Candy / Snack Brand"),
        Open(47, "packaged-bakery", "Bread / Packaged Bakery"),
        Open(48, "ice-cream", "Ice-Cream / Frozen Dessert"),
        Open(49, "personal-care", "Personal-Care / Cosmetics"),
        Open(50, "household", "Household Cleaning / Consumer Products")
    ];

    public static NicheEntry NextOpen => All.First(item => item.Status == NotCreated);

    public static NicheEntry? Find(string? key)
    {
        var lane = (key ?? string.Empty).Trim().ToLowerInvariant();
        if (lane == "grocery")
        {
            lane = "supermarket";
        }

        return All.FirstOrDefault(item => string.Equals(item.Key, lane, StringComparison.Ordinal));
    }

    private static NicheEntry Local(int number, string key, string name, string teacher, string notice, bool createdThisRun = false) =>
        new(number, key, name, Created, LocalAnchor, teacher, createdThisRun, notice);

    private static NicheEntry External(int number, string key, string name) =>
        new(number, key, name, Created, ExternalAnchor, string.Empty, false, ExternalNotice);

    private static NicheEntry Open(int number, string key, string name) =>
        new(number, key, name, NotCreated, None, string.Empty, false, OpenNotice);
}
