using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

public sealed class CreativeAcademyTests
{
    [Fact]
    public void The_prototype_teaches_quality_and_is_not_an_advertiser()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "maison-fleur");
        var inspection = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, null, null, false, false, null);

        Assert.Equal(CreativeAcademy.Reference, inspection.Status);
        Assert.Equal(0, inspection.ModelCalls);
        Assert.False(inspection.CampaignReady);
        Assert.Equal("NOT_SENT", inspection.Delivery);
        Assert.Contains("not an advertiser", inspection.Notice);
    }

    [Fact]
    public void A_duplicated_headline_word_is_revise_and_preserves_the_rest()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "lamour-sucre");
        var inspection = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, false, false, null);

        Assert.Equal(CreativeAcademy.Revise, inspection.Status);
        Assert.Contains("Duplicated word: ESCAPE", inspection.Defects);
        Assert.Contains("hero dessert", inspection.Preserve);
        Assert.Contains("Remove the duplicated word ESCAPE.", inspection.Repair);
        Assert.Equal(84, inspection.Score);
        Assert.Equal(0, inspection.ModelCalls);
        Assert.False(inspection.GreenMeansSend);
    }

    [Fact]
    public void Malformed_product_labels_are_revise()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "solara");
        var inspection = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, false, false, null);

        Assert.Equal(CreativeAcademy.Revise, inspection.Status);
        Assert.Contains("Malformed text: CURDJ", inspection.Defects);
        Assert.Contains("Malformed text: MJER", inspection.Defects);
        Assert.DoesNotContain(inspection.Defects, item => item.Contains("LEMON", StringComparison.Ordinal));
    }

    [Fact]
    public void Clear_text_stays_withheld_until_a_visual_benchmark_is_recorded()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "belmonte");
        var withheld = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, null, null, false, false, null);
        var passed = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, false, false, null);

        Assert.Equal(CreativeAcademy.Withheld, withheld.Status);
        Assert.Null(withheld.Score);
        Assert.Contains("unrecorded", withheld.Notice);
        Assert.Equal(CreativeAcademy.Pass, passed.Status);
        Assert.Equal(100, passed.Score);
        Assert.False(passed.CampaignReady);
    }

    [Fact]
    public void A_critical_text_defect_overrides_a_met_visual_benchmark()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "lamour-sucre");
        var inspection = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, false, false, QualityWeights.Initial);

        Assert.Equal(CreativeAcademy.Revise, inspection.Status);
        Assert.NotEqual(CreativeAcademy.Pass, inspection.Status);
    }

    [Fact]
    public void Copying_the_prototype_identity_fails()
    {
        var inspection = CreativeAcademy.Judge(
            "CANDIDATE",
            "Another Name",
            "A sweeter journey awaits you.",
            "French artistry. Reserve your table.",
            "OTHER",
            false,
            true,
            null,
            false,
            false,
            null);

        Assert.Equal(CreativeAcademy.Fail, inspection.Status);
        Assert.Contains("Prototype identity was copied.", inspection.Defects);

        var fitnessCopy = CreativeAcademy.Judge(
            "CANDIDATE",
            "Different Club",
            "Tu mejor versión comienza aquí.",
            "Nova Fit membership. Join today.",
            "DIFFERENT,CLUB",
            false,
            true,
            null,
            false,
            false,
            null);
        Assert.Equal(CreativeAcademy.Fail, fitnessCopy.Status);
        Assert.Contains("Prototype identity was copied.", fitnessCopy.Defects);
    }

    [Fact]
    public void An_obstructed_creator_face_fails()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "belmonte");
        var inspection = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            true, true, null, false, false, null);

        Assert.Equal(CreativeAcademy.Fail, inspection.Status);
        Assert.Contains("Creator face obstructed.", inspection.Defects);
    }

    [Fact]
    public void Weights_are_configurable_and_must_total_100()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "lamour-sucre");
        var heavier = new QualityWeights(20, 8, 12, 12, 10, 10, 10, 10, 8);
        var inspection = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, false, false, heavier);

        Assert.Equal(80, inspection.Score);
        Assert.Throws<InvalidOperationException>(() => CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, false, false, new QualityWeights(50, 12, 12, 12, 10, 10, 10, 10, 8)));
    }

    [Fact]
    public void A_model_call_and_campaign_ready_are_refused()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "belmonte");
        var model = Assert.Throws<InvalidOperationException>(() => CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, true, false, null));
        var campaign = Assert.Throws<InvalidOperationException>(() => CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, true, null, false, true, null));

        Assert.Contains("not called", model.Message);
        Assert.Contains("Campaign ready is refused", campaign.Message);
    }

    [Fact]
    public void Patisserie_dna_selects_the_teacher_and_a_clone_is_refused()
    {
        var dna = CreativeAcademy.Choose(
            "patisserie",
            "Atelier Cendre",
            "Burnt honey mille-feuille",
            "Ink and apricot",
            "Reserve your table",
            "Quiet coastal luxury",
            false);
        var dental = CreativeAcademy.Choose(
            "dental",
            "North Clinic",
            "A calm consultation",
            "Stone and sage",
            "Book a visit",
            "Precise and warm",
            false);
        var clone = Assert.Throws<InvalidOperationException>(() => CreativeAcademy.Choose(
            "patisserie", "Maison Fleur", "Cake", "Gold", "Reserve your table", "Luxury", false));

        Assert.True(dna.TeacherOnFile);
        Assert.Equal(CreativeAcademy.PatisserieTeacher, dna.TeacherKey);
        Assert.True(dna.Distinct);
        Assert.False(dental.TeacherOnFile);
        Assert.Equal(string.Empty, dental.TeacherKey);
        Assert.Contains("None was invented", dental.Notice);
        Assert.Contains("not cloned", clone.Message);
        Assert.False(CreativeAcademy.SameBrand(dna.BrandName, "Solara"));
        Assert.Equal(0, dna.ModelCalls);
        Assert.Equal("NOT_SENT", dna.Delivery);
    }

    [Fact]
    public void A_weaker_recorded_contrast_note_revises_without_erasing_the_text_defect()
    {
        var lesson = CreativeAcademy.PatisserieLessons.Single(item => item.LessonKey == "lamour-sucre");
        var inspection = CreativeAcademy.Judge(
            lesson.Role, lesson.BrandName, lesson.Headline, lesson.Body, lesson.ProperNouns,
            false, false, "Weaker visual contrast.", false, false, null);

        Assert.Equal(CreativeAcademy.Revise, inspection.Status);
        Assert.Contains("Duplicated word: ESCAPE", inspection.Defects);
        Assert.Contains("Weaker visual contrast.", inspection.Defects);
        Assert.Equal(72, inspection.Score);
    }

    [Fact]
    public void VidaCare_is_master_prototype_01_and_FreshMart_stays_a_supplied_example()
    {
        var pharmacy = CreativeAcademy.AllLessons.Single(item => item.LessonKey == "vidacare-master-01");
        var grocery = CreativeAcademy.AllLessons.Single(item => item.LessonKey == "freshmart-supplied");

        Assert.Equal(CreativeAcademy.MasterReference, pharmacy.Role);
        Assert.Equal("pharmacy", pharmacy.Family);
        Assert.Contains("quality anchor", pharmacy.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Creative template: no", pharmacy.Body);
        Assert.Equal(CreativeAcademy.SuppliedExample, grocery.Role);
        Assert.Contains("not promoted", grocery.Body);
    }

    [Fact]
    public void Pharmacy_brand_dna_changes_the_identity_without_lowering_the_quality_anchor()
    {
        var dna = CreativeAcademy.Choose(
            "pharmacy",
            "Farmacia Nueva Salud",
            "Prescription pickup",
            "Burgundy and gold",
            "Recoge tu receta",
            "Warm professional Panama",
            false);

        Assert.Equal(CreativeAcademy.PharmacyTeacher, dna.TeacherKey);
        Assert.True(dna.TeacherOnFile);
        Assert.True(dna.Distinct);
        Assert.Contains("Brand DNA remains authoritative", dna.Notice);
        Assert.Throws<InvalidOperationException>(() => CreativeAcademy.Choose(
            "pharmacy", "VidaCare Pharmacy", "Medicine", "Blue and green", "Shop now", "Bright", false));
    }

    [Fact]
    public void A_production_brief_preserves_customization_and_the_quality_signature()
    {
        var brief = CreativeAcademy.PrepareProductionBrief(
            "pharmacy",
            "Farmacia Nueva Salud",
            "Burgundy and gold",
            "Montserrat",
            "Tu salud, más cerca",
            "Recoge tu receta",
            "Panama",
            "Spanish",
            "Feature prescription pickup; do not show families",
            true,
            false,
            false);

        Assert.Equal(CreativeAcademy.ProductionSpecReady, brief.Status);
        Assert.Equal(CreativeAcademy.PharmacyTeacher, brief.TeacherKey);
        Assert.Equal("Burgundy and gold", brief.Palette);
        Assert.Equal("Montserrat", brief.FontFamily);
        Assert.Equal("LOCALLY_PLAUSIBLE_DEFAULT", brief.Casting.Status);
        Assert.Contains("hero dominance", brief.QualitySignature);
        Assert.Contains("photography and people", brief.ReplaceCreative);
        Assert.False(brief.CanGenerate);
        Assert.Equal(0, brief.ModelCalls);
        Assert.False(brief.CampaignReady);
        Assert.Equal("NOT_SENT", brief.Delivery);
    }

    [Fact]
    public void Geographic_casting_requires_a_specific_market_and_research_when_uncertain()
    {
        var research = CreativeAcademy.PlanCasting("Santo Domingo", false, false);
        var approved = CreativeAcademy.PlanCasting("Manila", false, true);

        Assert.Equal("MARKET_RESEARCH_REQUIRED", research.Status);
        Assert.True(research.ResearchRequired);
        Assert.Equal("ADVERTISER_ASSET", approved.Status);
        Assert.False(approved.ResearchRequired);
        Assert.Throws<InvalidOperationException>(() => CreativeAcademy.PlanCasting("generic Latino", true, false));
    }

    [Fact]
    public void Customization_quality_originality_and_geography_are_independent_gates()
    {
        var qualityFailed = CreativeAcademy.EvaluateFinalGates(true, false, true, true);
        var customizationFailed = CreativeAcademy.EvaluateFinalGates(false, true, true, true);
        var withheld = CreativeAcademy.EvaluateFinalGates(true, true, null, true);
        var eligible = CreativeAcademy.EvaluateFinalGates(true, true, true, true);

        Assert.Equal(CreativeAcademy.Revise, qualityFailed.Status);
        Assert.Contains(qualityFailed.Defects, item => item.Contains("quality parity", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(CreativeAcademy.Revise, customizationFailed.Status);
        Assert.Contains(customizationFailed.Defects, item => item.Contains("Brand DNA", StringComparison.Ordinal));
        Assert.Equal(CreativeAcademy.Withheld, withheld.Status);
        Assert.Equal(CreativeAcademy.Eligible, eligible.Status);
        Assert.True(eligible.EligibleToContinue);
        Assert.False(eligible.CampaignReady);
        Assert.Equal("NOT_SENT", eligible.Delivery);
    }

    [Fact]
    public void Production_model_calls_are_still_refused_until_a_model_is_configured()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcademy.PrepareProductionBrief(
                "pharmacy", "Farmacia Nueva Salud", "Burgundy and gold", "Montserrat",
                "Tu salud, más cerca", "Recoge tu receta", "Panama", "Spanish",
                "Feature prescription pickup", true, false, true));

        Assert.Contains("not configured", refusal.Message);
    }

    [Fact]
    public void Fitness_and_automotive_niches_select_their_supplied_quality_references()
    {
        var fitness = CreativeAcademy.Choose(
            "fitness", "Impulso Fitness", "Membership", "Black and orange",
            "Join today", "Energetic local club", false);
        var automotive = CreativeAcademy.Choose(
            "automotive", "Motor Centro", "Brake service", "Navy and silver",
            "Book service", "Precise and trustworthy", false);

        Assert.Equal(CreativeAcademy.FitnessTeacher, fitness.TeacherKey);
        Assert.Equal(CreativeAcademy.AutomotiveTeacher, automotive.TeacherKey);
        Assert.True(fitness.TeacherOnFile);
        Assert.True(automotive.TeacherOnFile);
        Assert.Contains(CreativeAcademy.AllLessons, item =>
            item.LessonKey == CreativeAcademy.FitnessTeacher && item.Role == CreativeAcademy.Reference);
        Assert.Contains(CreativeAcademy.AllLessons, item =>
            item.LessonKey == CreativeAcademy.AutomotiveTeacher && item.Role == CreativeAcademy.Reference);
    }

    [Fact]
    public void Brand_and_niche_are_the_only_required_client_intake_fields()
    {
        var brief = CreativeAcademy.PrepareProductionBrief(
            "fitness", "Impulso Fitness",
            null, null, null, null, null, null, null,
            false, false, false);

        Assert.Equal(CreativeAcademy.ProductionSpecReady, brief.Status);
        Assert.Equal(CreativeAcademy.FitnessTeacher, brief.TeacherKey);
        Assert.Equal("MARKET_RESEARCH_REQUIRED", brief.Casting.Status);
        Assert.Contains("Impulso Fitness", brief.GenerationRecipe);
        Assert.Contains("wide 16:9", brief.GenerationRecipe);
        Assert.Contains(CreativeAcademy.FitnessTeacher, brief.GenerationRecipe);
        Assert.Contains("Do not copy", brief.GenerationRecipe);
        Assert.False(brief.CanGenerate);
        Assert.Equal("NOT_SENT", brief.Delivery);
    }

    [Fact]
    public void A_niche_without_an_approved_anchor_does_not_enter_image_generation()
    {
        var brief = CreativeAcademy.PrepareProductionBrief(
            "dental", "Sonrisa Norte",
            null, null, null, null, "Colombia", "Spanish", null,
            true, false, false);

        Assert.Equal("NICHE_ANCHOR_REQUIRED", brief.Status);
        Assert.Equal(string.Empty, brief.TeacherKey);
        Assert.Contains("Stop before image generation", brief.GenerationRecipe);
        Assert.False(brief.CanGenerate);
    }
}
