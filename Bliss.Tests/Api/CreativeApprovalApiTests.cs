using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class CreativeApprovalApiTests
{
    [Fact]
    public async Task A_human_decision_stays_in_its_workspace_and_does_not_write_a_match()
    {
        await using var factory = new BlissApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new WeddingPlannerDataSeeder(db).SeedAsync();
        }

        var client = factory.CreateClient();
        var sunrise = await OpenAsync(client, Phase1DataSeeder.AdvertiserId, "creative-workspace-sunrise");
        var dental = await OpenAsync(client, WeddingPlannerDataSeeder.DentalManilaId, "creative-workspace-dental");
        var sessionResponse = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{sunrise}/sessions",
            new CreateWeddingPlannerSessionRequest("OPERATOR_CONSOLE", "creative-session-sunrise"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);
        var message = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session.GetProperty("sessionId").GetGuid()}/messages",
            new AppendWeddingPlannerMessageRequest("OPERATOR", "Sunrise private note.", "OPERATOR_CONSOLE", "creative-message-sunrise"));
        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/bliss/matches");
        var matchCount = before!.GetArrayLength();
        var empty = await client.GetFromJsonAsync<JsonElement>($"/api/operations/creative?workspaceId={sunrise}");
        Assert.Equal("NOT_SENT", empty!.GetProperty("delivery").GetString());
        Assert.False(empty.GetProperty("campaignReady").GetBoolean());
        Assert.False(empty.GetProperty("matchWritten").GetBoolean());
        Assert.False(empty.GetProperty("priceInvented").GetBoolean());
        Assert.Equal(0, empty.GetProperty("modelCalls").GetInt32());
        Assert.Contains("discovered business is not opened", empty.GetProperty("notice").GetString());
        Assert.Equal(0, empty.GetProperty("decisions").GetArrayLength());
        Assert.Contains("Sunrise private note.", empty.GetProperty("messages")[0].GetProperty("body").GetString());

        var approved = await client.PostAsJsonAsync("/api/operations/creative", new
        {
            workspaceId = sunrise,
            title = "Table card",
            decision = "APPROVE",
            actorType = "OPERATOR",
            idempotencyKey = "creative-table-card"
        });
        var first = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("duplicate").GetBoolean());
        Assert.Equal("HUMAN_APPROVED", first.GetProperty("decisions")[0].GetProperty("status").GetString());
        Assert.False(first.GetProperty("decisions")[0].GetProperty("campaignReady").GetBoolean());
        Assert.Equal(0, first.GetProperty("decisions")[0].GetProperty("modelCalls").GetInt32());
        Assert.Equal("NOT_SENT", first.GetProperty("decisions")[0].GetProperty("delivery").GetString());
        Assert.Equal("CREATIVE_DECIDED", first.GetProperty("audits")[0].GetProperty("action").GetString());
        Assert.Contains("No price was invented", first.GetProperty("notice").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/creative", new
        {
            workspaceId = sunrise,
            title = "Table card",
            decision = "APPROVE",
            actorType = "OPERATOR",
            idempotencyKey = "creative-table-card"
        });
        var duplicate = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True(duplicate!.GetProperty("duplicate").GetBoolean());
        Assert.False(duplicate.GetProperty("written").GetBoolean());
        Assert.Equal(1, duplicate.GetProperty("decisions").GetArrayLength());
        Assert.Equal(1, duplicate.GetProperty("audits").GetArrayLength());

        var other = await client.GetFromJsonAsync<JsonElement>($"/api/operations/creative?workspaceId={dental}");
        Assert.Equal("TEST Dental Manila", other!.GetProperty("advertiserName").GetString());
        Assert.Equal(0, other.GetProperty("decisions").GetArrayLength());
        Assert.Equal(0, other.GetProperty("messages").GetArrayLength());
        Assert.Equal(0, other.GetProperty("audits").GetArrayLength());
        Assert.DoesNotContain("Sunrise private note.", other.GetRawText());
        Assert.DoesNotContain("Table card", other.GetRawText());

        var campaign = await client.PostAsJsonAsync("/api/operations/creative", new
        {
            workspaceId = sunrise,
            title = "Table card",
            decision = "CAMPAIGN_READY",
            actorType = "OPERATOR",
            idempotencyKey = "creative-campaign"
        });
        Assert.Equal(HttpStatusCode.BadRequest, campaign.StatusCode);
        var refused = await campaign.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Campaign ready is refused", refused!.GetProperty("error").GetString());
        Assert.Equal(0, refused.GetProperty("modelCalls").GetInt32());
        Assert.False(refused.GetProperty("matchWritten").GetBoolean());

        var model = await client.PostAsJsonAsync("/api/operations/creative", new
        {
            workspaceId = sunrise,
            title = "Table card",
            decision = "APPROVE",
            actorType = "SYSTEM",
            idempotencyKey = "creative-model"
        });
        Assert.Equal(HttpStatusCode.BadRequest, model.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/bliss/matches");
        Assert.Equal(matchCount, after!.GetArrayLength());
    }

    private static async Task<Guid> OpenAsync(HttpClient client, Guid advertiserId, string key)
    {
        var response = await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(advertiserId, "OPERATOR_CONSOLE", key));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return body!.GetProperty("workspaceId").GetGuid();
    }
}
