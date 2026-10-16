using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: in-memory HTTP TestServer integration regression.
// Reuses existing fake authentication only; no real signed OIDC provider,
// actual visitor consent or deployment. Never sends real data externally.
public sealed class AskAlphaVisitorConsentHttpPipelineTests
    : IClassFixture<OidcSecurityApiFactory>
{
    private readonly OidcSecurityApiFactory factory;
    private const string Base="/api/ask-alpha/visitor-consent";

    public AskAlphaVisitorConsentHttpPipelineTests(OidcSecurityApiFactory factory)
        => this.factory=factory;

    private static object Consent(bool selected) => new
    {
        purpose="CREATIVE_PREVIEW_ONLY",
        consentVersion="AGM-CONSENT-PREVIEW-V1",
        explicitlySelected=selected,
        requestKey="fixture_consent_http_12345"
    };

    private HttpClient TestVisitor()
    {
        var client=factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name","Fictional HTTP Visitor");
        client.DefaultRequestHeaders.Add("X-Test-Roles","bliss.ask_alpha_visitor");
        return client;
    }

    private static async Task<string> CsrfToken(HttpClient client)
    {
        var response=await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("authenticationEnabled").GetBoolean());
        return Assert.IsType<string>(
            json.RootElement.GetProperty("csrfToken").GetString());
    }

    private static async Task AssertDenied(HttpResponseMessage response,
        HttpStatusCode expected)
    {
        Assert.Equal(expected,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync();
        if (expected==HttpStatusCode.Conflict)
        {
            using var json=JsonDocument.Parse(body);
            var root=json.RootElement;
            Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
                root.GetProperty("status").GetString());
            Assert.Equal("NOT_RECORDED",root.GetProperty("visitorConsentStatus").GetString());
            foreach(var flag in new [] {
                "authoritativeOwnershipVerified", "authenticatedVisitorConsentRecorded",
                "transcriptExportAuthorized", "paymentAuthorized",
                "creatorPayoutAuthorized", "campaignDeliveryAuthorized"
            })
                Assert.False(root.GetProperty(flag).GetBoolean());
            Assert.Equal("NOT_SENT",root.GetProperty("delivery").GetString());
        }
        if (expected==HttpStatusCode.BadRequest)
            Assert.Contains("CSRF",body);
    }

    [Fact]
    public async Task Anonymous_http_readiness_and_both_writes_require_authentication()
    {
        using var client=factory.CreateSecureClient();
        using var readiness=await client.GetAsync(Base+"/readiness");
        Assert.Equal(HttpStatusCode.Unauthorized,readiness.StatusCode);
        using var optIn=await client.PostAsJsonAsync(Base+"/opt-in",Consent(true));
        Assert.Equal(HttpStatusCode.Unauthorized,optIn.StatusCode);
        using var withdraw=await client.PostAsJsonAsync(Base+"/withdraw",Consent(false));
        Assert.Equal(HttpStatusCode.Unauthorized,withdraw.StatusCode);
    }

    [Theory]
    [InlineData("opt-in",true)]
    [InlineData("withdraw",false)]
    public async Task Authenticated_http_write_without_antiforgery_token_fails_before_controller(
        string action,bool selected)
    {
        using var client=TestVisitor();
        using var response=await client.PostAsJsonAsync(Base+"/"+action,Consent(selected));
        await AssertDenied(response,HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("opt-in",true)]
    [InlineData("withdraw",false)]
    public async Task Authenticated_http_write_with_real_testserver_csrf_still_never_records_consent(
        string action,bool selected)
    {
        using var client=TestVisitor();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await CsrfToken(client));
        using var response=await client.PostAsJsonAsync(Base+"/"+action,Consent(selected));
        await AssertDenied(response,HttpStatusCode.Conflict);
        Assert.True(response.Headers.CacheControl?.NoStore==true ||
            response.Headers.TryGetValues("Cache-Control",out var values)
            && values.Any(v=>v.Contains("no-store",StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Authenticated_readiness_is_disabled_and_not_cached()
    {
        using var client=TestVisitor();
        using var response=await client.GetAsync(Base+"/readiness");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root=json.RootElement;
        Assert.Equal("REAL_VISITOR_CONSENT_BACKEND_NOT_CONFIGURED",
            root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("authenticatedVisitorConsentRecorded").GetBoolean());
        Assert.False(root.GetProperty("transcriptExportAuthorized").GetBoolean());
        Assert.False(root.GetProperty("paymentAuthorized").GetBoolean());
        Assert.False(root.GetProperty("creatorPayoutAuthorized").GetBoolean());
        Assert.False(root.GetProperty("campaignDeliveryAuthorized").GetBoolean());
        Assert.Equal("NOT_SENT",root.GetProperty("delivery").GetString());
        Assert.True(response.Headers.CacheControl?.NoStore==true ||
            response.Headers.TryGetValues("Cache-Control",out var values)
            && values.Any(v=>v.Contains("no-store",StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("opt-in", true)]
    [InlineData("withdraw", false)]
    public async Task Tampered_csrf_token_with_real_test_cookie_fails_closed(
        string action, bool selected)
    {
        using var client=TestVisitor();
        var genuine=await CsrfToken(client);
        // Keep the genuine antiforgery cookie but deliberately corrupt the
        // request token. This tests middleware verification, not controller.
        var tampered=(genuine[0]=='A' ? "B" : "A")+genuine[1..];
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",tampered);
        using var response=await client.PostAsJsonAsync(Base+"/"+action,Consent(selected));
        await AssertDenied(response,HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("opt-in", true)]
    [InlineData("withdraw", false)]
    public async Task Csrf_header_borrowed_from_another_client_without_its_cookie_is_denied(
        string action, bool selected)
    {
        using var first=TestVisitor();
        var borrowed=await CsrfToken(first);
        // The receiving HttpClient has a distinct browser-cookie container.
        // A request token alone is not proof of a valid same-browser session.
        using var second=TestVisitor();
        second.DefaultRequestHeaders.Add("X-CSRF-TOKEN",borrowed);
        using var response=await second.PostAsJsonAsync(Base+"/"+action,Consent(selected));
        await AssertDenied(response,HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("opt-in", true)]
    [InlineData("withdraw", false)]
    public async Task Untrusted_bearer_header_cannot_substitute_for_authenticated_cookie(
        string action, bool selected)
    {
        using var client=factory.CreateSecureClient();
        // The isolated fake auth scheme deliberately does not trust Bearer:
        // this is NOT a JWT signature validation test.
        client.DefaultRequestHeaders.Authorization=
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer","not-a-real-token");
        using var response=await client.PostAsJsonAsync(Base+"/"+action,Consent(selected));
        Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
    }

    [Fact]
    public async Task Staff_claims_do_not_turn_consent_into_an_export_or_payment()
    {
        using var client=factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name","Fictional Staff");
        client.DefaultRequestHeaders.Add("X-Test-Roles","bliss.operator,bliss.ask_alpha_visitor");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",await CsrfToken(client));
        using var response=await client.PostAsJsonAsync(Base+"/opt-in",Consent(true));
        await AssertDenied(response,HttpStatusCode.Conflict);
    }
}
