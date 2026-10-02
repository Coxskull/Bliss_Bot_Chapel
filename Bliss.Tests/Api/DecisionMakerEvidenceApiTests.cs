using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class DecisionMakerEvidenceApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public DecisionMakerEvidenceApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_evidence_can_raise_confidence_without_sending()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = "https://example.com/casa-verde"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var rejected = await client.PostAsJsonAsync("/api/demonstrations/casa-verde/decision-maker", new
        {
            personName = "Ana Ruiz",
            role = "Owner",
            evidenceKind = "team_page",
            evidenceUrl = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("will not store", rejectedBody.GetProperty("error").GetString());
        var unchanged = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/casa-verde");
        Assert.Equal("UNVERIFIED", unchanged!.GetProperty("decisionMakerStatus").GetString());
        Assert.Equal(string.Empty, unchanged.GetProperty("decisionMakerName").GetString());

        var recorded = await client.PostAsJsonAsync("/api/demonstrations/casa-verde/decision-maker", new
        {
            personName = "Ana Ruiz",
            role = "Owner",
            evidenceKind = "team_page",
            evidenceUrl = "https://example.com/casa-verde/team",
            corroboratingKind = "professional_profile",
            corroboratingUrl = "https://example.com/casa-verde/profile",
            contactKind = "company_marketing",
            contactValue = "marketing@example.com",
            contactSourceUrl = "https://example.com/casa-verde/contact"
        });
        var page = await recorded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.Equal("MEDIUM", page!.GetProperty("decisionMakerStatus").GetString());
        Assert.Equal("Ana Ruiz", page.GetProperty("decisionMakerName").GetString());
        Assert.Equal("TIER_2", page.GetProperty("contactTier").GetString());
        Assert.Equal("CURRENT", page.GetProperty("freshness").GetString());
        Assert.True(page.GetProperty("personalizationAllowed").GetBoolean());
        Assert.Equal("NOT_SENT", page.GetProperty("delivery").GetString());
        Assert.Contains("For Ana Ruiz:", page.GetProperty("subject").GetString());

        var reply = await client.PostAsJsonAsync("/api/demonstrations/casa-verde/messages", new
        {
            text = "Who is the marketing director?"
        });
        var conversation = await reply.Content.ReadFromJsonAsync<JsonElement>();
        var text = conversation!.GetProperty("reply").GetString();
        Assert.Contains("Ana Ruiz", text);
        Assert.Contains("MEDIUM", text);
        Assert.Contains("not authorized", text);

        var price = await client.PostAsJsonAsync("/api/demonstrations/casa-verde/messages", new
        {
            text = "What does this cost?"
        });
        var priceBody = await price.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("cannot invent a price", priceBody!.GetProperty("reply").GetString());
    }
}
