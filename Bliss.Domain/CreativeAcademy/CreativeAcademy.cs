using System.Globalization;
using System.Text;

namespace Bliss.Domain.CreativeAcademy;

public sealed record QualityWeights(
    int Typography,
    int Lighting,
    int Realism,
    int Composition,
    int Hierarchy,
    int Identity,
    int Credibility,
    int Creator,
    int Cta)
{
    public static QualityWeights Initial { get; } = new(16, 12, 12, 12, 10, 10, 10, 10, 8);

    public int Total =>
        Typography + Lighting + Realism + Composition + Hierarchy + Identity + Credibility + Creator + Cta;
}

public sealed record Inspection(
    string Status,
    int? Score,
    bool Critical,
    bool VisualRecorded,
    IReadOnlyList<string> Defects,
    IReadOnlyList<string> Preserve,
    IReadOnlyList<string> Repair,
    int ModelCalls,
    bool CampaignReady,
    bool GreenMeansSend,
    string Delivery,
    string Notice);

public sealed record DnaDecision(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string Family,
    string BrandName,
    string Hero,
    string Palette,
    string Cta,
    string Personality,
    string TeacherKey,
    bool TeacherOnFile,
    bool Distinct,
    int ModelCalls);

public sealed record CurriculumLesson(
    string LessonKey,
    string Role,
    string Family,
    string BrandName,
    string Headline,
    string Body,
    string ProperNouns,
    string ImagePath);

public sealed record CastingDecision(
    string Status,
    string Market,
    string Direction,
    bool ResearchRequired);

public sealed record ProductionBrief(
    string Status,
    string BrandName,
    string Family,
    string TeacherKey,
    string Market,
    string Language,
    string Palette,
    string FontFamily,
    string Headline,
    string Cta,
    string Requirements,
    string GenerationRecipe,
    CastingDecision Casting,
    IReadOnlyList<string> QualitySignature,
    IReadOnlyList<string> ReplaceCreative,
    bool CanGenerate,
    int ModelCalls,
    bool CampaignReady,
    string Delivery,
    string Notice);

public sealed record ParityDecision(
    string Status,
    IReadOnlyList<string> Defects,
    IReadOnlyList<string> Repair,
    bool EligibleToContinue,
    bool CampaignReady,
    int ModelCalls,
    string Delivery,
    string Notice);

/// <summary>
/// The prototype teaches quality. Creative DNA customizes the advertiser.
/// The production brief describes new work. The instructor enforces both
/// customization compliance and reference-quality parity. A model is not called.
/// </summary>
public static class CreativeAcademy
{
    public const string Reference = "REFERENCE";
    public const string MasterReference = "MASTER_REFERENCE";
    public const string SuppliedExample = "SUPPLIED_EXAMPLE";
    public const string Pass = "PASS";
    public const string Revise = "REVISE";
    public const string Fail = "FAIL";
    public const string Withheld = "WITHHELD";
    public const string Eligible = "ELIGIBLE_TO_CONTINUE";
    public const string ProductionSpecReady = "PRODUCTION_SPEC_READY";
    public const string PatisserieTeacher = "maison-fleur";
    public const string PharmacyTeacher = "vidacare-master-01";
    public const string SupermarketTeacher = "freshmart-supplied";
    public const string FitnessTeacher = "nova-fit-reference";
    public const string AutomotiveTeacher = "taller-ruta-reference";
    public const string MotorcycleTeacher = "brava-moto-reference";

    public const string Notice =
        "The Academy is designed to produce original advertisements at the reference quality class. Reproduce the craftsmanship. Replace the creative content. Advertiser Brand DNA remains authoritative. Both customization compliance and quality parity must pass. A vision model is not configured. A production model was not called. Campaign ready is refused. Green does not send. Delivery remains NOT_SENT.";

    public static IReadOnlyList<string> ReferenceParityGate { get; } =
    [
        "color power",
        "tonal contrast",
        "lighting craftsmanship",
        "highlight brilliance",
        "shadow quality",
        "dimensional depth",
        "foreground/background separation",
        "material realism",
        "texture fidelity",
        "hero dominance",
        "visual hierarchy",
        "typographic quality",
        "screen pop",
        "commercial polish",
        "Premium Dominant Presence",
        "overall reference quality parity"
    ];

    public static IReadOnlyList<string> ReplaceCreativeContent { get; } =
    [
        "brand and logo",
        "headline, copy, and call to action",
        "photography and people",
        "hero product and supporting products",
        "environment and props",
        "composition and campaign expression"
    ];

    public static IReadOnlyDictionary<string, string> NicheAnchors { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pharmacy"] = PharmacyTeacher,
            ["supermarket"] = SupermarketTeacher,
            ["grocery"] = SupermarketTeacher,
            ["fitness"] = FitnessTeacher,
            ["automotive"] = AutomotiveTeacher,
            ["motorcycle"] = MotorcycleTeacher,
            ["patisserie"] = PatisserieTeacher
        };

    private static readonly string[] PreserveList =
    [
        "composition",
        "hero dessert",
        "hosts",
        "brand identity",
        "call to action",
        "supporting imagery"
    ];

    private static readonly HashSet<string> LegacyFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "patisserie",
        "grocery",
        "coffee",
        "beauty"
    };

    private static readonly HashSet<string> Lexicon = new(StringComparer.Ordinal)
    {
        "AUTHENTIC", "ANYWHERE", "ART", "ARTISAN", "AWAITS", "BLOSSOM", "BLOOD", "BORN", "BOUTIQUE",
        "BREATHTAKING", "CAKE", "CALIFORNIA", "CITRUS", "COASTAL", "CRAFTED", "CREATIONS", "CURD",
        "DELIVERY", "DESSERTS", "DINING", "ESCAPE", "EXPERIENCE", "EXQUISITE", "FLAVORS", "FRENCH",
        "GELATO", "HERE", "ICONIC", "INDULGENCE", "INGREDIENTS", "JOURNEY", "LEMON", "LIFE",
        "LOCATIONS", "MACARONS", "MEYER", "MOMENTS", "NOW", "ORANGE", "ORDER", "OUR", "PASTRIES",
        "PATISSERIE", "PICKUP", "PREMIUM", "PRIVATE", "RECIPES", "RESERVE", "ROMANTIC", "SEASONAL",
        "SIGNATURE", "SUNSHINE", "SWEET", "SWEETER", "SWEETNESS", "TABLE", "TARTS", "TECHNIQUES",
        "UNFORGETTABLE", "VIEWS", "WORLD", "YOUR"
    };

    public static IReadOnlyList<CurriculumLesson> PatisserieLessons { get; } =
    [
        new(
            "maison-fleur",
            Reference,
            "patisserie",
            "Maison Fleur",
            "A sweeter journey awaits you.",
            "French artistry. Sweeter moments anywhere in the world. Premium ingredients. Artisan crafted. Authentic French recipes. Signature pastries. Seasonal creations. Romantic dining. Iconic locations. Reserve your table.",
            "MAISON,FLEUR",
            "/operations/academy/maison-fleur.png"),
        new(
            "lamour-sucre",
            "CANDIDATE",
            "patisserie",
            "L'Amour Sucré",
            "A sweet escape escape awaits.",
            "Exquisite desserts. Unforgettable moments. Premium ingredients. Artisan crafted. Authentic French techniques. Signature macarons. Seasonal tarts. Private dining. Our boutique. Reserve your table.",
            "LAMOUR,SUCRE",
            "/operations/academy/lamour-sucre.png"),
        new(
            "belmonte",
            "CANDIDATE",
            "patisserie",
            "Belmonté",
            "Life is sweeter here.",
            "Artisan desserts for a sweeter life. Premium ingredients. Artisan crafted. Unforgettable flavors. Signature desserts. Seasonal creations. Romantic dining. Breathtaking views. Reserve your table.",
            "BELMONTE",
            "/operations/academy/belmonte.png"),
        new(
            "solara",
            "CANDIDATE",
            "patisserie",
            "Solara",
            "The art of indulgence, born in LA.",
            "Premium ingredients. Artisan crafted. Unforgettable flavors. California citrus blossom cake. Blood orange curdj in cake. California mjer lemon gelato. Sweetness, sunshine and you. Signature pastries. Seasonal creations. LA dining experience. California coastal views. Order now for delivery or pickup.",
            "SOLARA",
            "/operations/academy/solara.png")
    ];

    public static IReadOnlyList<CurriculumLesson> QualityAnchorLessons { get; } =
    [
        new(
            "vidacare-master-01",
            MasterReference,
            "pharmacy",
            "VidaCare Pharmacy",
            "Care for a brighter you.",
            "Alpha Master Prototype 01/50. Niche: pharmacy and drugstore. Purpose: quality anchor. Creative template: no. Protect photorealism, lighting craftsmanship, deep readable contrast, rich color, dimensional depth, material realism, hero dominance, professional typography, screen pop, and Premium Dominant Presence. Replace the brand, products, people, store, copy, colors, typography, photography, and composition for the advertiser.",
            "VIDACARE",
            "/operations/academy/vidacare-master-01.png"),
        new(
            "freshmart-supplied",
            SuppliedExample,
            "grocery",
            "FreshMart Supermarket",
            "Good food. Brighter lives.",
            "Owner-supplied supermarket example. It demonstrates the kind of commercially powerful advertisement the Academy is intended to produce. It is preserved as supplied context and is not promoted to a master prototype without an explicit designation.",
            "FRESHMART",
            "/operations/academy/freshmart-supplied.png"),
        new(
            FitnessTeacher,
            Reference,
            "fitness",
            "Nova Fit Gym & Wellness Club",
            "Tu mejor versión comienza aquí.",
            "Owner-supplied fitness quality reference. Protect energetic hero dominance, realistic people and anatomy, rich black/yellow/red contrast, dimensional gym lighting, crisp offer hierarchy, legible service benefits, CTA clarity, QR-safe placement, and premium commercial impact. Replace the fictional brand, people, gym, offer, copy, photography, and composition for the client.",
            "NOVA,FIT",
            "/operations/academy/nova-fit-reference.png"),
        new(
            AutomotiveTeacher,
            Reference,
            "automotive",
            "Taller Ruta Auto Service",
            "Tu auto en buenas manos.",
            "Owner-supplied automotive-service quality reference. Protect photorealistic technicians, tools, parts and vehicle materials, deep red/black/white contrast, directional workshop lighting, strong service hierarchy, legible benefits, CTA clarity, QR-safe placement, and premium commercial impact. Replace the fictional brand, people, workshop, services, copy, photography, and composition for the client.",
            "TALLER,RUTA",
            "/operations/academy/taller-ruta-reference.png"),
        new(
            MotorcycleTeacher,
            Reference,
            "motorcycle",
            "Brava Moto",
            "Tu proxima ruta empieza aqui.",
            "Motorcycle and moped dealership quality reference for niche 10. Protect photorealistic motorcycles, riders, showroom lighting, deep contrast, material realism, hero dominance, legible benefits, CTA clarity, and premium commercial impact. Replace the fictional brand, people, showroom, offer, copy, photography, and composition for the client. The visual benchmark is unrecorded.",
            "BRAVA,MOTO",
            "/operations/academy/brava-moto-reference.jpg")
    ];

    public static IReadOnlyList<CurriculumLesson> AllLessons { get; } =
        [.. QualityAnchorLessons, .. PatisserieLessons];

    public static Inspection Judge(
        string? role,
        string? brand,
        string? headline,
        string? body,
        string? properNouns,
        bool creatorFaceObstructed,
        bool? visualBenchmarkMet,
        string? visualNote,
        bool callModel,
        bool campaignReady,
        QualityWeights? weights)
    {
        if (callModel)
        {
            throw new InvalidOperationException("A production model was not called. None was invented.");
        }

        if (campaignReady)
        {
            throw new InvalidOperationException("Campaign ready is refused. This is not a placement.");
        }

        var scale = weights ?? QualityWeights.Initial;
        if (scale.Total != 100 || scale.Typography < 1 || scale.Lighting < 1)
        {
            throw new InvalidOperationException("Quality weights must total 100. None was invented.");
        }

        var kind = (role ?? string.Empty).Trim().ToUpperInvariant();
        if (kind is Reference or MasterReference or SuppliedExample)
        {
            return new Inspection(
                kind,
                null,
                false,
                false,
                [],
                [],
                [],
                0,
                false,
                false,
                "NOT_SENT",
                kind == SuppliedExample
                    ? "This owner-supplied example shows the intended commercial production class. It is not designated as a master prototype. Delivery remains NOT_SENT."
                    : "This prototype teaches the quality floor. It is not an advertiser to clone. Delivery remains NOT_SENT.");
        }

        if (kind != "CANDIDATE")
        {
            throw new InvalidOperationException("The lesson is a prototype or a candidate. None was invented.");
        }

        var defects = new List<string>();
        var repair = new List<string>();
        var text = (headline ?? string.Empty) + "\n" + (body ?? string.Empty);
        var normalized = Fold(text);
        if (normalized.Contains("MAISON FLEUR", StringComparison.Ordinal)
            || normalized.Contains("A SWEETER JOURNEY", StringComparison.Ordinal)
            || normalized.Contains("FRENCH ARTISTRY", StringComparison.Ordinal)
            || normalized.Contains("VIDACARE", StringComparison.Ordinal)
            || normalized.Contains("CARE FOR A BRIGHTER YOU", StringComparison.Ordinal)
            || normalized.Contains("NOVA FIT", StringComparison.Ordinal)
            || normalized.Contains("TU MEJOR VERSION COMIENZA AQUI", StringComparison.Ordinal)
            || normalized.Contains("TALLER RUTA", StringComparison.Ordinal)
            || normalized.Contains("TU AUTO EN BUENAS MANOS", StringComparison.Ordinal)
            || normalized.Contains("FRESHMART", StringComparison.Ordinal)
            || normalized.Contains("BRAVA MOTO", StringComparison.Ordinal)
            || normalized.Contains("TU PROXIMA RUTA EMPIEZA AQUI", StringComparison.Ordinal))
        {
            defects.Add("Prototype identity was copied.");
            repair.Add("Invent a different brand. The prototype is the teacher, not the advertiser.");
        }

        foreach (var word in Duplicates(Tokens(headline + " " + body)))
        {
            defects.Add("Duplicated word: " + word);
            repair.Add("Remove the duplicated word " + word + ".");
        }

        var allowed = new HashSet<string>(Lexicon, StringComparer.Ordinal);
        foreach (var noun in (properNouns ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            allowed.Add(Fold(noun).Replace(" ", "", StringComparison.Ordinal));
        }

        foreach (var token in Tokens(headline + " " + body))
        {
            if (token.Length < 4 || allowed.Contains(token))
            {
                continue;
            }

            defects.Add("Malformed text: " + token);
            repair.Add("Replace the malformed word " + token + ".");
        }

        if (creatorFaceObstructed)
        {
            defects.Add("Creator face obstructed.");
            repair.Add("Clear the creator face. Do not cover the host.");
        }

        var visualRecorded = visualBenchmarkMet is not null;
        if (visualBenchmarkMet == false)
        {
            var note = string.IsNullOrWhiteSpace(visualNote)
                ? "Visual benchmark was not met."
                : visualNote.Trim();
            defects.Add(note);
            repair.Add("Repair the recorded visual defect. Do not restart the advertisement.");
        }

        var critical = defects.Exists(item =>
            item.StartsWith("Duplicated word:", StringComparison.Ordinal)
            || item.StartsWith("Malformed text:", StringComparison.Ordinal)
            || item.StartsWith("Prototype identity", StringComparison.Ordinal)
            || item.StartsWith("Creator face", StringComparison.Ordinal));

        string status;
        int? score;
        if (creatorFaceObstructed || defects.Exists(item => item.StartsWith("Prototype identity", StringComparison.Ordinal)))
        {
            status = Fail;
            score = Score(scale, defects, visualBenchmarkMet);
        }
        else if (critical || visualBenchmarkMet == false)
        {
            status = Revise;
            score = Score(scale, defects, visualBenchmarkMet);
        }
        else if (visualBenchmarkMet == true)
        {
            status = Pass;
            score = 100;
        }
        else
        {
            status = Withheld;
            score = null;
            repair.Add("Record the visual benchmark. A vision model was not called.");
        }

        var notice = status switch
        {
            Pass => "The instructor passed the stored copy and the recorded visual benchmark. A model was not called. Campaign ready is refused. Delivery remains NOT_SENT.",
            Revise => "The instructor returned REVISE. The listed defects are repaired in place. A model was not called. Delivery remains NOT_SENT.",
            Fail => "The instructor returned FAIL. Automatic approval is refused. A model was not called. Delivery remains NOT_SENT.",
            _ => "Text inspection found no critical defect. The visual benchmark is unrecorded. Approval is withheld. A model was not called. Delivery remains NOT_SENT."
        };

        return new Inspection(
            status,
            score,
            critical || creatorFaceObstructed,
            visualRecorded,
            defects,
            status == Pass || status == Reference ? [] : PreserveList,
            repair,
            0,
            false,
            false,
            "NOT_SENT",
            notice);
    }

    public static DnaDecision Choose(
        string? family,
        string? brand,
        string? hero,
        string? palette,
        string? cta,
        string? personality,
        bool callModel)
    {
        if (callModel)
        {
            throw new InvalidOperationException("A production model was not called. None was invented.");
        }

        var lane = (family ?? string.Empty).Trim().ToLowerInvariant();
        if (!KnownFamily(lane))
        {
            throw new InvalidOperationException("A known creative family is required. None was invented.");
        }

        var name = Require("brand", brand);
        var subject = Require("hero", hero);
        var color = Require("palette", palette);
        var action = Require("call to action", cta);
        var tone = Require("personality", personality);
        var folded = Fold(name);
        if (IsProtectedIdentity(folded)
            || Fold(action).Contains("A SWEETER JOURNEY", StringComparison.Ordinal)
            || Fold(action).Contains("CARE FOR A BRIGHTER YOU", StringComparison.Ordinal)
            || Fold(action).Contains("TU PROXIMA RUTA EMPIEZA AQUI", StringComparison.Ordinal)
            || Fold(tone).Contains("FRENCH ARTISTRY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The prototype identity is the teacher. A new advertiser was not cloned.");
        }

        var entry = NicheCatalog.Find(lane);
        var external = entry is { Status: NicheCatalog.Created, AnchorKind: NicheCatalog.ExternalAnchor };
        var teacherKey = external ? string.Empty : TeacherFor(lane);
        var teacher = teacherKey.Length > 0;
        var notice = external
            ? lane + " was already created. The prototype was not repeated and no substitute image was invented. A model was not called. Delivery remains NOT_SENT."
            : teacher
                ? lane + " DNA selects " + teacherKey + " as the quality teacher. The advertiser's approved Brand DNA remains authoritative. The new creative identity stays distinct. A model was not called. Delivery remains NOT_SENT."
                : "This niche is not created yet. No quality anchor was invented. A model was not called. Delivery remains NOT_SENT.";
        return new DnaDecision(
            notice,
            false,
            "NOT_SENT",
            lane,
            name,
            subject,
            color,
            action,
            tone,
            teacherKey,
            teacher,
            true,
            0);
    }

    public static ProductionBrief PrepareProductionBrief(
        string? family,
        string? brand,
        string? palette,
        string? fontFamily,
        string? headline,
        string? cta,
        string? market,
        string? language,
        string? requirements,
        bool marketResearchComplete,
        bool approvedPeopleProvided,
        bool callModel)
    {
        if (callModel)
        {
            throw new InvalidOperationException("A production model is not configured. No advertisement was generated.");
        }

        var lane = (family ?? string.Empty).Trim().ToLowerInvariant();
        if (!KnownFamily(lane))
        {
            throw new InvalidOperationException("A known creative family is required. None was invented.");
        }

        var name = Require("brand", brand);
        var color = Default(palette, "Create an original niche-appropriate palette unless advertiser colors are supplied.");
        var font = Default(fontFamily, "Choose a professional niche-appropriate font unless an approved font is supplied.");
        var title = Default(headline, "Create an original niche-appropriate headline.");
        var action = Default(cta, "Create one clear niche-appropriate call to action.");
        var locale = Default(market, "Unspecified market");
        var copyLanguage = Default(language, "Language to be confirmed");
        var limits = Default(requirements, "No additional advertiser restrictions were supplied.");
        var folded = Fold(name);
        if (IsProtectedIdentity(folded))
        {
            throw new InvalidOperationException("The master prototype is the quality teacher. Its fictional identity was not reused.");
        }

        var entry = NicheCatalog.Find(lane);
        var external = entry is { Status: NicheCatalog.Created, AnchorKind: NicheCatalog.ExternalAnchor };
        var teacherKey = external ? string.Empty : TeacherFor(lane);
        var casting = PlanCasting(locale, marketResearchComplete, approvedPeopleProvided);
        var anchorDirection = external
            ? "This niche was already created. Do not repeat the prototype and do not invent a substitute image."
            : teacherKey.Length == 0
                ? "No approved niche anchor is on file. Stop before image generation. This niche is not created yet."
                : "Use " + teacherKey + " only as the quality anchor.";
        var recipe =
            "Create a completely original wide 16:9 commercial advertisement for " + name +
            " in the " + lane + " niche. " + anchorDirection +
            " Do not copy the reference brand, people, photograph, environment, products, headline, layout, or distinctive creative content. " +
            "Brand direction: " + color + " Typography: " + font + " Headline direction: " + title +
            " CTA direction: " + action + " Market: " + casting.Market + " Language: " + copyLanguage +
            " Requirements: " + limits +
            " Match or exceed the anchor in color power, tonal contrast, lighting craftsmanship, dimensional depth, material realism, texture fidelity, hero dominance, hierarchy, screen pop, typography, commercial polish, and Premium Dominant Presence. Important text must be correctly spelled. Preserve safe placement for the CTA and any QR code.";
        var notice = external
            ? "This niche was already created. The prototype was not repeated and no substitute image was invented. A production model is not configured, so no advertisement was generated. Delivery remains NOT_SENT."
            : teacherKey.Length == 0
                ? "This niche is not created yet. No quality anchor was invented and no production model is configured. No advertisement was generated. Delivery remains NOT_SENT."
                : "The production specification is ready. The anchor fixes the quality floor, not the creative template. The advertiser's brand, palette, font, copy, photography, people, environment, and composition remain customizable. A production model is not configured, so no advertisement was generated. Delivery remains NOT_SENT.";
        var status = external
            ? NicheCatalog.AlreadyCreated
            : teacherKey.Length == 0 ? NicheCatalog.NicheNotCreated : ProductionSpecReady;
        return new ProductionBrief(
            status,
            name,
            lane,
            teacherKey,
            casting.Market,
            copyLanguage,
            color,
            font,
            title,
            action,
            limits,
            recipe,
            casting,
            ReferenceParityGate,
            ReplaceCreativeContent,
            false,
            0,
            false,
            "NOT_SENT",
            notice);
    }

    public static CastingDecision PlanCasting(
        string? market,
        bool marketResearchComplete,
        bool approvedPeopleProvided)
    {
        var locale = Require("market", market);
        var folded = Fold(locale);
        if (folded is "ASIAN" or "ASIA" or "LATINO" or "LATINA" or "LATIN AMERICAN"
            || folded.Contains("GENERIC ASIAN", StringComparison.Ordinal)
            || folded.Contains("GENERIC LATIN", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A specific market is required. Generic regional casting is refused.");
        }

        if (approvedPeopleProvided)
        {
            return new CastingDecision(
                "ADVERTISER_ASSET",
                locale,
                "Use the advertiser-approved employees, spokespersons, models, or photography according to the campaign requirements.",
                false);
        }

        var known = folded is
            "PHILIPPINES" or "MANILA" or
            "MALAYSIA" or "KUALA LUMPUR" or
            "INDONESIA" or "JAKARTA" or
            "COLOMBIA" or "BOGOTA" or
            "PANAMA" or "PANAMA CITY" or
            "DOMINICAN REPUBLIC" or "SANTO DOMINGO";
        if (!known || !marketResearchComplete)
        {
            return new CastingDecision(
                "MARKET_RESEARCH_REQUIRED",
                locale,
                "Research contemporary local commercial context using multiple legitimate references before art direction. Do not infer a single national face or use caricatures.",
                true);
        }

        return new CastingDecision(
            "LOCALLY_PLAUSIBLE_DEFAULT",
            locale,
            "Cast original people who are plausible for the specific market, vary people across campaigns, integrate clothing and environment naturally, and avoid stereotypes. Geographic authenticity does not lower the Alpha photorealism standard.",
            false);
    }

    public static ParityDecision EvaluateFinalGates(
        bool? customizationCompliance,
        bool? qualityParity,
        bool? originality,
        bool? geographicAuthenticity)
    {
        var defects = new List<string>();
        var repair = new List<string>();
        AddGate(customizationCompliance, "Advertiser Brand DNA or customization did not pass.", "Repair the stated brand, color, font, copy, imagery, or restriction mismatch.", defects, repair);
        AddGate(qualityParity, "Reference quality parity did not pass.", "Restore the deficient contrast, lighting, realism, depth, texture, hierarchy, screen pop, or commercial polish.", defects, repair);
        AddGate(originality, "Originality did not pass.", "Replace copied creative content while preserving the quality signature.", defects, repair);
        AddGate(geographicAuthenticity, "Geographic authenticity did not pass.", "Research the specific market and repair casting without caricature or a generic regional model.", defects, repair);

        if (defects.Count > 0)
        {
            return new ParityDecision(
                Revise, defects, repair, false, false, 0, "NOT_SENT",
                "At least one independent gate failed. The candidate is REVISE. Attractive is not enough. Delivery remains NOT_SENT.");
        }

        if (customizationCompliance is null || qualityParity is null || originality is null || geographicAuthenticity is null)
        {
            return new ParityDecision(
                Withheld, [], ["Record all four independent gates."], false, false, 0, "NOT_SENT",
                "One or more gates are unrecorded. Approval is withheld. Delivery remains NOT_SENT.");
        }

        return new ParityDecision(
            Eligible, [], [], true, false, 0, "NOT_SENT",
            "Customization, quality parity, originality, and geographic authenticity passed. The candidate is eligible to continue through the approval workflow. Campaign ready is still false. Delivery remains NOT_SENT.");
    }

    public static bool SameBrand(string? left, string? right) =>
        Fold(left ?? string.Empty) == Fold(right ?? string.Empty) && Fold(left ?? string.Empty).Length > 0;

    private static void AddGate(
        bool? gate,
        string defect,
        string repairInstruction,
        ICollection<string> defects,
        ICollection<string> repair)
    {
        if (gate != false)
        {
            return;
        }

        defects.Add(defect);
        repair.Add(repairInstruction);
    }

    private static bool KnownFamily(string lane) =>
        LegacyFamilies.Contains(lane) || NicheCatalog.Find(lane) is not null;

    private static bool IsProtectedIdentity(string folded) =>
        folded is "MAISON FLEUR" or "MAISONFLEUR"
            or "VIDACARE" or "VIDACARE PHARMACY" or "VIDACAREPHARMACY"
            or "NOVA FIT" or "NOVAFIT"
            or "TALLER RUTA" or "TALLERRUTA"
            or "FRESHMART" or "FRESHMART SUPERMARKET"
            or "BRAVA MOTO" or "BRAVAMOTO";

    private static string TeacherFor(string family) =>
        NicheAnchors.TryGetValue(family, out var teacher) ? teacher : string.Empty;

    private static int Score(QualityWeights weights, IReadOnlyList<string> defects, bool? visualBenchmarkMet)
    {
        var score = 100;
        if (defects.Any(item => item.StartsWith("Duplicated word:", StringComparison.Ordinal) || item.StartsWith("Malformed text:", StringComparison.Ordinal)))
        {
            score -= weights.Typography;
        }

        if (defects.Any(item => item.StartsWith("Prototype identity", StringComparison.Ordinal)))
        {
            score -= weights.Identity;
        }

        if (defects.Any(item => item.StartsWith("Creator face", StringComparison.Ordinal)))
        {
            score -= weights.Creator;
        }

        if (visualBenchmarkMet == false)
        {
            score -= weights.Lighting;
        }

        return Math.Max(0, score);
    }

    private static IEnumerable<string> Duplicates(IReadOnlyList<string> tokens)
    {
        string? previous = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (token.Length >= 4 && previous == token)
            {
                seen.Add(token);
            }

            previous = token;
        }

        return seen;
    }

    private static List<string> Tokens(string? text)
    {
        var folded = Fold(text ?? string.Empty);
        var tokens = new List<string>();
        var buffer = new StringBuilder();
        foreach (var character in folded)
        {
            if (char.IsLetter(character) || character == '\'')
            {
                buffer.Append(character);
                continue;
            }

            Add(tokens, buffer);
        }

        Add(tokens, buffer);
        return tokens;
    }

    private static void Add(List<string> tokens, StringBuilder buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        tokens.Add(buffer.ToString().Replace("'", "", StringComparison.Ordinal));
        buffer.Clear();
    }

    private static string Fold(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    private static string Require(string name, string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length < 3 || text.Length > 120)
        {
            throw new InvalidOperationException("A " + name + " is required. None was invented.");
        }

        return text;
    }

    private static string Default(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }
}
