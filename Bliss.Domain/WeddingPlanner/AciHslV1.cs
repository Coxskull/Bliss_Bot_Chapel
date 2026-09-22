using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Pure deterministic Alpha Color Intelligence algorithm <c>aci.hsl.v1</c>.
/// No AI/provider calls; Brand DNA is provenance only and is never parsed for hues.
/// </summary>
public static class AciHslV1
{
    public const string AlgorithmVersion = WeddingPlannerAlgorithmVersions.AciHslV1;
    public const string SchemaVersion = WeddingPlannerSchemaVersions.ColorProfileV1;

    public const string ContrastDisclaimer =
        "WCAG contrast figures are deterministic arithmetic evidence only and are not a legal or accessibility certification of any rendered UI.";

    public const string GeometryDisclaimer =
        "HSL secondary/accent/neutral offsets are deterministic geometry, not psychological or brand-strategy recommendations.";

    public static readonly string DefaultBackgroundHex = "#FFFFFF";

    private static readonly (string Role, double T)[] NeutralLadder =
    {
        ("neutral50", 0.04),
        ("neutral100", 0.08),
        ("neutral200", 0.16),
        ("neutral400", 0.32),
        ("neutral600", 0.52),
        ("neutral800", 0.72),
        ("neutral900", 0.88)
    };

    private static readonly (string Fg, string Bg)[] EvidencePairs =
    {
        ("onPrimary", "primary"),
        ("onSecondary", "secondary"),
        ("onAccent", "accent"),
        ("onBackground", "background"),
        ("onSurface", "surface"),
        ("neutral900", "background"),
        ("neutral800", "surface"),
        ("primary", "background"),
        ("secondary", "background"),
        ("accent", "background")
    };

    public static string CanonicalizeHex(string? raw)
    {
        if (raw is null)
        {
            throw new InvalidOperationException("Hex color is required.");
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0 || trimmed[0] != '#')
        {
            throw new InvalidOperationException("Hex color must start with '#' and use #RGB or #RRGGBB form.");
        }

        var body = trimmed[1..];
        if (body.Length == 3)
        {
            if (!IsHexNibble(body[0]) || !IsHexNibble(body[1]) || !IsHexNibble(body[2]))
            {
                throw new InvalidOperationException("Hex color must start with '#' and use #RGB or #RRGGBB form.");
            }

            return $"#{char.ToUpperInvariant(body[0])}{char.ToUpperInvariant(body[0])}{char.ToUpperInvariant(body[1])}{char.ToUpperInvariant(body[1])}{char.ToUpperInvariant(body[2])}{char.ToUpperInvariant(body[2])}";
        }

        if (body.Length == 6)
        {
            for (var i = 0; i < 6; i++)
            {
                if (!IsHexNibble(body[i]))
                {
                    throw new InvalidOperationException("Hex color must start with '#' and use #RGB or #RRGGBB form.");
                }
            }

            return $"#{body.ToUpperInvariant()}";
        }

        throw new InvalidOperationException("Hex color must start with '#' and use #RGB or #RRGGBB form.");
    }

    public static (byte R, byte G, byte B) ParseHex(string canonicalHex)
    {
        var hex = CanonicalizeHex(canonicalHex);
        return (
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));
    }

    public static string ToHex(byte r, byte g, byte b) =>
        $"#{r:X2}{g:X2}{b:X2}";

    public static double RelativeLuminance(byte r, byte g, byte b)
    {
        var R = Linearize(r / 255.0);
        var G = Linearize(g / 255.0);
        var B = Linearize(b / 255.0);
        return 0.2126 * R + 0.7152 * G + 0.0722 * B;
    }

    public static double RelativeLuminance(string canonicalHex)
    {
        var (r, g, b) = ParseHex(canonicalHex);
        return RelativeLuminance(r, g, b);
    }

    public static decimal StoredLuminance(string canonicalHex) =>
        Round(RelativeLuminance(canonicalHex), 6);

    public static double ContrastRatio(double luminanceA, double luminanceB)
    {
        var lighter = Math.Max(luminanceA, luminanceB);
        var darker = Math.Min(luminanceA, luminanceB);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static decimal StoredContrastRatio(string foregroundHex, string backgroundHex)
    {
        var ratio = ContrastRatio(RelativeLuminance(foregroundHex), RelativeLuminance(backgroundHex));
        return Round(ratio, 2);
    }

    public static string SelectOnColor(string roleHex)
    {
        var black = StoredContrastRatio("#000000", roleHex);
        var white = StoredContrastRatio("#FFFFFF", roleHex);
        return black >= white ? "#000000" : "#FFFFFF";
    }

    public static (decimal H, decimal S, decimal L) RgbToHslStored(byte r, byte g, byte b)
    {
        var (h, s, l) = RgbToHsl(r, g, b);
        return (Round(h, 1), Round(s, 1), Round(l, 1));
    }

    public static (double H, double S, double L) RgbToHsl(byte r, byte g, byte b)
    {
        var rf = r / 255.0;
        var gf = g / 255.0;
        var bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var l = (max + min) / 2.0 * 100.0;
        double h;
        double s;
        if (AlmostEqual(max, min))
        {
            h = 0;
            s = 0;
        }
        else
        {
            var d = max - min;
            s = (l > 50.0 ? d / (2.0 - max - min) : d / (max + min)) * 100.0;
            if (AlmostEqual(max, rf))
            {
                h = ((gf - bf) / d + (gf < bf ? 6.0 : 0.0)) * 60.0;
            }
            else if (AlmostEqual(max, gf))
            {
                h = ((bf - rf) / d + 2.0) * 60.0;
            }
            else
            {
                h = ((rf - gf) / d + 4.0) * 60.0;
            }
        }

        h %= 360.0;
        if (h < 0)
        {
            h += 360.0;
        }

        return (h, s, l);
    }

    public static (byte R, byte G, byte B) HslToRgb(double h, double sPercent, double lPercent)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        var s = Clamp(sPercent / 100.0, 0, 1);
        var l = Clamp(lPercent / 100.0, 0, 1);

        double r;
        double g;
        double b;
        if (AlmostEqual(s, 0))
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            var hk = h / 360.0;
            r = HueToRgb(p, q, hk + 1.0 / 3.0);
            g = HueToRgb(p, q, hk);
            b = HueToRgb(p, q, hk - 1.0 / 3.0);
        }

        return (
            (byte)RoundToByte(r * 255.0),
            (byte)RoundToByte(g * 255.0),
            (byte)RoundToByte(b * 255.0));
    }

    public static string HslToHex(double h, double sPercent, double lPercent)
    {
        var (r, g, b) = HslToRgb(h, sPercent, lPercent);
        return ToHex(r, g, b);
    }

    public static string MixTowardBlack(string backgroundHex, double t)
    {
        var (r, g, b) = ParseHex(backgroundHex);
        return ToHex(
            (byte)RoundToByte(r + (0 - r) * t),
            (byte)RoundToByte(g + (0 - g) * t),
            (byte)RoundToByte(b + (0 - b) * t));
    }

    public static ColorProfileComputeResult Compute(
        string primaryHex,
        string? secondaryHex,
        string? accentHex,
        string? backgroundHex,
        string? surfaceHex,
        string? notes,
        Guid approvedBrandDnaVersionId,
        int approvedBrandDnaVersionNumber)
    {
        var primary = CanonicalizeHex(primaryHex);
        var secondaryDerived = string.IsNullOrWhiteSpace(secondaryHex);
        var accentDerived = string.IsNullOrWhiteSpace(accentHex);
        var backgroundDefaulted = string.IsNullOrWhiteSpace(backgroundHex);
        var surfaceDefaulted = string.IsNullOrWhiteSpace(surfaceHex);

        var (pr, pg, pb) = ParseHex(primary);
        var (ph, ps, pl) = RgbToHsl(pr, pg, pb);

        var secondary = secondaryDerived
            ? HslToHex(ph + 180.0, ps, pl)
            : CanonicalizeHex(secondaryHex);
        var accent = accentDerived
            ? HslToHex(ph + 30.0, ps, pl)
            : CanonicalizeHex(accentHex);
        var background = backgroundDefaulted
            ? DefaultBackgroundHex
            : CanonicalizeHex(backgroundHex);
        var surface = surfaceDefaulted
            ? background
            : CanonicalizeHex(surfaceHex);

        var notesValue = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (notesValue is { Length: > 2000 })
        {
            throw new InvalidOperationException("Notes cannot exceed 2000 characters.");
        }

        var paletteHex = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["primary"] = primary,
            ["secondary"] = secondary,
            ["accent"] = accent,
            ["background"] = background,
            ["surface"] = surface
        };

        paletteHex["onPrimary"] = SelectOnColor(primary);
        paletteHex["onSecondary"] = SelectOnColor(secondary);
        paletteHex["onAccent"] = SelectOnColor(accent);
        paletteHex["onBackground"] = SelectOnColor(background);
        paletteHex["onSurface"] = SelectOnColor(surface);

        foreach (var (role, t) in NeutralLadder)
        {
            paletteHex[role] = MixTowardBlack(background, t);
        }

        var pairs = new List<ColorProfileContrastPairV1>(EvidencePairs.Length);
        foreach (var (fgRole, bgRole) in EvidencePairs)
        {
            var fgHex = paletteHex[fgRole];
            var bgHex = paletteHex[bgRole];
            var ratio = StoredContrastRatio(fgHex, bgHex);
            pairs.Add(new ColorProfileContrastPairV1(
                fgRole,
                bgRole,
                fgHex,
                bgHex,
                ratio,
                ratio >= 4.50m,
                ratio >= 3.00m,
                ratio >= 7.00m,
                ratio >= 4.50m));
        }

        var document = new ColorProfileDocumentV1(
            SchemaVersion,
            AlgorithmVersion,
            new ColorProfileProvenanceV1(approvedBrandDnaVersionId, approvedBrandDnaVersionNumber),
            new ColorProfileSeedsV1(
                primary,
                secondary,
                accent,
                background,
                surface,
                secondaryDerived,
                accentDerived,
                backgroundDefaulted,
                surfaceDefaulted,
                notesValue),
            BuildPalette(paletteHex),
            new ColorProfileContrastEvidenceV1(ContrastDisclaimer, pairs),
            GeometryDisclaimer);

        var documentJson = SerializeDocument(document);
        var inputJson = SerializeInput(
            approvedBrandDnaVersionId,
            primary,
            secondary,
            accent,
            background,
            surface,
            secondaryDerived,
            accentDerived,
            backgroundDefaulted,
            surfaceDefaulted);
        var inputSha256 = Sha256LowerHex(inputJson);

        var summary =
            $"{SchemaVersion} / {AlgorithmVersion} primary={primary} " +
            $"secondary={(secondaryDerived ? "derived" : "supplied")} " +
            $"accent={(accentDerived ? "derived" : "supplied")}";

        return new ColorProfileComputeResult(documentJson, summary, inputJson, inputSha256, document);
    }

    private static ColorProfilePaletteV1 BuildPalette(IReadOnlyDictionary<string, string> paletteHex) =>
        new(
            RoleSwatch(paletteHex["primary"]),
            RoleSwatch(paletteHex["secondary"]),
            RoleSwatch(paletteHex["accent"]),
            RoleSwatch(paletteHex["background"]),
            RoleSwatch(paletteHex["surface"]),
            new ColorProfileHexOnlyV1(paletteHex["onPrimary"]),
            new ColorProfileHexOnlyV1(paletteHex["onSecondary"]),
            new ColorProfileHexOnlyV1(paletteHex["onAccent"]),
            new ColorProfileHexOnlyV1(paletteHex["onBackground"]),
            new ColorProfileHexOnlyV1(paletteHex["onSurface"]),
            new ColorProfileHexOnlyV1(paletteHex["neutral50"]),
            new ColorProfileHexOnlyV1(paletteHex["neutral100"]),
            new ColorProfileHexOnlyV1(paletteHex["neutral200"]),
            new ColorProfileHexOnlyV1(paletteHex["neutral400"]),
            new ColorProfileHexOnlyV1(paletteHex["neutral600"]),
            new ColorProfileHexOnlyV1(paletteHex["neutral800"]),
            new ColorProfileHexOnlyV1(paletteHex["neutral900"]));

    private static ColorProfileRoleSwatchV1 RoleSwatch(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        var (h, s, l) = RgbToHslStored(r, g, b);
        return new ColorProfileRoleSwatchV1(hex, new ColorProfileHslV1(h, s, l), StoredLuminance(hex));
    }

    private static string SerializeDocument(ColorProfileDocumentV1 document)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", document.SchemaVersion);
            writer.WriteString("algorithmVersion", document.AlgorithmVersion);

            writer.WritePropertyName("provenance");
            writer.WriteStartObject();
            writer.WriteString("approvedBrandDnaVersionId", document.Provenance.ApprovedBrandDnaVersionId);
            writer.WriteNumber("approvedBrandDnaVersionNumber", document.Provenance.ApprovedBrandDnaVersionNumber);
            writer.WriteEndObject();

            writer.WritePropertyName("seeds");
            writer.WriteStartObject();
            writer.WriteString("primaryHex", document.Seeds.PrimaryHex);
            writer.WriteString("secondaryHex", document.Seeds.SecondaryHex);
            writer.WriteString("accentHex", document.Seeds.AccentHex);
            writer.WriteString("backgroundHex", document.Seeds.BackgroundHex);
            writer.WriteString("surfaceHex", document.Seeds.SurfaceHex);
            writer.WriteBoolean("secondaryDerived", document.Seeds.SecondaryDerived);
            writer.WriteBoolean("accentDerived", document.Seeds.AccentDerived);
            writer.WriteBoolean("backgroundDefaulted", document.Seeds.BackgroundDefaulted);
            writer.WriteBoolean("surfaceDefaulted", document.Seeds.SurfaceDefaulted);
            if (document.Seeds.Notes is null)
            {
                writer.WriteNull("notes");
            }
            else
            {
                writer.WriteString("notes", document.Seeds.Notes);
            }

            writer.WriteEndObject();

            writer.WritePropertyName("palette");
            writer.WriteStartObject();
            WriteRoleSwatch(writer, "primary", document.Palette.Primary);
            WriteRoleSwatch(writer, "secondary", document.Palette.Secondary);
            WriteRoleSwatch(writer, "accent", document.Palette.Accent);
            WriteRoleSwatch(writer, "background", document.Palette.Background);
            WriteRoleSwatch(writer, "surface", document.Palette.Surface);
            WriteHexOnly(writer, "onPrimary", document.Palette.OnPrimary);
            WriteHexOnly(writer, "onSecondary", document.Palette.OnSecondary);
            WriteHexOnly(writer, "onAccent", document.Palette.OnAccent);
            WriteHexOnly(writer, "onBackground", document.Palette.OnBackground);
            WriteHexOnly(writer, "onSurface", document.Palette.OnSurface);
            WriteHexOnly(writer, "neutral50", document.Palette.Neutral50);
            WriteHexOnly(writer, "neutral100", document.Palette.Neutral100);
            WriteHexOnly(writer, "neutral200", document.Palette.Neutral200);
            WriteHexOnly(writer, "neutral400", document.Palette.Neutral400);
            WriteHexOnly(writer, "neutral600", document.Palette.Neutral600);
            WriteHexOnly(writer, "neutral800", document.Palette.Neutral800);
            WriteHexOnly(writer, "neutral900", document.Palette.Neutral900);
            writer.WriteEndObject();

            writer.WritePropertyName("contrastEvidence");
            writer.WriteStartObject();
            writer.WriteString("disclaimer", document.ContrastEvidence.Disclaimer);
            writer.WritePropertyName("pairs");
            writer.WriteStartArray();
            foreach (var pair in document.ContrastEvidence.Pairs)
            {
                writer.WriteStartObject();
                writer.WriteString("foregroundRole", pair.ForegroundRole);
                writer.WriteString("backgroundRole", pair.BackgroundRole);
                writer.WriteString("foregroundHex", pair.ForegroundHex);
                writer.WriteString("backgroundHex", pair.BackgroundHex);
                WriteFixed(writer, "ratio", pair.Ratio, 2);
                writer.WriteBoolean("aaNormal", pair.AaNormal);
                writer.WriteBoolean("aaLarge", pair.AaLarge);
                writer.WriteBoolean("aaaNormal", pair.AaaNormal);
                writer.WriteBoolean("aaaLarge", pair.AaaLarge);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteString("geometryDisclaimer", document.GeometryDisclaimer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteRoleSwatch(Utf8JsonWriter writer, string name, ColorProfileRoleSwatchV1 swatch)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("hex", swatch.Hex);
        writer.WritePropertyName("hsl");
        writer.WriteStartObject();
        WriteFixed(writer, "h", swatch.Hsl.H, 1);
        WriteFixed(writer, "s", swatch.Hsl.S, 1);
        WriteFixed(writer, "l", swatch.Hsl.L, 1);
        writer.WriteEndObject();
        WriteFixed(writer, "luminance", swatch.Luminance, 6);
        writer.WriteEndObject();
    }

    private static void WriteHexOnly(Utf8JsonWriter writer, string name, ColorProfileHexOnlyV1 value)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("hex", value.Hex);
        writer.WriteEndObject();
    }

    private static void WriteFixed(Utf8JsonWriter writer, string name, decimal value, int places)
    {
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString($"F{places}", CultureInfo.InvariantCulture));
    }

    private static string SerializeInput(
        Guid approvedBrandDnaVersionId,
        string primary,
        string secondary,
        string accent,
        string background,
        string surface,
        bool secondaryDerived,
        bool accentDerived,
        bool backgroundDefaulted,
        bool surfaceDefaulted)
    {
        // Alphabetical camelCase keys; Notes intentionally excluded from hash input.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("accentDerived", accentDerived);
            writer.WriteString("accentHex", accent);
            writer.WriteString("algorithmVersion", AlgorithmVersion);
            writer.WriteString("approvedBrandDnaVersionId", approvedBrandDnaVersionId);
            writer.WriteBoolean("backgroundDefaulted", backgroundDefaulted);
            writer.WriteString("backgroundHex", background);
            writer.WriteString("primaryHex", primary);
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WriteBoolean("secondaryDerived", secondaryDerived);
            writer.WriteString("secondaryHex", secondary);
            writer.WriteBoolean("surfaceDefaulted", surfaceDefaulted);
            writer.WriteString("surfaceHex", surface);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Sha256LowerHex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static double Linearize(double cSrgb) =>
        cSrgb <= 0.04045 ? cSrgb / 12.92 : Math.Pow((cSrgb + 0.055) / 1.055, 2.4);

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6.0)
        {
            return p + (q - p) * 6.0 * t;
        }

        if (t < 1.0 / 2.0)
        {
            return q;
        }

        if (t < 2.0 / 3.0)
        {
            return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        }

        return p;
    }

    private static decimal Round(double value, int decimals) =>
        Math.Round((decimal)value, decimals, MidpointRounding.AwayFromZero);

    private static int RoundToByte(double value) =>
        (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    private static bool AlmostEqual(double a, double b) =>
        Math.Abs(a - b) < 1e-12;

    private static bool IsHexNibble(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}

public sealed record ColorProfileComputeResult(
    string DocumentJson,
    string Summary,
    string InputJson,
    string InputSha256,
    ColorProfileDocumentV1 Document);

public sealed record ColorProfileDocumentV1(
    string SchemaVersion,
    string AlgorithmVersion,
    ColorProfileProvenanceV1 Provenance,
    ColorProfileSeedsV1 Seeds,
    ColorProfilePaletteV1 Palette,
    ColorProfileContrastEvidenceV1 ContrastEvidence,
    string GeometryDisclaimer);

public sealed record ColorProfileProvenanceV1(
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber);

public sealed record ColorProfileSeedsV1(
    string PrimaryHex,
    string SecondaryHex,
    string AccentHex,
    string BackgroundHex,
    string SurfaceHex,
    bool SecondaryDerived,
    bool AccentDerived,
    bool BackgroundDefaulted,
    bool SurfaceDefaulted,
    string? Notes);

public sealed record ColorProfilePaletteV1(
    ColorProfileRoleSwatchV1 Primary,
    ColorProfileRoleSwatchV1 Secondary,
    ColorProfileRoleSwatchV1 Accent,
    ColorProfileRoleSwatchV1 Background,
    ColorProfileRoleSwatchV1 Surface,
    ColorProfileHexOnlyV1 OnPrimary,
    ColorProfileHexOnlyV1 OnSecondary,
    ColorProfileHexOnlyV1 OnAccent,
    ColorProfileHexOnlyV1 OnBackground,
    ColorProfileHexOnlyV1 OnSurface,
    ColorProfileHexOnlyV1 Neutral50,
    ColorProfileHexOnlyV1 Neutral100,
    ColorProfileHexOnlyV1 Neutral200,
    ColorProfileHexOnlyV1 Neutral400,
    ColorProfileHexOnlyV1 Neutral600,
    ColorProfileHexOnlyV1 Neutral800,
    ColorProfileHexOnlyV1 Neutral900);

public sealed record ColorProfileRoleSwatchV1(
    string Hex,
    ColorProfileHslV1 Hsl,
    decimal Luminance);

public sealed record ColorProfileHslV1(decimal H, decimal S, decimal L);

public sealed record ColorProfileHexOnlyV1(string Hex);

public sealed record ColorProfileContrastEvidenceV1(
    string Disclaimer,
    IReadOnlyList<ColorProfileContrastPairV1> Pairs);

public sealed record ColorProfileContrastPairV1(
    string ForegroundRole,
    string BackgroundRole,
    string ForegroundHex,
    string BackgroundHex,
    decimal Ratio,
    bool AaNormal,
    bool AaLarge,
    bool AaaNormal,
    bool AaaLarge);
