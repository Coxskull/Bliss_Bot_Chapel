using System.Security.Claims;
using System.Security.Cryptography;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bliss.Tests.Api;

// AGM-COMM-015 / IDP-01: cryptographic negative cases with local ephemeral
// signing keys. NO hosted IdP, user, real credentials, consent or authentication
// middleware handoff is exercised by these token verifier tests.
public sealed class AskAlphaVisitorSignedOidcFixtureTests
{
    private const string Issuer="https://idp.example.invalid/realms/agm";
    private const string Client="agm-staging-visitor";

    private static TokenValidationParameters Parameters(SecurityKey trusted) => new()
    {
        ValidateIssuer=true,
        ValidIssuer=Issuer,
        ValidateAudience=true,
        ValidAudience=Client,
        ValidateLifetime=true,
        RequireExpirationTime=true,
        RequireSignedTokens=true,
        ValidateIssuerSigningKey=true,
        IssuerSigningKey=trusted,
        ValidAlgorithms=[SecurityAlgorithms.RsaSha256],
        ClockSkew=TimeSpan.Zero
    };

    private static string Token(SecurityKey? signingKey, string issuer=Issuer,
        string audience=Client, bool expired=false,
        string signingAlgorithm=SecurityAlgorithms.RsaSha256)
    {
        var now=DateTime.UtcNow;
        var descriptor=new SecurityTokenDescriptor
        {
            Issuer=issuer,
            Audience=audience,
            Subject=new ClaimsIdentity(new[]
            {
                new Claim("sub","fixture-visitor-subject-001"),
                new Claim("roles","bliss.ask_alpha_visitor")
            }),
            IssuedAt=expired ? now.AddMinutes(-10) : now,
            NotBefore=expired ? now.AddMinutes(-10) : now.AddSeconds(-1),
            Expires=expired ? now.AddMinutes(-1) : now.AddMinutes(5),
            SigningCredentials=signingKey is null ? null :
                new SigningCredentials(signingKey,signingAlgorithm)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    [Fact]
    public async Task Correctly_signed_fixture_can_validate_but_never_establishes_actual_visitor_consent()
    {
        using var rsa=RSA.Create(2048);
        var key=new RsaSecurityKey(rsa) { KeyId="ephemeral-staging-fixture" };
        var validator=new JsonWebTokenHandler();
        var validated=await validator.ValidateTokenAsync(Token(key),Parameters(key));

        Assert.True(validated.IsValid,validated.Exception?.GetType().Name);
        Assert.NotNull(validated.ClaimsIdentity);
        var principal=new ClaimsPrincipal(validated.ClaimsIdentity);
        var config=new BlissAuthenticationOptions
        {
            Enabled=true,Authority=Issuer,ClientId=Client,
            AskAlphaVisitorIssuer=Issuer,
            AskAlphaVisitorEnrollmentRequested=true
        };
        // Crypto fixture success MUST NOT turn the application's
        // preflight into real identity/session/consent/export authority.
        var readiness=AskAlphaVisitorEnrollmentGate.Inspect(principal,config);
        Assert.False(readiness.RealVisitorIdentityAccepted);
        Assert.False(readiness.NewVisitorSessionCreated);
        Assert.False(readiness.VisitorConsentRecorded);
        Assert.False(readiness.TranscriptExportAuthorized);
        Assert.False(readiness.PaymentAuthorized);
        Assert.False(readiness.CreatorPayoutAuthorized);
        Assert.False(readiness.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",readiness.Delivery);
    }

    [Fact]
    public async Task Issuer_audience_expiry_and_signing_key_substitution_all_fail()
    {
        using var trustedRsa=RSA.Create(2048);
        using var attackerRsa=RSA.Create(2048);
        var trusted=new RsaSecurityKey(trustedRsa) { KeyId="trusted-test-only" };
        var attacker=new RsaSecurityKey(attackerRsa) { KeyId="attacker-test-only" };
        var validator=new JsonWebTokenHandler();
        var parameters=Parameters(trusted);

        foreach(var invalid in new[]
        {
            Token(trusted,issuer:"https://other.example.invalid/realms/agm"),
            Token(trusted,audience:"unapproved-staff-client"),
            Token(trusted,expired:true),
            Token(attacker)
        })
        {
            var result=await validator.ValidateTokenAsync(invalid,parameters);
            Assert.False(result.IsValid);
        }
    }

    [Fact]
    public async Task Unsigned_or_wrong_algorithm_token_cannot_be_upgraded_to_a_visitor()
    {
        using var rsa=RSA.Create(2048);
        var trusted=new RsaSecurityKey(rsa) { KeyId="trusted-rsa" };
        var validator=new JsonWebTokenHandler();

        // A no-signature token must fail even if issuer and audience match.
        var unsigned=Token(null);
        Assert.False((await validator.ValidateTokenAsync(
            unsigned,Parameters(trusted))).IsValid);

        // A token signed using an untrusted symmetric key must NOT be
        // treated as an RS256 OIDC identity token.
        var symmetric=new SymmetricSecurityKey(
            RandomNumberGenerator.GetBytes(32)) { KeyId="untrusted-hs256" };
        var wrongAlgorithm=Token(symmetric,
            signingAlgorithm:SecurityAlgorithms.HmacSha256);
        Assert.False((await validator.ValidateTokenAsync(
            wrongAlgorithm,Parameters(trusted))).IsValid);
    }

    [Fact]
    public void Hosted_openidconnect_configuration_declares_code_pkce_without_token_persistence()
    {
        using var factory=new OidcSecurityApiFactory();
        using var client=factory.CreateSecureClient();
        var options=factory.Services
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);

        Assert.Equal(OpenIdConnectResponseType.Code,options.ResponseType);
        Assert.True(options.UsePkce);
        Assert.False(options.SaveTokens);
        Assert.False(options.MapInboundClaims);
        // Configuration assertions alone are NOT IdP callback/signed-token
        // integration, browser cookie security or hosted staging acceptance.
    }
}
