using System.Text.Json;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class AciHslV1AlgorithmTests
{
    private static readonly Guid BrandDnaId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Theory]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("#AbC", "#AABBCC")]
    [InlineData("#a1b2c3", "#A1B2C3")]
    [InlineData("  #ff00aa  ", "#FF00AA")]
    public void Canonicalizes_hex_forms(string input, string expected) =>
        Assert.Equal(expected, AciHslV1.CanonicalizeHex(input));

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("rgb(0,0,0)")]
    [InlineData("#gg0000")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#AABBCCDD")]
    [InlineData("FF0000")]
    public void Rejects_invalid_hex(string input) =>
        Assert.Throws<InvalidOperationException>(() => AciHslV1.CanonicalizeHex(input));

    [Fact]
    public void Black_on_white_contrast_is_21_and_white_on_white_is_1()
    {
        Assert.Equal(21.00m, AciHslV1.StoredContrastRatio("#000000", "#FFFFFF"));
        Assert.Equal(1.00m, AciHslV1.StoredContrastRatio("#FFFFFF", "#FFFFFF"));
        Assert.Equal(0.000000m, AciHslV1.StoredLuminance("#000000"));
        Assert.Equal(1.000000m, AciHslV1.StoredLuminance("#FFFFFF"));
    }

    [Fact]
    public void On_color_picks_higher_contrast_and_ties_to_black()
    {
        Assert.Equal("#000000", AciHslV1.SelectOnColor("#FFFFFF"));
        Assert.Equal("#FFFFFF", AciHslV1.SelectOnColor("#000000"));

        // Construct a mid luminance where stored ratios can tie: use #777777 empirically.
        var mid = "#808080";
        var black = AciHslV1.StoredContrastRatio("#000000", mid);
        var white = AciHslV1.StoredContrastRatio("#FFFFFF", mid);
        var on = AciHslV1.SelectOnColor(mid);
        if (black == white)
        {
            Assert.Equal("#000000", on);
        }
        else
        {
            Assert.Equal(black > white ? "#000000" : "#FFFFFF", on);
        }
    }

    [Fact]
    public void Omitted_secondary_and_accent_follow_hue_offsets_with_same_sl()
    {
        var primary = "#E11D48";
        var (r, g, b) = AciHslV1.ParseHex(primary);
        var (h, s, l) = AciHslV1.RgbToHsl(r, g, b);
        var expectedSecondary = AciHslV1.HslToHex(h + 180.0, s, l);
        var expectedAccent = AciHslV1.HslToHex(h + 30.0, s, l);

        var result = AciHslV1.Compute(primary, null, null, null, null, null, BrandDnaId, 1);
        Assert.Equal(expectedSecondary, result.Document.Seeds.SecondaryHex);
        Assert.Equal(expectedAccent, result.Document.Seeds.AccentHex);
        Assert.True(result.Document.Seeds.SecondaryDerived);
        Assert.True(result.Document.Seeds.AccentDerived);

        var (sr, sg, sb) = AciHslV1.ParseHex(result.Document.Seeds.SecondaryHex);
        var (_, ss, sl) = AciHslV1.RgbToHslStored(sr, sg, sb);
        var (pr, pg, pb) = AciHslV1.ParseHex(primary);
        var (_, psStored, plStored) = AciHslV1.RgbToHslStored(pr, pg, pb);
        Assert.Equal(psStored, ss);
        Assert.Equal(plStored, sl);
    }

    [Fact]
    public void Red_seed_has_stable_phase3_golden_palette()
    {
        var result = AciHslV1.Compute("#FF0000", null, null, null, null, null, BrandDnaId, 1);

        Assert.Equal("#00FFFF", result.Document.Seeds.SecondaryHex);
        Assert.Equal("#FF8000", result.Document.Seeds.AccentHex);
        Assert.Equal("#F5F5F5", result.Document.Palette.Neutral50.Hex);
        Assert.Equal("#1F1F1F", result.Document.Palette.Neutral900.Hex);
        Assert.Equal("#000000", result.Document.Palette.OnPrimary.Hex);
        Assert.Equal("#000000", result.Document.Palette.OnBackground.Hex);
    }

    [Fact]
    public void Background_defaults_to_white_and_surface_defaults_to_background()
    {
        var result = AciHslV1.Compute("#336699", null, null, null, null, null, BrandDnaId, 2);
        Assert.Equal("#FFFFFF", result.Document.Seeds.BackgroundHex);
        Assert.Equal("#FFFFFF", result.Document.Seeds.SurfaceHex);
        Assert.True(result.Document.Seeds.BackgroundDefaulted);
        Assert.True(result.Document.Seeds.SurfaceDefaulted);

        var customBg = AciHslV1.Compute("#336699", null, null, "#F5F5F5", null, null, BrandDnaId, 2);
        Assert.Equal("#F5F5F5", customBg.Document.Seeds.BackgroundHex);
        Assert.Equal("#F5F5F5", customBg.Document.Seeds.SurfaceHex);
        Assert.False(customBg.Document.Seeds.BackgroundDefaulted);
        Assert.True(customBg.Document.Seeds.SurfaceDefaulted);
    }

    [Fact]
    public void Neutral_ladder_matches_channel_mix_percentages()
    {
        var bg = "#FFFFFF";
        Assert.Equal(AciHslV1.MixTowardBlack(bg, 0.04), AciHslV1.Compute("#0000FF", null, null, bg, bg, null, BrandDnaId, 1).Document.Palette.Neutral50.Hex);
        Assert.Equal(AciHslV1.MixTowardBlack(bg, 0.08), AciHslV1.Compute("#0000FF", null, null, bg, bg, null, BrandDnaId, 1).Document.Palette.Neutral100.Hex);
        Assert.Equal(AciHslV1.MixTowardBlack(bg, 0.16), AciHslV1.Compute("#0000FF", null, null, bg, bg, null, BrandDnaId, 1).Document.Palette.Neutral200.Hex);
        Assert.Equal(AciHslV1.MixTowardBlack(bg, 0.32), AciHslV1.Compute("#0000FF", null, null, bg, bg, null, BrandDnaId, 1).Document.Palette.Neutral400.Hex);
        Assert.Equal(AciHslV1.MixTowardBlack(bg, 0.52), AciHslV1.Compute("#0000FF", null, null, bg, bg, null, BrandDnaId, 1).Document.Palette.Neutral600.Hex);
        Assert.Equal(AciHslV1.MixTowardBlack(bg, 0.72), AciHslV1.Compute("#0000FF", null, null, bg, bg, null, BrandDnaId, 1).Document.Palette.Neutral800.Hex);
        Assert.Equal(AciHslV1.MixTowardBlack(bg, 0.88), AciHslV1.Compute("#0000FF", null, null, bg, bg, null, BrandDnaId, 1).Document.Palette.Neutral900.Hex);
        Assert.Equal("#F5F5F5", AciHslV1.MixTowardBlack("#FFFFFF", 0.04));
    }

    [Fact]
    public void Identical_canonical_seeds_produce_identical_document_and_sha()
    {
        var a = AciHslV1.Compute("#c00", null, null, null, null, null, BrandDnaId, 3);
        var b = AciHslV1.Compute("#CC0000", null, null, null, null, null, BrandDnaId, 3);
        Assert.Equal(a.DocumentJson, b.DocumentJson);
        Assert.Equal(a.InputSha256, b.InputSha256);
        Assert.Equal(a.InputJson, b.InputJson);
        Assert.Equal(64, a.InputSha256.Length);
        Assert.Equal(a.InputSha256, a.InputSha256.ToLowerInvariant());

        var supplied = AciHslV1.Compute("#CC0000", "#00CCCC", "#FF6600", "#FFFFFF", "#FFFFFF", null, BrandDnaId, 3);
        var suppliedAgain = AciHslV1.Compute("#cc0000", "#00cccc", "#ff6600", "#ffffff", "#ffffff", null, BrandDnaId, 3);
        Assert.Equal(supplied.DocumentJson, suppliedAgain.DocumentJson);
        Assert.Equal(supplied.InputSha256, suppliedAgain.InputSha256);
        Assert.NotEqual(a.InputSha256, supplied.InputSha256);
    }

    [Fact]
    public void Notes_change_does_not_change_sha_but_persists_in_document()
    {
        var without = AciHslV1.Compute("#112233", "#445566", null, null, null, null, BrandDnaId, 4);
        var withNotes = AciHslV1.Compute("#112233", "#445566", null, null, null, "Keep warm", BrandDnaId, 4);
        Assert.Equal(without.InputSha256, withNotes.InputSha256);
        Assert.Equal(without.InputJson, withNotes.InputJson);
        Assert.DoesNotContain("notes", without.InputJson, StringComparison.OrdinalIgnoreCase);
        Assert.Null(without.Document.Seeds.Notes);
        Assert.Equal("Keep warm", withNotes.Document.Seeds.Notes);
        Assert.Contains("\"notes\":\"Keep warm\"", withNotes.DocumentJson);
        Assert.Contains("\"notes\":null", without.DocumentJson);
        Assert.NotEqual(without.DocumentJson, withNotes.DocumentJson);
    }

    [Fact]
    public void Wcag_flags_follow_stored_two_decimal_thresholds_and_disclaimers_are_present()
    {
        var result = AciHslV1.Compute("#000000", null, null, "#FFFFFF", null, null, BrandDnaId, 1);
        var onPrimary = result.Document.ContrastEvidence.Pairs.Single(x => x.ForegroundRole == "onPrimary");
        Assert.Equal(21.00m, onPrimary.Ratio);
        Assert.True(onPrimary.AaNormal);
        Assert.True(onPrimary.AaLarge);
        Assert.True(onPrimary.AaaNormal);
        Assert.True(onPrimary.AaaLarge);
        Assert.Equal(AciHslV1.ContrastDisclaimer, result.Document.ContrastEvidence.Disclaimer);
        Assert.Equal(AciHslV1.GeometryDisclaimer, result.Document.GeometryDisclaimer);
        Assert.Contains(AciHslV1.ContrastDisclaimer, result.DocumentJson);
        Assert.Contains(AciHslV1.GeometryDisclaimer, result.DocumentJson);

        using var doc = JsonDocument.Parse(result.DocumentJson);
        Assert.Equal("color-profile.v1", doc.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("aci.hsl.v1", doc.RootElement.GetProperty("algorithmVersion").GetString());
        Assert.Equal(10, doc.RootElement.GetProperty("contrastEvidence").GetProperty("pairs").GetArrayLength());
    }

    [Fact]
    public void Determinism_is_stable_across_repeated_computes()
    {
        var first = AciHslV1.Compute("#4A90E2", "#50E3C2", "#F5A623", "#FAFAFA", "#FFFFFF", "n", BrandDnaId, 9);
        for (var i = 0; i < 5; i++)
        {
            var again = AciHslV1.Compute("#4A90E2", "#50E3C2", "#F5A623", "#FAFAFA", "#FFFFFF", "n", BrandDnaId, 9);
            Assert.Equal(first.DocumentJson, again.DocumentJson);
            Assert.Equal(first.InputSha256, again.InputSha256);
            Assert.Equal(first.Summary, again.Summary);
        }
    }
}
