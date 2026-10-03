using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerWakeApiTests
{
    [Fact]
    public async Task Discovered_businesses_stay_asleep_and_open_no_workspace()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        await Discover(client, "Mesa Norte", "Panama City");
        await Discover(client, "Puerto Azul", "Quito");

        var mesa = await Wake(client, "mesa-norte", null);
        var azul = await Wake(client, "puerto-azul", null);

        Assert.Equal("ASLEEP", mesa.GetProperty("plannerStatus").GetString());
        Assert.Equal("ASLEEP", azul.GetProperty("plannerStatus").GetString());
        Assert.Contains("not planned", mesa.GetProperty("plannerNotice").GetString());
        Assert.Contains("NOT_SENT", mesa.GetProperty("plannerNotice").GetString());
        Assert.Contains("not planned", azul.GetProperty("plannerNotice").GetString());
        Assert.Equal("NOT_SENT", mesa.GetProperty("delivery").GetString());
        Assert.Equal("NOT_SENT", azul.GetProperty("delivery").GetString());
        Assert.Equal("PRESERVED", azul.GetProperty("prospectState").GetString());
        Assert.Equal(string.Empty, mesa.GetProperty("plannerWorkspaceId").GetString());
        Assert.DoesNotMatch(@"\d", mesa.GetProperty("plannerNotice").GetString());
        Assert.Equal(0, WorkspaceCount(factory));
    }

    [Fact]
    public async Task An_accepted_quote_wakes_the_existing_planner_once()
    {
        await using var factory = new BlissApiFactory();
        var advertiserId = await SeedAdvertiserAsync(factory);
        var quoteId = await SeedQuoteAsync(factory, QuoteStatuses.Accepted, 215m);
        var client = factory.CreateClient();
        await Discover(client, "Harbor Table", "Panama City");
        await Discover(client, "Kept Table", "Panama City");
        Attach(factory, "harbor-table", quoteId);

        var awake = await Wake(client, "harbor-table", advertiserId);
        var notice = awake.GetProperty("plannerNotice").GetString()!;
        Assert.Equal("AWAKE", awake.GetProperty("plannerStatus").GetString());
        Assert.Contains("Wedding Planner is awake for Harbor Table in Panama City", notice);
        Assert.Contains("215 PHP", notice);
        Assert.Contains("No campaign is planned", notice);
        Assert.Contains("NOT_SENT", notice);
        Assert.DoesNotContain("200", notice);
        Assert.DoesNotContain("win", notice);
        Assert.Equal("OPPORTUNITY_SCORED", awake.GetProperty("prospectState").GetString());
        Assert.Equal("NOT_SENT", awake.GetProperty("delivery").GetString());
        var workspaceId = awake.GetProperty("plannerWorkspaceId").GetString();
        var sessionId = awake.GetProperty("plannerSessionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(workspaceId));

        var again = await Wake(client, "harbor-table", advertiserId);
        Assert.Equal(workspaceId, again.GetProperty("plannerWorkspaceId").GetString());
        Assert.Equal(sessionId, again.GetProperty("plannerSessionId").GetString());
        Assert.Equal(1, WorkspaceCount(factory));
        Assert.Equal(1, SessionCount(factory));
        Assert.Equal(1, MessageCount(factory));

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var message = database.WeddingPlannerConversationMessages.Single();
        Assert.Equal(WeddingPlannerActorTypes.System, message.ActorType);
        Assert.Equal(notice, message.Body);
        Assert.Equal("SYSTEM", message.ActorLabel);

        var kept = await Wake(client, "kept-table", advertiserId);
        Assert.Equal("ASLEEP", kept.GetProperty("plannerStatus").GetString());
        Assert.Equal(string.Empty, kept.GetProperty("plannerWorkspaceId").GetString());
        Assert.Equal(1, WorkspaceCount(factory));
        Assert.Equal("Kept Table", kept.GetProperty("businessName").GetString());
    }

    [Fact]
    public async Task An_unaccepted_quote_and_suppression_leave_the_planner_asleep()
    {
        await using var factory = new BlissApiFactory();
        var advertiserId = await SeedAdvertiserAsync(factory);
        var approved = await SeedQuoteAsync(factory, QuoteStatuses.Approved, 215m);
        var accepted = await SeedQuoteAsync(factory, QuoteStatuses.Accepted, 215m);
        var client = factory.CreateClient();
        await Discover(client, "Draft Table", "Panama City");
        await Discover(client, "Stopped Table", "Panama City");
        Attach(factory, "draft-table", approved);
        Attach(factory, "stopped-table", accepted);
        var stopped = await client.PostAsJsonAsync("/api/demonstrations/stopped-table/suppression", new
        {
            reason = "The business asked Alpha to stop"
        });
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);

        var draft = await Wake(client, "draft-table", advertiserId);
        var suppressed = await Wake(client, "stopped-table", advertiserId);
        Assert.Equal("ASLEEP", draft.GetProperty("plannerStatus").GetString());
        Assert.Contains("has not been accepted", draft.GetProperty("plannerNotice").GetString());
        Assert.DoesNotContain("215", draft.GetProperty("plannerNotice").GetString());
        Assert.Equal("ASLEEP", suppressed.GetProperty("plannerStatus").GetString());
        Assert.Contains("Suppression comes before a campaign", suppressed.GetProperty("plannerNotice").GetString());
        Assert.Equal("NOT_SENT", suppressed.GetProperty("delivery").GetString());
        Assert.Equal(0, WorkspaceCount(factory));
    }

    private static async Task Discover(HttpClient client, string businessName, string market)
    {
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market,
            businessName,
            publicSourceUrl = "https://example.com/" + businessName.ToLowerInvariant().Replace(' ', '-')
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    private static async Task<JsonElement> Wake(HttpClient client, string slug, Guid? advertiserId)
    {
        var response = await client.PostAsJsonAsync("/api/demonstrations/" + slug + "/wedding-planner", new
        {
            advertiserId
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body!;
    }

    private static void Attach(BlissApiFactory factory, string slug, Guid quoteId)
    {
        using var scope = factory.Services.CreateScope();
        var demonstrations = scope.ServiceProvider.GetRequiredService<Bliss.Api.Demonstrations.ProspectDemonstrationService>();
        demonstrations.AttachEconomicsQuote(slug, quoteId);
    }

    private static async Task<Guid> SeedAdvertiserAsync(BlissApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var advertiser = new Advertiser
        {
            Id = Guid.NewGuid(),
            Name = "Fixture advertiser",
            CreatedAt = DateTime.UtcNow
        };
        database.Advertisers.Add(advertiser);
        await database.SaveChangesAsync();
        return advertiser.Id;
    }

    private static async Task<Guid> SeedQuoteAsync(BlissApiFactory factory, string status, decimal amount)
    {
        var quoteId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var now = DateTime.UtcNow;
        database.Quotes.Add(new Quote
        {
            Id = quoteId,
            CurrencyCode = "PHP",
            Status = status,
            CurrentVersionNumber = 1,
            RequestedBy = "TEST",
            SourceSystem = "TEST",
            IdempotencyKey = "phase19-quote-" + quoteId.ToString("N"),
            CreatedAt = now,
            UpdatedAt = now
        });
        database.Set<QuoteVersion>().Add(new QuoteVersion
        {
            Id = versionId,
            QuoteId = quoteId,
            VersionNumber = 1,
            CurrencyCode = "PHP",
            SubtotalAmount = amount,
            TotalAmount = amount,
            RevisionReason = "TEST",
            CreatedBy = "TEST",
            SourceSystem = "TEST",
            IdempotencyKey = "phase19-version-" + versionId.ToString("N"),
            CreatedAt = now
        });
        database.Set<QuoteOutcome>().Add(new QuoteOutcome
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            QuoteVersionId = versionId,
            Response = QuoteOutcomeResponses.Accepted,
            Amount = amount,
            CurrencyCode = "PHP",
            ActorLabel = "TEST",
            Rationale = "Economics fixture",
            SourceSystem = "TEST",
            IdempotencyKey = "phase19-outcome-" + quoteId.ToString("N"),
            CreatedAt = now
        });
        await database.SaveChangesAsync();
        return quoteId;
    }

    private static int WorkspaceCount(BlissApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        return database.WeddingPlannerWorkspaces.Count();
    }

    private static int SessionCount(BlissApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        return database.WeddingPlannerPlanningSessions.Count();
    }

    private static int MessageCount(BlissApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        return database.WeddingPlannerConversationMessages.Count();
    }
}
