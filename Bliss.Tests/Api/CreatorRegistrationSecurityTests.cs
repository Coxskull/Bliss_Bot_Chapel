using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Tests.Api;

public sealed class CreatorRegistrationSecurityTests
{
    private const string Issuer = "https://alpha-auth.example.invalid";
    private static readonly BlissAuthenticationOptions Options = new()
    {
        Enabled = true, SupabaseEnabled = true, CreatorRegistrationRequested = true,
        Authority = Issuer, ClientId = "synthetic-client"
    };

    [Fact]
    public void Registration_only_identity_requires_verified_issuer_marker_and_no_roles_or_tenant_claims()
    {
        Assert.True(BlissAuthorization.IsPendingCreatorRegistration(Pending("creator-subject"), Options));
        Assert.False(BlissAuthorization.IsPendingCreatorRegistration(
            Pending("creator-subject", new Claim(ClaimTypes.Role, Options.AdminRole)), Options));
        Assert.False(BlissAuthorization.IsPendingCreatorRegistration(
            Pending("creator-subject", new Claim(Options.CreatorIdClaimType, Guid.NewGuid().ToString())), Options));
        Assert.False(BlissAuthorization.IsPendingCreatorRegistration(
            Pending("creator-subject", new Claim(Options.AdvertiserIdClaimType, Guid.NewGuid().ToString())), Options));
        var missingVerifiedEmail = new ClaimsIdentity("test");
        foreach (var claim in Pending("creator-subject").Claims.Where(x => x.Type != "email_verified"))
            missingVerifiedEmail.AddClaim(claim);
        Assert.False(BlissAuthorization.IsPendingCreatorRegistration(
            new ClaimsPrincipal(missingVerifiedEmail), Options));

        var forged = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "creator-subject"), new Claim("email_verified", "true"),
            new Claim(BlissAuthenticationOptions.CreatorRegistrationPendingMarkerClaimType,
                "true", ClaimValueTypes.Boolean, "untrusted-issuer")
        }, "test"));
        Assert.False(BlissAuthorization.IsPendingCreatorRegistration(forged, Options));
        Assert.False(BlissAuthorization.IsPendingCreatorRegistration(Pending("creator-subject"),
            new BlissAuthenticationOptions { Enabled = true, SupabaseEnabled = true, Authority = Issuer }));
    }

    [Fact]
    public void Supabase_oidc_maps_new_verified_identity_to_creator_registration_only()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "new-creator"), new Claim("email_verified", "true"),
            new Claim(Options.RoleClaimType, Options.AdminRole), new Claim("roles", Options.CreatorRole),
            new Claim(Options.AdvertiserIdClaimType, Guid.NewGuid().ToString()),
            new Claim(Options.CreatorIdClaimType, Guid.NewGuid().ToString())
        }, "oidc");
        var access = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "new-creator"), new Claim("client_id", Options.ClientId),
            new Claim("app_metadata", "{}")
        }, "access"));

        SupabaseOidc.ApplyVerifiedAccessToken(identity, access, Options);

        var principal = new ClaimsPrincipal(identity);
        Assert.True(BlissAuthorization.IsPendingCreatorRegistration(principal, Options));
        Assert.Empty(identity.Claims.Where(c => c.Type == Options.RoleClaimType || c.Type == "roles"
            || c.Type == Options.AdvertiserIdClaimType || c.Type == Options.CreatorIdClaimType));
    }

    [Fact]
    public void Shared_registration_identity_is_limited_to_the_two_explicit_registration_lanes()
    {
        var bothOptions = new BlissAuthenticationOptions
        {
            Enabled = true, SupabaseEnabled = true, CreatorRegistrationRequested = true,
            AdvertiserRegistrationRequested = true, Authority = Issuer, ClientId = Options.ClientId
        };
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "new-customer"), new Claim("email_verified", "true") }, "oidc");
        var access = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "new-customer"), new Claim("client_id", bothOptions.ClientId),
            new Claim("app_metadata", "{}")
        }, "access"));
        SupabaseOidc.ApplyVerifiedAccessToken(identity, access, bothOptions);
        var principal = new ClaimsPrincipal(identity);
        Assert.True(BlissAuthorization.IsPendingCreatorRegistration(principal, bothOptions));
        Assert.True(BlissAuthorization.IsPendingAdvertiserRegistration(principal, bothOptions));

        var context = new DefaultHttpContext { User = principal };
        context.Request.Method = "POST";
        context.Request.Path = "/api/creators/register";
        Assert.False(CreatorRegistrationBoundary.ShouldDeny(context, bothOptions));
        Assert.False(AdvertiserRegistrationBoundary.ShouldDeny(context, bothOptions));
        context.Request.Path = "/api/advertisers/register";
        Assert.False(CreatorRegistrationBoundary.ShouldDeny(context, bothOptions));
        Assert.False(AdvertiserRegistrationBoundary.ShouldDeny(context, bothOptions));
        context.Request.Path = "/api/campaigns";
        Assert.True(CreatorRegistrationBoundary.ShouldDeny(context, bothOptions));
        Assert.True(AdvertiserRegistrationBoundary.ShouldDeny(context, bothOptions));
    }

    [Fact]
    public void Pending_creator_boundary_allows_only_creator_registration_status_auth_and_logout()
    {
        Assert.False(Denied("GET", "/api/auth/session"));
        Assert.False(Denied("GET", "/api/auth/login"));
        Assert.False(Denied("GET", "/api/auth/supabase-config"));
        Assert.False(Denied("POST", "/api/auth/logout"));
        Assert.False(Denied("POST", "/api/creators/register"));
        Assert.False(Denied("GET", "/api/creators/registration"));
        Assert.True(Denied("GET", "/api/creators/me"));
        Assert.True(Denied("GET", "/api/creators"));
        Assert.True(Denied("POST", "/api/creators/register/other"));
    }

    [Fact]
    public void Creator_registration_storage_has_unique_identity_receipt_and_tenant_constraints()
    {
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseNpgsql("Host=localhost;Database=bliss_model;Username=bliss;Password=synthetic").Options;
        using var db = new BlissDbContext(options);
        var memberships = db.Model.FindEntityType(typeof(CreatorAccountMembership))!;
        Assert.Contains(memberships.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(CreatorAccountMembership.Subject));
        Assert.Contains(memberships.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(CreatorAccountMembership.CreatorId));
        var receipts = db.Model.FindEntityType(typeof(CreatorRegistrationReceipt))!;
        Assert.Contains(receipts.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(CreatorRegistrationReceipt.RequestKey));
        Assert.Contains(receipts.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { nameof(CreatorRegistrationReceipt.Subject), nameof(CreatorRegistrationReceipt.CreatorId) }));
    }

    [Fact]
    public async Task PostgreSql_registration_is_atomic_pending_scoped_idempotent_and_receipt_is_immutable()
    {
        var source = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(source)) return;
        var schema = "task4_" + Guid.NewGuid().ToString("N");
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
            var request = new CreatorRegistrationRequest(Guid.NewGuid(), "Synthetic Creator", "PH",
                "https://example.invalid/@synthetic", "en", true, true, false,
                CreatorRegistrationController.PrivacyNoticeVersion, CreatorRegistrationController.TermsVersion);

            var created = await CallRegister(db, "creator-subject", request);
            var createdResult = Assert.IsType<ObjectResult>(created);
            Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
            var receipt = Assert.IsType<CreatorRegistrationReceiptDto>(createdResult.Value);
            Assert.Equal("PENDING_REVIEW", receipt.Status);
            Assert.False(receipt.MarketingOptIn);
            Assert.Equal(1, await db.Creators.CountAsync());
            Assert.Equal("UNVERIFIED", (await db.Creators.SingleAsync()).AuthorityStatus);
            Assert.Equal(1, await db.CreatorAccountMemberships.CountAsync());
            var saved = await db.CreatorRegistrationReceipts.SingleAsync();
            Assert.True(saved.ProcessingConsent);
            Assert.True(saved.TermsAccepted);
            Assert.False(saved.MarketingOptIn);
            Assert.Equal("creator-subject", saved.Subject);
            Assert.Equal(64, saved.RequestSha256.Length);
            Assert.Equal("https://example.invalid/@synthetic", (await db.CreatorAccountMemberships.SingleAsync()).SubmittedChannelUrl);
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"CreatorRegistrationReceipts\" SET \"MarketingOptIn\" = TRUE WHERE \"Id\" = {0}", saved.Id));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"CreatorRegistrationReceipts\" WHERE \"Id\" = {0}", saved.Id));

            var retry = Assert.IsType<OkObjectResult>(await CallRegister(db, "creator-subject", request));
            Assert.Equal(receipt.ReceiptId, Assert.IsType<CreatorRegistrationReceiptDto>(retry.Value).ReceiptId);
            Assert.IsType<ConflictObjectResult>(await CallRegister(db, "creator-subject",
                request with { ChannelUrl = "https://other.example.invalid/creator" }));
            Assert.IsType<ConflictObjectResult>(await CallRegister(db, "creator-subject", request with { RequestKey = Guid.NewGuid() }));
            Assert.IsType<ConflictObjectResult>(await CallRegister(db, "other-subject", request));
            Assert.IsType<BadRequestObjectResult>(await CallRegister(db, "third-subject",
                request with { RequestKey = Guid.NewGuid(), TermsAccepted = false }));
            Assert.Equal(StatusCodes.Status403Forbidden,
                Assert.IsType<StatusCodeResult>(await CallRegister(db, "unauthorized", request with { RequestKey = Guid.NewGuid() }, pending: false)).StatusCode);
            Assert.Equal(1, await db.Creators.CountAsync());
            Assert.Equal(1, await db.CreatorRegistrationReceipts.CountAsync());

            Assert.IsType<OkObjectResult>(await CallStatus(db, "creator-subject"));
            Assert.IsType<NotFoundResult>(await CallStatus(db, "other-subject"));
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
        var context = new DefaultHttpContext { User = Pending("creator-subject") };
        context.Request.Method = method;
        context.Request.Path = path;
        return CreatorRegistrationBoundary.ShouldDeny(context, Options);
    }

    private static async Task<IActionResult> CallRegister(BlissDbContext db, string subject,
        CreatorRegistrationRequest request, bool pending = true)
    {
        var controller = new CreatorRegistrationController(db, Options);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = pending ? Pending(subject) : new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", subject) }, "test")) }
        };
        return await controller.Register(request, CancellationToken.None);
    }

    private static async Task<IActionResult> CallStatus(BlissDbContext db, string subject)
    {
        var controller = new CreatorRegistrationController(db, Options);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Pending(subject) } };
        return await controller.GetMyRegistration(CancellationToken.None);
    }

    private static ClaimsPrincipal Pending(string subject, params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new("sub", subject), new("email_verified", "true"),
            new(BlissAuthenticationOptions.CreatorRegistrationPendingMarkerClaimType,
                "true", ClaimValueTypes.Boolean, Issuer)
        };
        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
