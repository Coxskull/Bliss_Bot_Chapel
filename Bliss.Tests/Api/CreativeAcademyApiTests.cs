using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class CreativeAcademyApiTests
{
    [Fact]
    public async Task The_curriculum_is_judged_once_and_does_not_send()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/academy");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.Equal(0, before.GetProperty("modelCalls").GetInt32());
        Assert.False(before.GetProperty("campaignReady").GetBoolean());
        Assert.Equal(0, before.GetProperty("lessons").GetArrayLength());

        var stored = await client.PostAsJsonAsync(
            "/api/operations/academy/curriculum",
            new { curriculumKey = "patisserie-curriculum-1" });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.Equal(9, first.GetProperty("lessons").GetArrayLength());
        Assert.Equal(50, first.GetProperty("nicheRoster").GetArrayLength());
        Assert.Equal(10, first.GetProperty("createdCount").GetInt32());
        Assert.Equal(40, first.GetProperty("notCreatedCount").GetInt32());
        Assert.Equal(11, first.GetProperty("nextNiche").GetProperty("number").GetInt32());
        Assert.Equal("auto-parts", first.GetProperty("nextNiche").GetProperty("key").GetString());

        var lamour = Lesson(first, "lamour-sucre");
        var solara = Lesson(first, "solara");
        var belmonte = Lesson(first, "belmonte");
        var prototype = Lesson(first, "maison-fleur");
        var pharmacy = Lesson(first, "vidacare-master-01");
        var grocery = Lesson(first, "freshmart-supplied");
        var fitness = Lesson(first, "nova-fit-reference");
        var automotive = Lesson(first, "taller-ruta-reference");
        var motorcycle = Lesson(first, "brava-moto-reference");
        Assert.Equal("REVISE", lamour.GetProperty("status").GetString());
        Assert.Equal("REVISE", solara.GetProperty("status").GetString());
        Assert.Equal("WITHHELD", belmonte.GetProperty("status").GetString());
        Assert.Equal("REFERENCE", prototype.GetProperty("status").GetString());
        Assert.Equal("MASTER_REFERENCE", pharmacy.GetProperty("status").GetString());
        Assert.Equal("SUPPLIED_EXAMPLE", grocery.GetProperty("status").GetString());
        Assert.Equal("REFERENCE", fitness.GetProperty("status").GetString());
        Assert.Equal("REFERENCE", automotive.GetProperty("status").GetString());
        Assert.Equal("REFERENCE", motorcycle.GetProperty("status").GetString());
        Assert.Equal("motorcycle", motorcycle.GetProperty("family").GetString());
        Assert.Equal(0, lamour.GetProperty("modelCalls").GetInt32());
        Assert.False(lamour.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", lamour.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync(
            "/api/operations/academy/curriculum",
            new { curriculumKey = "patisserie-curriculum-1" });
        var second = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.Equal(9, second.GetProperty("lessons").GetArrayLength());

        var visual = await client.PostAsJsonAsync(
            "/api/operations/academy/visual",
            new { lessonKey = "belmonte", visualBenchmarkMet = true });
        var judged = await visual.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, visual.StatusCode);
        Assert.Equal("PASS", Lesson(judged!, "belmonte").GetProperty("status").GetString());
        Assert.Equal("REVISE", Lesson(judged, "lamour-sucre").GetProperty("status").GetString());

        var campaign = await client.PostAsJsonAsync(
            "/api/operations/academy/visual",
            new { lessonKey = "belmonte", visualBenchmarkMet = true, campaignReady = true });
        var refused = await campaign.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, campaign.StatusCode);
        Assert.Equal("NOT_SENT", refused!.GetProperty("delivery").GetString());
        Assert.False(refused.GetProperty("campaignReady").GetBoolean());
        Assert.Contains("Campaign ready is refused", refused.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Production_brief_uses_the_pharmacy_anchor_without_generating_an_ad()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/operations/academy/production-brief",
            new
            {
                family = "pharmacy",
                brandName = "Farmacia Nueva Salud",
                palette = "Burgundy and gold",
                fontFamily = "Montserrat",
                headline = "Tu salud, mas cerca",
                cta = "Recoge tu receta",
                market = "Panama",
                language = "Spanish",
                requirements = "Feature prescription pickup; do not show families",
                marketResearchComplete = true
            });
        var brief = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("PRODUCTION_SPEC_READY", brief!.GetProperty("status").GetString());
        Assert.Equal("vidacare-master-01", brief.GetProperty("teacherKey").GetString());
        Assert.Equal("Burgundy and gold", brief.GetProperty("palette").GetString());
        Assert.Equal("LOCALLY_PLAUSIBLE_DEFAULT", brief.GetProperty("casting").GetProperty("status").GetString());
        Assert.False(brief.GetProperty("canGenerate").GetBoolean());
        Assert.Equal(0, brief.GetProperty("modelCalls").GetInt32());
        Assert.Equal("NOT_SENT", brief.GetProperty("delivery").GetString());
    }

    [Fact]
    public async Task Both_final_gates_must_pass_and_eligibility_still_does_not_send()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var reviseResponse = await client.PostAsJsonAsync(
            "/api/operations/academy/parity",
            new
            {
                customizationCompliance = true,
                qualityParity = false,
                originality = true,
                geographicAuthenticity = true
            });
        var revise = await reviseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var eligibleResponse = await client.PostAsJsonAsync(
            "/api/operations/academy/parity",
            new
            {
                customizationCompliance = true,
                qualityParity = true,
                originality = true,
                geographicAuthenticity = true
            });
        var eligible = await eligibleResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("REVISE", revise!.GetProperty("status").GetString());
        Assert.False(revise.GetProperty("eligibleToContinue").GetBoolean());
        Assert.Equal("ELIGIBLE_TO_CONTINUE", eligible!.GetProperty("status").GetString());
        Assert.True(eligible.GetProperty("eligibleToContinue").GetBoolean());
        Assert.False(eligible.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", eligible.GetProperty("delivery").GetString());
    }

    [Fact]
    public async Task Client_can_enter_only_store_name_and_niche_to_prepare_the_recipe()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/operations/academy/production-brief",
            new { family = "automotive", brandName = "Motor Centro" });
        var brief = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("PRODUCTION_SPEC_READY", brief!.GetProperty("status").GetString());
        Assert.Equal("taller-ruta-reference", brief.GetProperty("teacherKey").GetString());
        Assert.Contains("Motor Centro", brief.GetProperty("generationRecipe").GetString());
        Assert.Equal("MARKET_RESEARCH_REQUIRED", brief.GetProperty("casting").GetProperty("status").GetString());
        Assert.False(brief.GetProperty("canGenerate").GetBoolean());
        Assert.Equal("NOT_SENT", brief.GetProperty("delivery").GetString());
    }

    [Fact]
    public async Task Generate_button_fails_closed_until_a_production_model_is_configured()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/operations/academy/production-brief",
            new { family = "fitness", brandName = "Impulso Fitness", callModel = true });
        var refusal = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not configured", refusal!.GetProperty("error").GetString());
        Assert.Equal(0, refusal.GetProperty("modelCalls").GetInt32());
        Assert.False(refusal.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", refusal.GetProperty("delivery").GetString());
    }

    private static JsonElement Lesson(JsonElement board, string key)
    {
        foreach (var lesson in board.GetProperty("lessons").EnumerateArray())
        {
            if (lesson.GetProperty("lessonKey").GetString() == key)
            {
                return lesson;
            }
        }

        throw new InvalidOperationException("Missing lesson " + key);
    }
}
