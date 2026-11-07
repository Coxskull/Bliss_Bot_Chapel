using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bliss.Tests.Api;

public sealed class AdvertiserRegistrationSecurityTests
{
    private static readonly string Issuer = "https://alpha-auth.example.invalid";
    private static readonly BlissAuthenticationOptions Options = new()
    {
        Enabled = true,
        SupabaseEnabled = true,
        AdvertiserRegistrationRequested = true,
        Authority = Issuer,
        ClientId = "synthetic-client"
    };

    private static ClaimsPrincipal Pending(params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new("sub", "auth-subject-123"),
            new("email_verified", "true"),
            new(BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
                "true", ClaimValueTypes.Boolean, Issuer)
        };
        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Pending_registration_requires_verified_issuer_marker_and_no_roles_or_tenant_claims()
    {
        Assert.True(BlissAuthorization.IsPendingAdvertiserRegistration(Pending(), Options));
        Assert.False(BlissAuthorization.IsPendingAdvertiserRegistration(
            Pending(new Claim(ClaimTypes.Role, Options.AdminRole)), Options));
        Assert.False(BlissAuthorization.IsPendingAdvertiserRegistration(
            Pending(new Claim(Options.AdvertiserIdClaimType, Guid.NewGuid().ToString())), Options));

        var unverified = Pending().Claims.Where(x => x.Type != "email_verified").ToArray();
        Assert.False(BlissAuthorization.IsPendingAdvertiserRegistration(
            new ClaimsPrincipal(new ClaimsIdentity(unverified, "test")), Options));

        var forged = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "auth-subject-123"), new Claim("email_verified", "true"),
            new Claim(BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
                "true", ClaimValueTypes.Boolean, "untrusted-issuer")
        }, "test"));
        Assert.False(BlissAuthorization.IsPendingAdvertiserRegistration(forged, Options));

        var disabled = new BlissAuthenticationOptions
        {
            Enabled = true, SupabaseEnabled = true, Authority = Issuer,
            AdvertiserRegistrationRequested = false
        };
        Assert.False(BlissAuthorization.IsPendingAdvertiserRegistration(Pending(), disabled));
    }

    [Fact]
    public void Supabase_access_token_maps_new_verified_user_to_registration_only_identity()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "new-user-123"), new Claim("email_verified", "true"),
            new Claim(Options.RoleClaimType, Options.AdminRole), new Claim("role", Options.AdminRole),
            new Claim(Options.AdvertiserIdClaimType, Guid.NewGuid().ToString())
        }, "oidc");
        var access = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "new-user-123"), new Claim("client_id", Options.ClientId), new Claim("app_metadata", "{}")
        }, "access"));

        SupabaseOidc.ApplyVerifiedAccessToken(identity, access, Options);

        var principal = new ClaimsPrincipal(identity);
        Assert.True(BlissAuthorization.IsPendingAdvertiserRegistration(principal, Options));
        Assert.Empty(identity.Claims.Where(c => c.Type == Options.RoleClaimType || c.Type == "role" || c.Type == Options.AdvertiserIdClaimType));
    }

    [Fact]
    public void Pending_registration_boundary_allows_only_session_login_config_logout_and_register()
    {
        Assert.False(Denied("GET", "/api/auth/session"));
        Assert.False(Denied("GET", "/api/auth/login"));
        Assert.False(Denied("GET", "/api/auth/supabase-config"));
        Assert.False(Denied("POST", "/api/auth/logout"));
        Assert.False(Denied("POST", "/api/advertisers/register"));
        Assert.True(Denied("GET", "/api/advertisers/me"));
        Assert.True(Denied("GET", "/api/creators"));
        Assert.True(Denied("POST", "/api/advertisers/register/other"));
    }

    [Fact]
    public void Registration_storage_has_unique_identity_request_and_tenant_ownership_constraints()
    {
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseNpgsql("Host=localhost;Database=bliss_model;Username=bliss;Password=synthetic")
            .Options;
        using var db = new BlissDbContext(options);
        var memberships = db.Model.FindEntityType(typeof(AdvertiserAccountMembership))!;
        var membershipIndexes = memberships.GetIndexes();
        Assert.Contains(membershipIndexes, x => x.IsUnique && x.Properties.Single().Name == nameof(AdvertiserAccountMembership.Subject));
        Assert.Contains(membershipIndexes, x => x.IsUnique && x.Properties.Single().Name == nameof(AdvertiserAccountMembership.AdvertiserId));

        var receipts = db.Model.FindEntityType(typeof(AdvertiserRegistrationReceipt))!;
        Assert.Contains(receipts.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(AdvertiserRegistrationReceipt.RequestKey));
        Assert.Contains(receipts.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { nameof(AdvertiserRegistrationReceipt.Subject), nameof(AdvertiserRegistrationReceipt.AdvertiserId) }));
    }

    [Fact]
    public async Task PostgreSql_registration_persists_consent_replays_exactly_and_rejects_duplicates_and_tenant_replay()
    {
        var source = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(source)) return;
        var schema = "task3_" + Guid.NewGuid().ToString("N");
        var adminBuilder = new NpgsqlConnectionStringBuilder(source);
        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var tenantBuilder = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema };
            var dbOptions = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(tenantBuilder.ConnectionString).Options;
            await using var db = new BlissDbContext(dbOptions);
            await db.Database.MigrateAsync();

            var request = new AdvertiserRegistrationRequest(Guid.NewGuid(), "Synthetic Northwind Studio", "PH",
                "https://example.invalid", true, true, false,
                AdvertiserRegistrationController.PrivacyNoticeVersion, AdvertiserRegistrationController.TermsVersion);

            var created = await CallRegister(db, "auth-subject-123", request);
            var createdResult = Assert.IsType<ObjectResult>(created);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
            var receipt = Assert.IsType<AdvertiserRegistrationReceiptDto>(createdResult.Value);
            Assert.Equal("PENDING_REVIEW", receipt.Status);
            Assert.False(receipt.MarketingOptIn);
            Assert.Equal(1, await db.Advertisers.CountAsync());
            Assert.Equal(1, await db.AdvertiserAccountMemberships.CountAsync());
            var saved = await db.AdvertiserRegistrationReceipts.SingleAsync();
            Assert.True(saved.ProcessingConsent);
            Assert.True(saved.TermsAccepted);
            Assert.False(saved.MarketingOptIn);
            Assert.Equal("auth-subject-123", saved.Subject);
            Assert.Equal(64, saved.RequestSha256.Length);
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"AdvertiserRegistrationReceipts\" SET \"MarketingOptIn\" = TRUE WHERE \"Id\" = {0}", saved.Id));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"AdvertiserRegistrationReceipts\" WHERE \"Id\" = {0}", saved.Id));

            var retry = Assert.IsType<OkObjectResult>(await CallRegister(db, "auth-subject-123", request));
            Assert.Equal(receipt.ReceiptId, Assert.IsType<AdvertiserRegistrationReceiptDto>(retry.Value).ReceiptId);

            var mismatch = await CallRegister(db, "auth-subject-123", request with { Website = "https://different.example.invalid" });
            Assert.IsType<ConflictObjectResult>(mismatch);
            var duplicate = await CallRegister(db, "auth-subject-123", request with { RequestKey = Guid.NewGuid() });
            Assert.IsType<ConflictObjectResult>(duplicate);
            var crossTenantReplay = await CallRegister(db, "another-auth-subject", request);
            Assert.IsType<ConflictObjectResult>(crossTenantReplay);
            Assert.Equal(1, await db.Advertisers.CountAsync());
            Assert.Equal(1, await db.AdvertiserRegistrationReceipts.CountAsync());

            var missingConsent = await CallRegister(db, "third-subject", request with { RequestKey = Guid.NewGuid(), TermsAccepted = false });
            Assert.IsType<BadRequestObjectResult>(missingConsent);
            var unauthorized = await CallRegister(db, "no-marker", request with { RequestKey = Guid.NewGuid() }, pending: false);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(unauthorized).StatusCode);
        }
        finally
        {
            await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static bool Denied(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.User = Pending();
        context.Request.Method = method;
        context.Request.Path = path;
        return AdvertiserRegistrationBoundary.ShouldDeny(context, Options);
    }

    private static async Task<IActionResult> CallRegister(BlissDbContext db, string subject,
        AdvertiserRegistrationRequest request, bool pending = true)
    {
        var controller = new AdvertiserRegistrationController(db, Options);
        var context = new DefaultHttpContext();
        context.User = pending ? Pending(subject) : new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", subject) }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return await controller.Register(request, CancellationToken.None);
    }

    private static ClaimsPrincipal Pending(string subject) => new(new ClaimsIdentity(new[]
    {
        new Claim("sub", subject), new Claim("email_verified", "true"),
        new Claim(BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
            "true", ClaimValueTypes.Boolean, Issuer)
    }, "test"));
}
