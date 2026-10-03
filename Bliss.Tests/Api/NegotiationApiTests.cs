using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class NegotiationApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public NegotiationApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ask_Alpha_negotiates_only_inside_the_economics_envelope()
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

        var refused = await Ask(client, "Can you take 100?");
        Assert.Contains("will not negotiate without an approved Economics quote", refused.Reply);
        Assert.Contains("cannot invent a price", refused.Reply);
        Assert.Contains("NOT_SENT", refused.Reply);
        Assert.Equal("integrity", refused.Gear);
        Assert.Equal("Ask Alpha", refused.Voice);
        Assert.DoesNotMatch(new Regex(@"\d"), refused.Reply);
        Assert.DoesNotContain("$", refused.Reply);

        var price = await Ask(client, "How much does this cost?");
        Assert.Contains("cannot invent a price", price.Reply);
        Assert.DoesNotContain("negotiate without an approved", price.Reply);
        Assert.DoesNotMatch(new Regex(@"\d"), price.Reply);

        var quoteId = await SeedApprovedQuoteAsync();
        Attach(quoteId);

        var below = await Ask(client, "Can you take 150?");
        Assert.Contains("below the creator floor", below.Reply);
        Assert.Contains("will not break", below.Reply);
        Assert.Contains("200 PHP", below.Reply);
        Assert.Contains("cannot invent a price", below.Reply);
        Assert.Contains("not a win", below.Reply);
        Assert.DoesNotContain("150", below.Reply);
        Assert.Equal("handoff", below.Gear);
        Assert.True(below.HumanEscalation);
        await AssertStillApprovedAsync(quoteId, outcomes: 0);

        var above = await Ask(client, "Can you take 350?");
        Assert.Contains("outside the Economics envelope", above.Reply);
        Assert.Contains("will not raise the price", above.Reply);
        Assert.Contains("300 PHP", above.Reply);
        Assert.DoesNotContain("350", above.Reply);
        Assert.Equal("handoff", above.Gear);
        await AssertStillApprovedAsync(quoteId, outcomes: 0);

        var explain = await Ask(client, "Can you negotiate?");
        Assert.Contains("creator floor is 200 PHP", explain.Reply);
        Assert.Contains("envelope high is 300 PHP", explain.Reply);
        Assert.Contains("not accepted", explain.Reply);
        Assert.Contains("cannot invent a price", explain.Reply);
        await AssertStillApprovedAsync(quoteId, outcomes: 0);

        var inside = await Ask(client, "Can you take 220?");
        Assert.Contains("inside the Economics envelope", inside.Reply);
        Assert.Contains("Economics recorded a draft of 220 PHP", inside.Reply);
        Assert.Contains("not accepted", inside.Reply);
        Assert.Contains("cannot invent a price", inside.Reply);
        Assert.Contains("not a win", inside.Reply);
        Assert.Contains("NOT_SENT", inside.Reply);
        Assert.DoesNotContain("Economics accepted", inside.Reply);
        Assert.Equal("integrity", inside.Gear);
        Assert.Equal("Ask Alpha", inside.Voice);

        await AssertDraftAsync(quoteId);

        var withdrawn = await Ask(client, "How much does this cost?");
        Assert.Contains("cannot invent a price", withdrawn.Reply);
        Assert.DoesNotContain("Economics accepted", withdrawn.Reply);
        Assert.DoesNotContain("220", withdrawn.Reply);
        Assert.DoesNotMatch(new Regex(@"\d"), withdrawn.Reply);
    }

    private static async Task<Spoken> Ask(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/messages", new { text });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new Spoken(
            body!.GetProperty("reply").GetString()!,
            body.GetProperty("gear").GetString()!,
            body.GetProperty("voice").GetString()!,
            body.GetProperty("humanEscalation").GetBoolean());
    }

    private void Attach(Guid quoteId)
    {
        using var scope = _factory.Services.CreateScope();
        var demonstrations = scope.ServiceProvider.GetRequiredService<Bliss.Api.Demonstrations.ProspectDemonstrationService>();
        demonstrations.AttachEconomicsQuote("mesa-norte", quoteId);
    }

    private async Task<Guid> SeedApprovedQuoteAsync()
    {
        var quoteId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        db.RateRecommendations.Add(new RateRecommendation
        {
            Id = recommendationId,
            CreatorId = Guid.NewGuid(),
            GeographicMarketId = Guid.NewGuid(),
            PricingModelId = Guid.NewGuid(),
            PricingRuleVersionId = Guid.NewGuid(),
            CurrencyCode = "PHP",
            RangeLow = 200m,
            RangeTarget = 250m,
            RangeHigh = 300m,
            ConfidenceLevel = "UNKNOWN",
            InputSnapshotJson = "{}",
            SourceSystem = "TEST",
            IdempotencyKey = "phase15-rec-" + recommendationId.ToString("N"),
            CreatedAt = now
        });
        db.Quotes.Add(new Quote
        {
            Id = quoteId,
            CurrencyCode = "PHP",
            Status = QuoteStatuses.Approved,
            CurrentVersionNumber = 1,
            RequestedBy = "TEST",
            SourceSystem = "TEST",
            IdempotencyKey = "phase15-quote-" + quoteId.ToString("N"),
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Set<QuoteVersion>().Add(new QuoteVersion
        {
            Id = versionId,
            QuoteId = quoteId,
            VersionNumber = 1,
            CurrencyCode = "PHP",
            SubtotalAmount = 250m,
            TotalAmount = 250m,
            RevisionReason = "TEST",
            CreatedBy = "TEST",
            SourceSystem = "TEST",
            IdempotencyKey = "phase15-version-" + versionId.ToString("N"),
            CreatedAt = now
        });
        db.Set<QuoteLineItem>().Add(new QuoteLineItem
        {
            Id = Guid.NewGuid(),
            QuoteVersionId = versionId,
            RateRecommendationId = recommendationId,
            SortOrder = 1,
            Description = "Placement",
            Quantity = 1m,
            UnitAmount = 250m,
            LineAmount = 250m,
            CurrencyCode = "PHP",
            RecommendationLow = 200m,
            RecommendationTarget = 250m,
            RecommendationHigh = 300m
        });
        await db.SaveChangesAsync();
        return quoteId;
    }

    private async Task AssertStillApprovedAsync(Guid quoteId, int outcomes)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var quote = await db.Quotes.Include(item => item.Outcomes).SingleAsync(item => item.Id == quoteId);
        Assert.Equal(QuoteStatuses.Approved, quote.Status);
        Assert.Equal(outcomes, quote.Outcomes.Count);
    }

    private async Task AssertDraftAsync(Guid quoteId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var quote = await db.Quotes
            .Include(item => item.Outcomes)
            .Include(item => item.Versions)
            .SingleAsync(item => item.Id == quoteId);
        Assert.Equal(QuoteStatuses.Draft, quote.Status);
        Assert.Equal(2, quote.CurrentVersionNumber);
        var outcome = Assert.Single(quote.Outcomes);
        Assert.Equal(QuoteOutcomeResponses.Negotiated, outcome.Response);
        Assert.Equal(220m, outcome.Amount);
        Assert.Equal("PHP", outcome.CurrencyCode);
        Assert.DoesNotContain(quote.ApprovalDecisions, item => item.QuoteVersionId == outcome.NewQuoteVersionId);
        var draft = quote.Versions.Single(item => item.VersionNumber == 2);
        Assert.Equal(220m, draft.TotalAmount);
    }

    private sealed record Spoken(string Reply, string Gear, string Voice, bool HumanEscalation);
}
