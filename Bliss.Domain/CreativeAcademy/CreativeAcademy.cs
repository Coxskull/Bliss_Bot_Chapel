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

/// <summary>
/// The prototype teaches quality. Creative DNA customizes the advertiser.
/// The instructor judges stored copy. A model is not called.
/// </summary>
public static class CreativeAcademy
{
    public const string Reference = "REFERENCE";
    public const string Pass = "PASS";
    public const string Revise = "REVISE";
    public const string Fail = "FAIL";
    public const string Withheld = "WITHHELD";
    public const string PatisserieTeacher = "maison-fleur";

    public const string Notice =
        "The prototype teaches the quality. The recipe teaches the craft. Creative DNA customizes the advertiser. The instructor judges the stored copy. A vision model is not configured. A production model was not called. Campaign ready is refused. Green does not send. Delivery remains NOT_SENT.";

    private static readonly string[] PreserveList =
    [
        "composition",
        "hero dessert",
        "hosts",
        "brand identity",
        "call to action",
        "supporting imagery"
    ];

    private static readonly HashSet<string> Families = new(StringComparer.OrdinalIgnoreCase)
    {
        "patisserie",
        "restaurant",
        "hotel",
        "automotive",
        "motorcycle",
        "fitness",
        "dental",
        "medical",
        "beauty",
        "real-estate",
        "grocery",
        "coffee",
        "education",
        "financial"
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
        if (kind == Reference)
        {
            return new Inspection(
                Reference,
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
                "This prototype teaches the quality floor. It is not an advertiser to clone. Delivery remains NOT_SENT.");
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
            || normalized.Contains("FRENCH ARTISTRY", StringComparison.Ordinal))
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
        if (!Families.Contains(lane))
        {
            throw new InvalidOperationException("A known creative family is required. None was invented.");
        }

        var name = Require("brand", brand);
        var subject = Require("hero", hero);
        var color = Require("palette", palette);
        var action = Require("call to action", cta);
        var tone = Require("personality", personality);
        var folded = Fold(name);
        if (folded is "MAISON FLEUR" or "MAISONFLEUR"
            || Fold(action).Contains("A SWEETER JOURNEY", StringComparison.Ordinal)
            || Fold(tone).Contains("FRENCH ARTISTRY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The prototype identity is the teacher. A new advertiser was not cloned.");
        }

        var teacher = lane == "patisserie";
        var notice = teacher
            ? "Patisserie DNA selects the Maison Fleur prototype as the quality teacher. The new brand stays distinct. A model was not called. Delivery remains NOT_SENT."
            : "No prototype is on file for this family. None was invented. A model was not called. Delivery remains NOT_SENT.";
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
            teacher ? PatisserieTeacher : string.Empty,
            teacher,
            true,
            0);
    }

    public static bool SameBrand(string? left, string? right) =>
        Fold(left ?? string.Empty) == Fold(right ?? string.Empty) && Fold(left ?? string.Empty).Length > 0;

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
}
