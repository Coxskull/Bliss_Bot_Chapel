using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPriceApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPriceApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ask_Alpha_says_only_an_accepted_economics_amount()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var refused = await Ask(client);
        Assert.Contains("cannot invent a price", refused);
        Assert.DoesNotMatch(new Regex(@"\d"), refused);

        var missing = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/economics-price", new
        {
            quoteId = Guid.NewGuid()
        });
        var missingBody = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("no accepted result", missingBody!.GetProperty("error").GetString());
        Assert.DoesNotContain("PHP", missingBody.GetProperty("error").GetString());

        var draftId = await SeedQuoteAsync(QuoteStatuses.Draft, 999m);
        var draft = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/economics-price", new { quoteId = draftId });
        var draftBody = await draft.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, draft.StatusCode);
        Assert.DoesNotContain("999", draftBody!.GetRawText());

        var acceptedId = await SeedQuoteAsync(QuoteStatuses.Accepted, 215m);
        var linked = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/economics-price", new { quoteId = acceptedId });
        var link = await linked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, linked.StatusCode);
        Assert.Equal("215 PHP", link!.GetProperty("spoken").GetString());
        Assert.Equal("NOT_SENT", link.GetProperty("delivery").GetString());
        Assert.Equal("Ask Alpha", link.GetProperty("voice").GetString());

        var spoken = await Ask(client);
        Assert.Contains("Economics accepted 215 PHP", spoken);
        Assert.Contains("cannot invent a price", spoken);
        Assert.DoesNotContain("$", spoken);
        Assert.DoesNotContain("999", spoken);

        await SetStatusAsync(acceptedId, QuoteStatuses.Declined);
        var withdrawn = await Ask(client);
        Assert.Contains("cannot invent a price", withdrawn);
        Assert.DoesNotContain("215", withdrawn);
    }

    private static async Task<string> Ask(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/messages", new
        {
            text = "How much does this cost?"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body!.GetProperty("reply").GetString()!;
    }

    private async Task<Guid> SeedQuoteAsync(string status, decimal amount)
    {
        var quoteId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var now = DateTime.UtcNow;
        db.Quotes.Add(new Quote
        {
            Id = quoteId,
            CurrencyCode = "PHP",
            Status = status,
            CurrentVersionNumber = 1,
            RequestedBy = "TEST",
            SourceSystem = "TEST",
            IdempotencyKey = "phase14-quote-" + quoteId.ToString("N"),
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Set<QuoteVersion>().Add(new QuoteVersion
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
            IdempotencyKey = "phase14-version-" + versionId.ToString("N"),
            CreatedAt = now
        });
        db.Set<QuoteOutcome>().Add(new QuoteOutcome
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
            IdempotencyKey = "phase14-outcome-" + quoteId.ToString("N"),
            CreatedAt = now
        });
        await db.SaveChangesAsync();
        return quoteId;
    }

    private async Task SetStatusAsync(Guid quoteId, string status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var quote = await db.Quotes.FindAsync(quoteId);
        quote!.Status = status;
        await db.SaveChangesAsync();
    }
}
