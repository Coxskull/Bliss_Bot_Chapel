using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Financials;
using Bliss.Domain.Entities;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Financials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Bliss.Tests.Api;

public sealed class FinancialBoundMockApiTests(WeddingPlannerOidcFactory factory) : IClassFixture<WeddingPlannerOidcFactory>
{
    private sealed record Prepared(PreparedPaymentOperation Operation, string StripeAccountStatus);
    private sealed record Journal(Guid JournalId, bool PaymentCompleted, bool ProviderCalled, string StripeAccountStatus);
    [Fact]
    public async Task Disabled_or_real_provider_modes_refuse_mock_callback_before_database_access()
    {
        foreach (var config in new[] { (Enabled: false, Mode: "Mock"), (Enabled: true, Mode: "Sandbox"), (Enabled: true, Mode: "Live") })
        {
            await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            { services.RemoveAll<FinancialPreparationOptions>(); services.AddSingleton(new FinancialPreparationOptions { Enabled = config.Enabled, Mode = config.Mode }); }));
            using var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), HandleCookies = true });
            client.DefaultRequestHeaders.Add("X-Test-Name", "ModeQA"); client.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.operator");
            var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
            foreach(var route in new[]{"mock-bound-webhooks","mock-status-webhooks"})Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/financials/"+route, new { operationId = Guid.NewGuid(), rawJson = "{}", signature = "invalid" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/financials/mock-payments/reconcile",new{operationId=Guid.NewGuid()})).StatusCode);
        }
    }
    [FinancialPostgresFact]
    public async Task Bound_mock_callbacks_isolate_profiles_scope_source_and_roles_deduplicate_and_refund_three_modules()
    {
        var name = "BoundOwner-" + Guid.NewGuid().ToString("N"); var advertiser = Guid.NewGuid();
        var profiles = new[] { new MockAccountProfile("mock-one", "whsec_fixture_one"), new MockAccountProfile("mock-two", "whsec_fixture_two") };
        var bindings = Enum.GetValues<FinancialModule>().Select((module, index) => new FundingBinding(Guid.NewGuid(), Guid.NewGuid(),
            new(Guid.NewGuid(), module, Guid.NewGuid()), "test-" + name, advertiser, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), profiles[index % 2].Id)).ToList();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<FinancialPreparationOptions>(); services.AddSingleton(new FinancialPreparationOptions
            {
                Enabled = true, Mode = "Mock", FixtureConnection = Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!,
                Bindings = bindings, MockAccountProfiles = profiles.ToList(), MockSigningSecret = "whsec_fixture_legacy",
                MockReviewers = bindings.Select(x => new MockReviewerScope("test-BoundQA-" + x.Scope.Module, x.Scope)).ToList()
            });
        }));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            foreach (var b in bindings) db.AddRange(new Quote { Id = b.SourceQuoteId, Status = "ACCEPTED", CurrencyCode = "USD", CurrentVersionNumber = 1 },
                new QuoteVersion { Id = b.SourceVersionId, QuoteId = b.SourceQuoteId, VersionNumber = 1, TotalAmount = 10, CurrencyCode = "USD" },
                new QuoteApprovalDecision { Id = Guid.NewGuid(), QuoteId = b.SourceQuoteId, QuoteVersionId = b.SourceVersionId, Decision = "APPROVED", CreatedAt = DateTime.UtcNow },
                new QuoteOutcome { Id = Guid.NewGuid(), QuoteId = b.SourceQuoteId, QuoteVersionId = b.SourceVersionId, Response = "ACCEPTED", Amount = 10, CurrencyCode = "USD", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        async Task<HttpClient> Client(string subject, string role)
        {
            var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost"), HandleCookies = true });
            client.DefaultRequestHeaders.Add("X-Test-Name", subject); client.DefaultRequestHeaders.Add("X-Test-Roles", role);
            client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiser.ToString());
            var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken); return client;
        }
        using var owner = await Client(name, "bliss.advertiser");
        await using var data = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!);
        foreach (var b in bindings)
        {
            using var staff = await Client("BoundQA-" + b.Scope.Module, "bliss.operator");
            var prepared = await owner.PostAsJsonAsync("/api/financials/payment-operations", new { quoteId = b.SourceQuoteId, idempotencyKey = b.Scope.Module.ToString() });
            Assert.Equal(HttpStatusCode.OK, prepared.StatusCode); var body = (await prepared.Content.ReadFromJsonAsync<Prepared>())!;
            Assert.Equal(FinancialAccountPolicy.StripeAccountStatus, body.StripeAccountStatus); var op = body.Operation;
            var dispatch = new PaymentDispatchStore(data); var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test-" + name)], "fixture"));
            var claim = await dispatch.Claim(user, op.Id); var pi = "pi_fixture_" + op.Id.ToString("N");
            Assert.True(await dispatch.Complete(claim, new(pi, "requires_payment_method", 1000, "USD")));
            var profile = profiles.Single(x => x.Id == b.MockProfileId); var eventId = "evt_" + Guid.NewGuid().ToString("N");
            string Raw(string id, string profileId, string? funding = null, string? intent = null, bool live = false, long amount = 1000, string currency = "usd", string? module = null) =>
                JsonSerializer.Serialize(new { id, livemode = live, type = "payment_intent.succeeded", data = new { @object = new
                { id = intent ?? pi, @object = "payment_intent", livemode = live, status = "succeeded", amount, amount_received = amount, currency,
                    metadata = new { merchant_id = op.Scope.MerchantId, campaign_id = op.Scope.CampaignId, module = module ?? op.Scope.Module.ToString(), funding_reference = funding ?? op.QuoteId.ToString("N"), account_profile = profileId } } } });
            object Request(string raw, string secret)
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var signature = "t=" + timestamp + ",v1=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + raw)));
                return new { operationId = op.Id, rawJson = raw, signature };
            }
            var raw = Raw(eventId, profile.Id); var request = Request(raw, profile.SigningSecret);
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/financials/mock-bound-webhooks", request)).StatusCode);
            foreach (var invalid in new[] { Raw(eventId, "mock-wrong"), Raw(eventId, profile.Id, funding: Guid.NewGuid().ToString("N")), Raw(eventId, profile.Id, intent: "pi_fixture_foreign"), Raw(eventId, profile.Id, live: true), Raw(eventId, profile.Id, amount: 999), Raw(eventId, profile.Id, currency: "php"), Raw(eventId, profile.Id, module: "wrong") })
                Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/financials/mock-bound-webhooks", Request(invalid, profile.SigningSecret))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/financials/mock-bound-webhooks", Request(raw, profiles.Single(x => x.Id != profile.Id).SigningSecret))).StatusCode);
            using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>(); (await db.Quotes.FindAsync(b.SourceQuoteId))!.Status = "REJECTED"; await db.SaveChangesAsync(); }
            Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync("/api/financials/mock-bound-webhooks", request)).StatusCode);
            using (var scope = app.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>(); (await db.Quotes.FindAsync(b.SourceQuoteId))!.Status = "ACCEPTED"; await db.SaveChangesAsync(); }
            // BEFORE the signed mock callback, the new read-only Mission Control
            // endpoint must show the stored operation but no journal, revenue,
            // commission or creator-payable entitlement.
            var evidencePath = "/api/financials/mock-review/cases/" + op.Id + "/campaign-evidence";
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(evidencePath)).StatusCode);
            var beforePayment = (await staff.GetFromJsonAsync<JsonElement>(evidencePath)).GetProperty("result");
            Assert.Equal(op.Id, beforePayment.GetProperty("operationId").GetGuid());
            Assert.Equal(op.Scope.CampaignId, beforePayment.GetProperty("scope").GetProperty("campaignId").GetGuid());
            Assert.Equal(op.AmountMinor, long.Parse(beforePayment.GetProperty("amountMinor").GetString()!));
            Assert.Equal(JsonValueKind.Null, beforePayment.GetProperty("captureJournalId").ValueKind);
            Assert.Empty(beforePayment.GetProperty("lines").EnumerateArray());
            Assert.False(beforePayment.GetProperty("paymentCompleted").GetBoolean());
            Assert.False(beforePayment.GetProperty("creatorPayoutAuthorized").GetBoolean());
            Assert.Equal("NO_APPROVED_ALLOCATION", beforePayment.GetProperty("alphaCommissionStatus").GetString());
            var captures = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => staff.PostAsJsonAsync("/api/financials/mock-bound-webhooks", request)));
            Guid journal = Guid.Empty;
            foreach (var capture in captures)
            {
                Assert.Equal(HttpStatusCode.OK, capture.StatusCode); var result = (await capture.Content.ReadFromJsonAsync<Journal>())!;
                if (journal == Guid.Empty) journal = result.JournalId; Assert.Equal(journal, result.JournalId);
                Assert.False(result.PaymentCompleted); Assert.False(result.ProviderCalled); Assert.Equal(FinancialAccountPolicy.StripeAccountStatus, result.StripeAccountStatus); capture.Dispose();
            }
            // AFTER eight concurrent exact signed callbacks, there is still
            // one capture and one balanced mock journal (not 8 transactions).
            var campaignEvidence = (await staff.GetFromJsonAsync<JsonElement>(evidencePath)).GetProperty("result");
            Assert.Equal(journal, campaignEvidence.GetProperty("captureJournalId").GetGuid());
            Assert.Equal("MOCK", campaignEvidence.GetProperty("mode").GetString());
            Assert.False(campaignEvidence.GetProperty("paymentCompleted").GetBoolean());
            Assert.False(campaignEvidence.GetProperty("providerCalled").GetBoolean());
            Assert.False(campaignEvidence.GetProperty("campaignDeliveryAuthorized").GetBoolean());
            Assert.Equal("NO_APPROVED_ALLOCATION", campaignEvidence.GetProperty("alphaCommissionStatus").GetString());
            Assert.Equal("NO_APPROVED_ALLOCATION", campaignEvidence.GetProperty("creatorCompensationStatus").GetString());
            var lines = campaignEvidence.GetProperty("lines").EnumerateArray().ToArray();
            Assert.Equal(2, lines.Length);
            Assert.Equal(op.AmountMinor, long.Parse(lines.Single(x => x.GetProperty("side").GetString()=="Debit").GetProperty("amountMinor").GetString()!));
            Assert.Equal(op.AmountMinor, long.Parse(lines.Single(x => x.GetProperty("side").GetString()=="Credit").GetProperty("amountMinor").GetString()!));
            Assert.All(lines, x => Assert.StartsWith("TEST_", x.GetProperty("role").GetString()));
            Assert.Matches("^[0-9A-F]{64}$", campaignEvidence.GetProperty("sourceEvidenceSha256").GetString()!);
            var second = await staff.PostAsJsonAsync("/api/financials/mock-bound-webhooks", Request(Raw("evt_" + Guid.NewGuid().ToString("N"), profile.Id), profile.SigningSecret));
            Assert.Equal(HttpStatusCode.OK, second.StatusCode); Assert.Equal(journal, (await second.Content.ReadFromJsonAsync<Journal>())!.JournalId);
            // A valid re-signed but changed body with the original event ID conflicts with immutable evidence.
            Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync("/api/financials/mock-bound-webhooks", Request(raw + " ", profile.SigningSecret))).StatusCode);
            await using (var count = data.CreateCommand("SELECT count(*) FROM financials.mock_provider_events WHERE operation_id=@i")) { count.Parameters.AddWithValue("i", op.Id); Assert.Equal(2L, await count.ExecuteScalarAsync()); }
            var legacyRaw = Raw("evt_" + Guid.NewGuid().ToString("N"), profile.Id, intent: "pi_mock_" + op.Id.ToString("N"));
            Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync("/api/financials/mock-webhooks", Request(legacyRaw, "whsec_fixture_legacy"))).StatusCode);
            var refund = await staff.PostAsJsonAsync("/api/financials/mock-refunds", new { operationId = op.Id, amountMinor = 400, idempotencyKey = "bound-partial" });
            Assert.Equal(HttpStatusCode.OK, refund.StatusCode); var refundId = (await refund.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refund").GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync("/api/financials/mock-refunds/reconcile", new { refundId, status = "SUCCEEDED" })).StatusCode);
        }
    }
}
