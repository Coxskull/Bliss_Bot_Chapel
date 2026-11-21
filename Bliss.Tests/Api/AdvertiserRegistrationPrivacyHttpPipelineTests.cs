using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bliss.Tests.Api;

/// <summary>
/// Real ASP.NET TestServer HTTP pipeline, fictional identities and isolated
/// in-memory persistence. Never contacts a real IdP, Railway, Supabase or CRM.
/// PostgreSQL migration/integrity checks run in separate synthetic PG CI jobs.
/// </summary>
public sealed class AdvertiserRegistrationPrivacyHttpPipelineTests
{
    private const string Base = "/api/advertisers/privacy-requests";

    private static HttpClient IdentityClient(AdvertiserPrivacyHttpFactory factory,
        string subject, string mode = "pending", Guid? advertiserId = null)
    {
        var client = factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Fixture-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Fixture-Mode", mode);
        if (advertiserId.HasValue)
            client.DefaultRequestHeaders.Add("X-Fixture-Advertiser-Id", advertiserId.Value.ToString("D"));
        return client;
    }

    private static async Task<string> Csrf(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isAuthenticated").GetBoolean());
        return Assert.IsType<string>(json.RootElement.GetProperty("csrfToken").GetString());
    }

    private static async Task<Guid> Register(HttpClient client, string label)
    {
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        using var response = await client.PostAsJsonAsync("/api/advertisers/register", new
        {
            requestKey = Guid.NewGuid(),
            businessName = label,
            countryCode = "PH",
            website = "https://example.invalid",
            processingConsent = true,
            termsAccepted = true,
            marketingOptIn = false,
            privacyNoticeVersion = AdvertiserRegistrationController.PrivacyNoticeVersion,
            termsVersion = AdvertiserRegistrationController.TermsVersion
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PENDING_REVIEW", json.RootElement.GetProperty("status").GetString());
        return json.RootElement.GetProperty("advertiserId").GetGuid();
    }

    private static async Task<Guid> CreateCase(HttpClient client, Guid key, string kind = "ACCESS")
    {
        using var response = await client.PostAsJsonAsync(Base, new
        {
            requestKey = key,
            requestType = kind
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("RECEIVED", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(key, json.RootElement.GetProperty("requestKey").GetGuid());
        return json.RootElement.GetProperty("caseId").GetGuid();
    }

    [Fact]
    public async Task Http_registration_rejects_excess_writes_with_429_before_database_access()
    {
        // Actual ASP.NET endpoint limiter; fake issuer + a separate in-memory
        // factory. Only intentionally invalid requests: zero database writes.
        await using var factory = new AdvertiserPrivacyHttpFactory(writePermitLimit: 1);
        using var client = IdentityClient(factory, "fixture-throttle");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        var invalid = new
        {
            requestKey = Guid.Empty,
            businessName = "No business created",
            countryCode = "PH",
            website = (string?)null,
            processingConsent = false,
            termsAccepted = false,
            marketingOptIn = false,
            privacyNoticeVersion = AdvertiserRegistrationController.PrivacyNoticeVersion,
            termsVersion = AdvertiserRegistrationController.TermsVersion
        };
        using var first = await client.PostAsJsonAsync("/api/advertisers/register", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        using var throttled = await client.PostAsJsonAsync("/api/advertisers/register", invalid);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Contains("600", throttled.Headers.GetValues("Retry-After"));
    }

    [Fact]
    public async Task Http_customer_campaign_drafts_are_idempotent_owned_and_never_executable()
    {
        await using var isolated = Task3bHttpPostgresDatabase.TryCreate();
        await using var factory = new AdvertiserPrivacyHttpFactory(
            postgresConnectionString: isolated?.ConnectionString, operatorReviewEnabled: true,
            membershipProfileEnabled: true, campaignDraftsEnabled: true);
        const string route = "/api/advertisers/my-campaign-drafts";
        const string review = "/api/advertisers/operator-review";
        using var a = IdentityClient(factory, "fixture-b");
        using var b = IdentityClient(factory, "fixture-c");
        var aId = await Register(a, "Fictional Draft A");
        var bId = await Register(b, "Fictional Draft B");
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync(route)).StatusCode);
        using var staff = IdentityClient(factory, "fixture-operator", "operator");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(route)).StatusCode);
        if (isolated is null) return; // PostgreSQL CI exercises serializable writes.
        using var queue = await staff.GetAsync(review);
        using var json = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());
        staff.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(staff));
        foreach (var pair in new[] { (aId, "fixture-b"), (bId, "fixture-c") })
        {
            var receiptId = json.RootElement.EnumerateArray()
                .Single(x => x.GetProperty("advertiserId").GetGuid() == pair.Item1)
                .GetProperty("receiptId").GetGuid();
            using var approval = await staff.PostAsJsonAsync(review, new { receiptId, advertiserId = pair.Item1,
                subject = pair.Item2, businessIdentityReviewed = true, consentReviewed = true });
            Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        }
        var key = Guid.NewGuid();
        var draft = new { requestKey = key, name = "Fictional campaign", brief = "A fictional planning brief." };
        using var created = await a.PostAsJsonAsync(route, draft);
        // This disposable database and request are entirely fictional; preserve a
        // bounded response detail on failures so CI does not hide the root cause.
        if (created.StatusCode != HttpStatusCode.Created)
        {
            var detail = await created.Content.ReadAsStringAsync();
            Assert.True(false, $"Expected Created; got {created.StatusCode}: {detail[..Math.Min(detail.Length, 6000)]}");
        }
        using var replay = await a.PostAsJsonAsync(route, draft);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync(route,
            new { requestKey = key, name = "Changed name", brief = draft.brief })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.PostAsJsonAsync(route, draft)).StatusCode);
        using var listA = await a.GetAsync(route + "?advertiserId=" + bId);
        Assert.Equal(HttpStatusCode.OK, listA.StatusCode);
        using var own = JsonDocument.Parse(await listA.Content.ReadAsStringAsync());
        Assert.Equal(key, own.RootElement.EnumerateArray().Single().GetProperty("campaignId").GetGuid());
        using var listB = await b.GetAsync(route + "?advertiserId=" + aId);
        Assert.Equal(HttpStatusCode.OK, listB.StatusCode);
        using var other = JsonDocument.Parse(await listB.Content.ReadAsStringAsync());
        Assert.Equal(0, other.RootElement.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/campaigns")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var campaign = await db.Campaigns.SingleAsync(c => c.Id == key);
            Assert.Equal("DRAFT", campaign.Status);
            Assert.Equal("UNVERIFIED", campaign.AuthorityStatus);
            var opp = await db.AdvertiserOpportunities.SingleAsync(o => o.Id == campaign.AdvertiserOpportunityId);
            Assert.Equal("DRAFT", opp.Status);
            Assert.Equal("UNVERIFIED", opp.AuthorityStatus);
            var program = await db.AdvertiserPrograms.SingleAsync(p => p.Id == opp.AdvertiserProgramId);
            Assert.Equal(aId, program.AdvertiserId);
            Assert.Equal("DRAFT", program.Status);
            Assert.False(await db.CampaignPlacements.AnyAsync(p => p.CampaignId == key));
            var membership = await db.AdvertiserAccountMemberships.SingleAsync(m => m.AdvertiserId == aId);
            membership.Status = "REVOKED";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PostAsJsonAsync(route, draft)).StatusCode);
    }

    [Fact]
    public async Task Http_membership_approval_activates_only_own_profile_and_revocation_denies_existing_session()
    {
        await using var isolated = Task3bHttpPostgresDatabase.TryCreate();
        await using var factory = new AdvertiserPrivacyHttpFactory(
            postgresConnectionString: isolated?.ConnectionString, operatorReviewEnabled: true,
            membershipProfileEnabled: true);
        const string review = "/api/advertisers/operator-review";
        const string profile = "/api/advertisers/me";
        using var applicant = IdentityClient(factory, "fixture-b");
        using var other = IdentityClient(factory, "fixture-c");
        using var unregistered = IdentityClient(factory, "fixture-d");
        var advertiserId = await Register(applicant, "Fictional Membership B");
        var otherId = await Register(other, "Fictional Membership C");
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync(profile)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unregistered.GetAsync(profile)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync(review)).StatusCode);
        using var staff = IdentityClient(factory, "fixture-operator", "operator");
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(profile)).StatusCode);
        using var queue = await staff.GetAsync(review);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        using var json = JsonDocument.Parse(await queue.Content.ReadAsStringAsync());
        var receiptId = json.RootElement.EnumerateArray()
            .Single(x => x.GetProperty("advertiserId").GetGuid() == advertiserId)
            .GetProperty("receiptId").GetGuid();
        staff.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(staff));
        if (isolated is null) return; // The atomic approval is exercised in PG18 CI.
        using var approval = await staff.PostAsJsonAsync(review, new
        {
            receiptId, advertiserId, subject = "fixture-b",
            businessIdentityReviewed = true, consentReviewed = true
        });
        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        using var approved = JsonDocument.Parse(await approval.Content.ReadAsStringAsync());
        Assert.Equal("MEMBERSHIP_PROFILE_ENABLED", approved.RootElement.GetProperty("identityProvisioning").GetString());

        // The original restricted applicant session can access its own active
        // profile immediately, without issuer changes or a deployment.
        using var result = await applicant.GetAsync(profile + "?advertiserId=" + otherId.ToString("D"));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var own = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.Equal(advertiserId, own.RootElement.GetProperty("advertiserId").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(profile)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync(review)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync("/api/campaigns")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsJsonAsync(profile, new { advertiserId })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var membership = await db.AdvertiserAccountMemberships.SingleAsync(m => m.AdvertiserId == advertiserId);
            membership.Status = "REVOKED";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync(profile)).StatusCode);
    }

    [Fact]
    public async Task Http_operator_review_requires_explicit_scope_csrf_and_exact_receipt()
    {
        await using var isolated = Task3bHttpPostgresDatabase.TryCreate();
        await using var factory = new AdvertiserPrivacyHttpFactory(
            postgresConnectionString: isolated?.ConnectionString, operatorReviewEnabled: true);
        const string route = "/api/advertisers/operator-review";
        using var a = IdentityClient(factory, "fixture-a");
        using var b = IdentityClient(factory, "fixture-b");
        var advertiserA = await Register(a, "Fictional Review A");
        var advertiserB = await Register(b, "Fictional Review B");
        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync(route)).StatusCode);
        using var otherStaff = IdentityClient(factory, "fixture-other-operator", "operator");
        Assert.Equal(HttpStatusCode.Forbidden, (await otherStaff.GetAsync(route)).StatusCode);
        using var staff = IdentityClient(factory, "fixture-operator", "operator");
        using var rr = await staff.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, rr.StatusCode);
        using var doc = JsonDocument.Parse(await rr.Content.ReadAsStringAsync());
        var row = doc.RootElement.EnumerateArray().Single(x => x.GetProperty("advertiserId").GetGuid() == advertiserA);
        var receipt = row.GetProperty("receiptId").GetGuid();
        var request = new { receiptId = receipt, advertiserId = advertiserA, subject = "fixture-a",
            businessIdentityReviewed = true, consentReviewed = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(route, request)).StatusCode);
        staff.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(staff));
        var mismatch = new { receiptId = receipt, advertiserId = advertiserB, subject = "fixture-b",
            businessIdentityReviewed = true, consentReviewed = true };
        var missingAttestation = new { receiptId = receipt, advertiserId = advertiserA, subject = "fixture-a",
            businessIdentityReviewed = false, consentReviewed = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(route, missingAttestation)).StatusCode);
        if (isolated is null) return; // Atomic relational approval is exercised by PostgreSQL CI.
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(route, mismatch)).StatusCode);
        using var accepted = await staff.PostAsJsonAsync(route, request);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var result = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal("ACTIVE", result.RootElement.GetProperty("status").GetString());
        Assert.Equal("REQUIRED_SEPARATELY", result.RootElement.GetProperty("identityProvisioning").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(route, request)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal("ACTIVE", await db.AdvertiserAccountMemberships.Where(x => x.AdvertiserId == advertiserA).Select(x => x.Status).SingleAsync());
        Assert.Equal("PENDING_REVIEW", await db.AdvertiserAccountMemberships.Where(x => x.AdvertiserId == advertiserB).Select(x => x.Status).SingleAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/advertisers/me")).StatusCode);
    }

    [Fact]
    public async Task Http_registration_case_receipts_tenant_isolation_and_staff_ack_use_real_csrf_pipeline()
    {
        // The successful operator ACK uses EF ExecuteUpdate, which only a real
        // relational provider can execute. CI supplies a localhost PostgreSQL
        // service; the fixture creates and destroys its own database.
        await using var isolated = Task3bHttpPostgresDatabase.TryCreate();
        await using var factory = new AdvertiserPrivacyHttpFactory(
            postgresConnectionString: isolated?.ConnectionString);
        using var a = IdentityClient(factory, "fixture-a");
        using var b = IdentityClient(factory, "fixture-b");
        var advertiserA = await Register(a, "Fictional A Studio");
        var advertiserB = await Register(b, "Fictional B Studio");
        Assert.NotEqual(advertiserA, advertiserB);

        // The dashboard's status read must bind to the signed-in subject,
        // never a receipt or advertiser ID supplied by a browser.
        const string statusPath = "/api/advertisers/registration-status";
        using var statusA = await a.GetAsync(statusPath);
        using var statusB = await b.GetAsync(statusPath);
        Assert.Equal(HttpStatusCode.OK, statusA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, statusB.StatusCode);
        using var ownA = JsonDocument.Parse(await statusA.Content.ReadAsStringAsync());
        using var ownB = JsonDocument.Parse(await statusB.Content.ReadAsStringAsync());
        var receiptA = ownA.RootElement.GetProperty("receiptId").GetGuid();
        var receiptB = ownB.RootElement.GetProperty("receiptId").GetGuid();
        Assert.NotEqual(receiptA, receiptB);
        Assert.Equal("PENDING_REVIEW", ownA.RootElement.GetProperty("status").GetString());
        Assert.Equal("PENDING_REVIEW", ownB.RootElement.GetProperty("status").GetString());
        Assert.True(ownA.RootElement.GetProperty("processingConsent").GetBoolean());
        Assert.True(ownA.RootElement.GetProperty("termsAccepted").GetBoolean());
        Assert.False(ownA.RootElement.GetProperty("marketingOptIn").GetBoolean());
        Assert.False(ownA.RootElement.TryGetProperty("subject", out _));
        Assert.False(ownA.RootElement.TryGetProperty("advertiserId", out _));
        Assert.False(ownA.RootElement.TryGetProperty("requestKey", out _));
        using var overrideAttempt = await b.GetAsync(statusPath + "?receiptId=" + receiptA.ToString("D")
            + "&advertiserId=" + advertiserA.ToString("D"));
        Assert.Equal(HttpStatusCode.OK, overrideAttempt.StatusCode);
        using var overrideJson = JsonDocument.Parse(await overrideAttempt.Content.ReadAsStringAsync());
        Assert.Equal(receiptB, overrideJson.RootElement.GetProperty("receiptId").GetGuid());
        using var noRegistration = IdentityClient(factory, "fixture-no-registration");
        using var noReceipt = await noRegistration.GetAsync(statusPath);
        Assert.Equal(HttpStatusCode.NotFound, noReceipt.StatusCode);
        using var anonymousStatusClient = factory.CreateSecureClient();
        using var anonymousStatus = await anonymousStatusClient.GetAsync(statusPath);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus.StatusCode);
        using var pendingProfile = await a.GetAsync("/api/advertisers/me");
        Assert.Equal(HttpStatusCode.Forbidden, pendingProfile.StatusCode);


        var key = Guid.NewGuid();
        var caseId = await CreateCase(a, key, "MARKETING_WITHDRAWAL");
        using var replay = await a.PostAsJsonAsync(Base, new { requestKey = key, requestType = "MARKETING_WITHDRAWAL" });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using (var json = JsonDocument.Parse(await replay.Content.ReadAsStringAsync()))
        {
            Assert.Equal(caseId, json.RootElement.GetProperty("caseId").GetGuid());
            Assert.Equal(key, json.RootElement.GetProperty("requestKey").GetGuid());
        }
        using var wrongType = await a.PostAsJsonAsync(Base, new { requestKey = key, requestType = "ERASURE" });
        Assert.Equal(HttpStatusCode.Conflict, wrongType.StatusCode);
        using var crossTenant = await b.PostAsJsonAsync(Base, new { requestKey = key, requestType = "MARKETING_WITHDRAWAL" });
        Assert.Equal(HttpStatusCode.Conflict, crossTenant.StatusCode);
        using var invalid = await a.PostAsJsonAsync(Base, new { requestKey = Guid.NewGuid(), requestType = "EVERY_TENANT_EXPORT" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var own = await a.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using (var json = JsonDocument.Parse(await own.Content.ReadAsStringAsync()))
        {
            Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(caseId, json.RootElement[0].GetProperty("caseId").GetGuid());
            Assert.Equal(key, json.RootElement[0].GetProperty("requestKey").GetGuid());
        }
        using var otherOwn = await b.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, otherOwn.StatusCode);
        using (var json = JsonDocument.Parse(await otherOwn.Content.ReadAsStringAsync()))
            Assert.Empty(json.RootElement.EnumerateArray());

        using var forbiddenQueue = await a.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenQueue.StatusCode);
        using var forbiddenAck = await a.PostAsJsonAsync(Base + "/" + caseId + "/acknowledge", new { });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAck.StatusCode);
        using var forbiddenMarketplace = await a.GetAsync("/api/creators");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenMarketplace.StatusCode);

        using var operatorClient = IdentityClient(factory, "fixture-operator", "operator");
        operatorClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(operatorClient));
        using var queue = await operatorClient.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        using (var json = JsonDocument.Parse(await queue.Content.ReadAsStringAsync()))
        {
            Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(caseId, json.RootElement[0].GetProperty("caseId").GetGuid());
            // Operator queue is deliberately a different minimal DTO.
            // It must not disclose the applicant's replay/idempotency key.
            Assert.False(json.RootElement[0].TryGetProperty("requestKey", out _));
        }
        if (isolated is null) return; // Local source-only builds lack Postgres; CI exercises ACK.

        using var acknowledged = await operatorClient.PostAsJsonAsync(Base + "/" + caseId + "/acknowledge", new { });
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        using (var json = JsonDocument.Parse(await acknowledged.Content.ReadAsStringAsync()))
            Assert.Equal("VERIFYING", json.RootElement.GetProperty("status").GetString());
        using var repeatAck = await operatorClient.PostAsJsonAsync(Base + "/" + caseId + "/acknowledge", new { });
        Assert.Equal(HttpStatusCode.Conflict, repeatAck.StatusCode);

        using var after = await a.GetAsync(Base);
        using (var json = JsonDocument.Parse(await after.Content.ReadAsStringAsync()))
            Assert.Equal("VERIFYING", json.RootElement[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Http_pending_and_approved_sessions_require_valid_csrf_and_exact_case_scope()
    {
        await using var factory = new AdvertiserPrivacyHttpFactory();
        using var anonymous = factory.CreateSecureClient();
        using var denyAnonymous = await anonymous.GetAsync(Base);
        Assert.Equal(HttpStatusCode.Unauthorized, denyAnonymous.StatusCode);
        using var anonymousPost = await anonymous.PostAsJsonAsync(Base, new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPost.StatusCode);

        using var pending = IdentityClient(factory, "fixture-a");
        using var noToken = await pending.PostAsJsonAsync("/api/advertisers/register",
            new { requestKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Contains("CSRF", await noToken.Content.ReadAsStringAsync());
        var token = await Csrf(pending);
        pending.DefaultRequestHeaders.Add("RequestVerificationToken", token);
        using var wrongHeader = await pending.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongHeader.StatusCode);
        pending.DefaultRequestHeaders.Remove("RequestVerificationToken");
        pending.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

        var advertiserId = await RegisterWithoutResettingCsrf(pending, "Fictional Pending Studio");
        using var secondApplicant = IdentityClient(factory, "fixture-b");
        var otherAdvertiserId = await Register(secondApplicant, "Fictional Other Studio");
        using var unapprovedProfile = IdentityClient(factory, "fixture-a", "approved", advertiserId);
        using var blockedPending = await unapprovedProfile.GetAsync("/api/advertisers/me");
        Assert.Equal(HttpStatusCode.Forbidden, blockedPending.StatusCode);
        using var wrongTenant = IdentityClient(factory, "fixture-a", "approved", otherAdvertiserId);
        using var blockedWrongTenant = await wrongTenant.GetAsync("/api/advertisers/me");
        Assert.Equal(HttpStatusCode.Forbidden, blockedWrongTenant.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var membership = await db.AdvertiserAccountMemberships.SingleAsync(x => x.Subject == "fixture-a");
            Assert.Equal(advertiserId, membership.AdvertiserId);
            membership.Status = "ACTIVE";
            await db.SaveChangesAsync();
        }
        using var approved = IdentityClient(factory, "fixture-a", "approved", advertiserId);
        approved.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(approved));
        using var ownProfile = await approved.GetAsync("/api/advertisers/me");
        Assert.Equal(HttpStatusCode.OK, ownProfile.StatusCode);
        using (var ownProfileJson = JsonDocument.Parse(await ownProfile.Content.ReadAsStringAsync()))
            Assert.Equal(advertiserId, ownProfileJson.RootElement.GetProperty("advertiserId").GetGuid());
        using var wrongAfterApproval = await wrongTenant.GetAsync("/api/advertisers/me");
        Assert.Equal(HttpStatusCode.Forbidden, wrongAfterApproval.StatusCode);
        using var getOwn = await approved.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, getOwn.StatusCode);
        using var created = await approved.PostAsJsonAsync(Base, new
        {
            requestKey = Guid.NewGuid(), requestType = "ACCESS"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var forbiddenQueue = await approved.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenQueue.StatusCode);
        using var forbiddenUnknown = await approved.GetAsync(Base + "/other-tenant");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUnknown.StatusCode);
        using var forbiddenMarketplace = await approved.GetAsync("/api/creators");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenMarketplace.StatusCode);
    }

    private static async Task<Guid> RegisterWithoutResettingCsrf(HttpClient client, string label)
    {
        using var response = await client.PostAsJsonAsync("/api/advertisers/register", new
        {
            requestKey = Guid.NewGuid(), businessName = label, countryCode = "PH",
            website = "https://example.invalid", processingConsent = true,
            termsAccepted = true, marketingOptIn = false,
            privacyNoticeVersion = AdvertiserRegistrationController.PrivacyNoticeVersion,
            termsVersion = AdvertiserRegistrationController.TermsVersion
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("advertiserId").GetGuid();
    }

    [Fact]
    public async Task Http_signup_shutdown_preserves_existing_pending_own_rights_but_denies_new_registration_and_other_APIs()
    {
        // Production feature flags can change while a signed pending cookie
        // remains valid. Fake identity only; no remote Auth, CRM or user data.
        await using var isolated = Task3bHttpPostgresDatabase.TryCreate();
        await using var factory = new AdvertiserPrivacyHttpFactory(
            postgresConnectionString: isolated?.ConnectionString, registrationEnabled: false);
        const string applicantSubject = "fixture-a";
        var advertiserId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            db.Advertisers.Add(new Advertiser
            {
                Id = advertiserId, Name = "Fictional Rights Continuity",
                CountryCode = "PH", CreatedAt = DateTime.UtcNow
            });
            db.AdvertiserAccountMemberships.Add(new AdvertiserAccountMembership
            {
                Id = Guid.NewGuid(), AdvertiserId = advertiserId,
                Subject = applicantSubject, Status = "PENDING_REVIEW",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var applicant = IdentityClient(factory, applicantSubject);
        applicant.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(applicant));
        using var blockedLegacy = await applicant.GetAsync("/api/creators");
        Assert.Equal(HttpStatusCode.Forbidden, blockedLegacy.StatusCode);
        using var blockedRegistration = await applicant.PostAsJsonAsync("/api/advertisers/register",
            new { requestKey = Guid.NewGuid(), businessName = "Should not create",
                  countryCode = "PH", processingConsent = true, termsAccepted = true,
                  marketingOptIn = false,
                  privacyNoticeVersion = AdvertiserRegistrationController.PrivacyNoticeVersion,
                  termsVersion = AdvertiserRegistrationController.TermsVersion });
        Assert.Equal(HttpStatusCode.Forbidden, blockedRegistration.StatusCode);
        using var caseRead = await applicant.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, caseRead.StatusCode);
        using var casePost = await applicant.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.Created, casePost.StatusCode);
        using var staff = await applicant.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.Forbidden, staff.StatusCode);

        using var unrelated = IdentityClient(factory, "fixture-b");
        unrelated.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(unrelated));
        using var missingMemberRead = await unrelated.GetAsync(Base);
        Assert.Equal(HttpStatusCode.Forbidden, missingMemberRead.StatusCode);
        using var missingMemberWrite = await unrelated.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.Forbidden, missingMemberWrite.StatusCode);

        using var scopeAfter = factory.Services.CreateScope();
        var check = scopeAfter.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await check.Advertisers.AsNoTracking().ToListAsync());
        Assert.Empty(await check.AdvertiserRegistrationReceipts.AsNoTracking().ToListAsync());
        Assert.Single(await check.AdvertiserPrivacyRequests.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Http_revoked_membership_retains_exact_privacy_intake_without_regaining_workspace()
    {
        // Historical registered users retain lawful data-rights intake when
        // their account is revoked. No API activation, restoration or erasure.
        await using var isolated = Task3bHttpPostgresDatabase.TryCreate();
        await using var factory = new AdvertiserPrivacyHttpFactory(
            postgresConnectionString: isolated?.ConnectionString,
            registrationEnabled: false);
        var advertiserId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            db.Advertisers.Add(new Advertiser
            {
                Id = advertiserId, Name = "Fictional Revoked Account",
                CountryCode = "PH", CreatedAt = DateTime.UtcNow
            });
            db.AdvertiserAccountMemberships.Add(new AdvertiserAccountMembership
            {
                Id = Guid.NewGuid(), AdvertiserId = advertiserId,
                Subject = "fixture-a", Status = "REVOKED", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var pending = IdentityClient(factory, "fixture-a");
        pending.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(pending));
        var requestKey = Guid.NewGuid();
        using var privacyRequest = await pending.PostAsJsonAsync(Base, new
        {
            requestKey, requestType = "ERASURE"
        });
        Assert.Equal(HttpStatusCode.Created, privacyRequest.StatusCode);
        using var replay = await pending.PostAsJsonAsync(Base, new
        {
            requestKey, requestType = "ERASURE"
        });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var myCases = await pending.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, myCases.StatusCode);
        using (var json = JsonDocument.Parse(await myCases.Content.ReadAsStringAsync()))
        {
            Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal("ERASURE", json.RootElement[0].GetProperty("requestType").GetString());
            Assert.Equal("RECEIVED", json.RootElement[0].GetProperty("status").GetString());
        }
        using var registration = await pending.PostAsJsonAsync("/api/advertisers/register",
            new { requestKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, registration.StatusCode);
        using var legacy = await pending.GetAsync("/api/creators");
        Assert.Equal(HttpStatusCode.Forbidden, legacy.StatusCode);
        using var queue = await pending.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.Forbidden, queue.StatusCode);

        using var approved = IdentityClient(factory, "fixture-a", "approved", advertiserId);
        approved.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(approved));
        using var profile = await approved.GetAsync("/api/advertisers/me");
        Assert.Equal(HttpStatusCode.Forbidden, profile.StatusCode);
        using var approvedPrivacy = await approved.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, approvedPrivacy.StatusCode);
        using var approvedCase = await approved.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "CORRECTION" });
        Assert.Equal(HttpStatusCode.Created, approvedCase.StatusCode);

        using var other = IdentityClient(factory, "fixture-b");
        other.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(other));
        using var otherPrivacy = await other.GetAsync(Base);
        Assert.Equal(HttpStatusCode.Forbidden, otherPrivacy.StatusCode);
        using var otherWrite = await other.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.Forbidden, otherWrite.StatusCode);

        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await check.AdvertiserAccountMemberships.AsNoTracking().ToListAsync());
        Assert.Equal("REVOKED", (await check.AdvertiserAccountMemberships.AsNoTracking()
            .SingleAsync()).Status);
        Assert.Equal(2, await check.AdvertiserPrivacyRequests.AsNoTracking().CountAsync());
        Assert.Empty(await check.AdvertiserRegistrationReceipts.AsNoTracking().ToListAsync());
        // Cases are only RECEIVED; no automatic erasure or account reactivation.
    }

    [Fact]
    public async Task Http_privacy_feature_off_denies_intake_despite_registration_being_enabled()
    {
        await using var factory = new AdvertiserPrivacyHttpFactory(privacyEnabled: false);
        using var applicant = IdentityClient(factory, "fixture-a");
        var advertiserId = await Register(applicant, "Fictional Rights Disabled Studio");
        Assert.NotEqual(Guid.Empty, advertiserId);
        using var denyRead = await applicant.GetAsync(Base);
        Assert.Equal(HttpStatusCode.Forbidden, denyRead.StatusCode);
        using var denyWrite = await applicant.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.Forbidden, denyWrite.StatusCode);
    }
}

/// <summary>
/// One test-owned database on local CI PostgreSQL only. No hosted or remote
/// target is eligible; all storage is synthetic and removed at test exit.
/// </summary>
internal sealed class Task3bHttpPostgresDatabase : IAsyncDisposable
{
    private readonly string adminConnection;
    private readonly string databaseName;
    public string ConnectionString { get; }

    private Task3bHttpPostgresDatabase(string adminConnection, string databaseName, string connectionString)
    {
        this.adminConnection = adminConnection;
        this.databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public static Task3bHttpPostgresDatabase? TryCreate()
    {
        var source = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        // The dedicated PG18 registration/rights acceptance job must never
        // silently fall back to EF InMemory. Local no-Postgres source tests
        // retain their existing lightweight behavior.
        var requirePg18 = Environment.GetEnvironmentVariable("TASK3B_REQUIRE_PG18") == "1";
        if (string.IsNullOrWhiteSpace(source))
        {
            if (requirePg18)
                throw new InvalidOperationException("TASK3B_PG18_RELATIONAL_TARGET_REQUIRED");
            return null;
        }
        var builder = new NpgsqlConnectionStringBuilder(source);
        if (!string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && builder.Host != "127.0.0.1" && builder.Host != "::1")
            throw new InvalidOperationException("Task #3B HTTP integration requires localhost PostgreSQL.");

        var name = "task3b_http_" + Guid.NewGuid().ToString("N");
        using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            admin.Open();
            if (requirePg18)
            {
                using var major = new NpgsqlCommand("SHOW server_version_num", admin);
                var version = int.Parse((string)(major.ExecuteScalar()
                    ?? throw new InvalidOperationException("TASK3B_PG18_VERSION_UNAVAILABLE")),
                    System.Globalization.CultureInfo.InvariantCulture);
                if (version < 180000 || version >= 190000)
                    throw new InvalidOperationException("TASK3B_PG18_VERSION_MISMATCH");
            }
            using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            create.ExecuteNonQuery();
        }
        var scoped = new NpgsqlConnectionStringBuilder(source) { Database = name };
        var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scoped.ConnectionString).Options;
        using var db = new BlissDbContext(options);
        db.Database.Migrate();
        return new Task3bHttpPostgresDatabase(builder.ConnectionString, name, scoped.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

internal sealed class AdvertiserPrivacyHttpFactory(bool privacyEnabled = true,
    string? postgresConnectionString = null, int? writePermitLimit = null,
    bool registrationEnabled = true, bool operatorReviewEnabled = false,
    bool membershipProfileEnabled = false, bool campaignDraftsEnabled = false) : WebApplicationFactory<Program>
{
    private const string Issuer = "https://identity.example.test";
    private readonly string dbName = "task3b_http_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:SupabaseEnabled", "true");
        builder.UseSetting("Authentication:Authority", Issuer);
        builder.UseSetting("Authentication:ClientId", "synthetic-http-fixture");
        builder.UseSetting("Authentication:AdvertiserRegistrationRequested", registrationEnabled ? "true" : "false");
        builder.UseSetting("Authentication:AdvertiserSelfServiceRequested", "true");
        builder.UseSetting("Authentication:AdvertiserMembershipSelfServiceRequested", membershipProfileEnabled ? "true" : "false");
        builder.UseSetting("Authentication:AdvertiserCampaignDraftsRequested", campaignDraftsEnabled ? "true" : "false");
        builder.UseSetting("Authentication:AdvertiserPrivacyRequestsRequested", privacyEnabled ? "true" : "false");
        builder.UseSetting("Authentication:ApprovedAdvertiserSubjects:0", "fixture-a");
        builder.UseSetting("Authentication:ApprovedPrivacyOperatorSubjects:0", "fixture-operator");
        builder.UseSetting("Authentication:AdvertiserOperatorReviewRequested", operatorReviewEnabled ? "true" : "false");
        builder.UseSetting("Authentication:ApprovedRegistrationOperatorSubjects:0", "fixture-operator");
        if (operatorReviewEnabled)
            builder.UseSetting("Authentication:ApprovedTestSubjects:0", "fixture-operator");
        if (writePermitLimit.HasValue)
        {
            builder.UseSetting("Runtime:WriteRateLimitPermitLimit", writePermitLimit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Runtime:RateLimitWindowSeconds", "600");
        }

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                || d.ServiceType == typeof(BlissDbContext)).ToArray();
            foreach (var item in toRemove) services.Remove(item);
            if (!string.IsNullOrWhiteSpace(postgresConnectionString))
                services.AddDbContext<BlissDbContext>(options => options.UseNpgsql(postgresConnectionString));
            else
                // The registration controller begins an EF transaction. InMemory
                // cannot enforce transactions; this is only for no-Postgres tests.
                services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(dbName)
                    .ConfigureWarnings(warnings => warnings.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AdvertiserPrivacyTestAuthHandler.FixtureScheme;
                options.DefaultChallengeScheme = AdvertiserPrivacyTestAuthHandler.FixtureScheme;
                options.DefaultForbidScheme = AdvertiserPrivacyTestAuthHandler.FixtureScheme;
            }).AddScheme<AuthenticationSchemeOptions, AdvertiserPrivacyTestAuthHandler>(
                AdvertiserPrivacyTestAuthHandler.FixtureScheme, _ => { });
        });
    }

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    });
}

/// <summary>Test-only fake issuer. No production authentication route or user ever created.</summary>
internal sealed class AdvertiserPrivacyTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string FixtureScheme = "Task3bSyntheticOnly";
    private const string Issuer = "https://identity.example.test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Fixture-Subject", out var subjects)
            || subjects.Count != 1 || string.IsNullOrWhiteSpace(subjects[0]))
            return Task.FromResult(AuthenticateResult.NoResult());

        var subject = subjects[0]!;
        var mode = Request.Headers.TryGetValue("X-Fixture-Mode", out var modes)
            ? modes.ToString() : "pending";
        var claims = new List<Claim> { new("sub", subject) };
        switch (mode)
        {
            case "pending":
                claims.Add(new Claim("email_verified", "true"));
                claims.Add(new Claim(BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
                    "true", ClaimValueTypes.Boolean, Issuer, Issuer));
                break;
            case "approved":
                if (!Request.Headers.TryGetValue("X-Fixture-Advertiser-Id", out var id)
                    || !Guid.TryParse(id.ToString(), out var parsed) || parsed == Guid.Empty)
                    return Task.FromResult(AuthenticateResult.Fail("No fictional advertiser scope."));
                claims.Add(new Claim("roles", "bliss.advertiser", ClaimValueTypes.String, Issuer, Issuer));
                claims.Add(new Claim("advertiser_id", parsed.ToString("D"), ClaimValueTypes.String, Issuer, Issuer));
                claims.Add(new Claim(BlissAuthenticationOptions.AdvertiserSelfServiceMarkerClaimType,
                    "true", ClaimValueTypes.Boolean, Issuer, Issuer));
                break;
            case "operator":
                claims.Add(new Claim("roles", "bliss.operator", ClaimValueTypes.String, Issuer, Issuer));
                break;
            default:
                return Task.FromResult(AuthenticateResult.Fail("Unknown fictional test identity."));
        }

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, FixtureScheme)),
            FixtureScheme)));
    }
}
