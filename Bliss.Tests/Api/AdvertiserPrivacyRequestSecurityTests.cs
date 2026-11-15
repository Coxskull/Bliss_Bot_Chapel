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

public sealed class AdvertiserPrivacyRequestSecurityTests
{
    private const string Issuer = "https://alpha-auth.example.invalid";
    private static readonly BlissAuthenticationOptions Options = new()
    {
        Enabled = true, SupabaseEnabled = true, AdvertiserRegistrationRequested = true,
        AdvertiserPrivacyRequestsRequested = true,
        Authority = Issuer, ClientId = "synthetic-client"
    };

    private static ClaimsPrincipal Pending(string subject) => new(new ClaimsIdentity(new[]
    {
        new Claim("sub", subject), new Claim("email_verified", "true"),
        new Claim(BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
            "true", ClaimValueTypes.Boolean, Issuer)
    }, "test"));

    private static AdvertiserPrivacyRequestsController Controller(BlissDbContext db, string subject, bool authorized = true)
    {
        var controller = new AdvertiserPrivacyRequestsController(db, Options);
        var identity = authorized ? Pending(subject) :
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", subject) }, "test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = identity }
        };
        return controller;
    }

    private static AdvertiserPrivacyRequestsController OperatorController(
        BlissDbContext db, bool allowlisted = true, string issuer = Issuer, string role = "bliss.operator")
    {
        var options = new BlissAuthenticationOptions
        {
            Enabled = true, SupabaseEnabled = true, AdvertiserRegistrationRequested = true,
            AdvertiserPrivacyRequestsRequested = true,
            Authority = Issuer, ClientId = "synthetic-client",
            ApprovedPrivacyOperatorSubjects = allowlisted ? new[] { "operator-sub" } : []
        };
        var roleClaim = new Claim(options.RoleClaimType, role, ClaimValueTypes.String, issuer, issuer);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "operator-sub"), roleClaim
        }, "oidc"));
        var controller = new AdvertiserPrivacyRequestsController(db, options);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static AdvertiserRegistrationController Registration(BlissDbContext db, string subject)
    {
        var controller = new AdvertiserRegistrationController(db, Options);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = Pending(subject) }
        };
        return controller;
    }

    [Fact]
    public void Pending_applicants_can_only_use_exact_account_case_routes_not_other_tenants_or_admin_queues()
    {
        bool denied(string method, string path)
        {
            var context = new DefaultHttpContext { User = Pending("subject-a") };
            context.Request.Method = method;
            context.Request.Path = path;
            return AdvertiserRegistrationBoundary.ShouldDeny(context, Options);
        }

        Assert.False(denied("POST", "/api/advertisers/privacy-requests"));
        Assert.False(denied("GET", "/api/advertisers/privacy-requests"));
        Assert.True(denied("PUT", "/api/advertisers/privacy-requests"));
        Assert.True(denied("DELETE", "/api/advertisers/privacy-requests"));
        Assert.True(denied("GET", "/api/advertisers/privacy-requests/queue"));
        Assert.True(denied("GET", "/api/advertisers/privacy-requests/another-subject"));
        Assert.True(denied("POST", "/api/advertisers/privacy-requests/another-subject"));
        Assert.True(denied("GET", "/api/advertisers/me"));
    }

    [Fact]
    public async Task Privacy_case_intake_is_rejected_when_independent_feature_flag_is_off()
    {
        var modelOptions = new DbContextOptionsBuilder<BlissDbContext>()
            .UseNpgsql("Host=localhost;Database=synthetic_missing;Username=synthetic;Password=synthetic").Options;
        using var db = new BlissDbContext(modelOptions);
        var disabled = new BlissAuthenticationOptions
        {
            Enabled = true, SupabaseEnabled = true, AdvertiserRegistrationRequested = true,
            AdvertiserPrivacyRequestsRequested = false, Authority = Issuer, ClientId = "synthetic-client"
        };
        var controller = new AdvertiserPrivacyRequestsController(db, disabled)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Pending("subject-a") }
            }
        };
        var received = await controller.Create(
            new AdvertiserPrivacyCaseRequest(Guid.NewGuid(), "ACCESS"), CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(received).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden,
            Assert.IsType<StatusCodeResult>(await controller.Mine(CancellationToken.None)).StatusCode);
    }

    [Fact]
    public void Privacy_case_model_has_request_key_uniqueness_and_no_free_text_personal_data()
    {
        var modelOptions = new DbContextOptionsBuilder<BlissDbContext>()
            .UseNpgsql("Host=localhost;Database=bliss_model;Username=bliss;Password=synthetic").Options;
        using var db = new BlissDbContext(modelOptions);
        var entity = db.Model.FindEntityType(typeof(AdvertiserPrivacyRequest))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == "RequestKey");
        Assert.DoesNotContain(entity.GetProperties(), p => p.Name is "Email" or "Description" or "Password" or "IpAddress");
    }

    [Fact]
    public async Task Pg_case_intake_is_durable_idempotent_tenant_scoped_and_never_applies_automatic_erasure()
    {
        var source = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(source)) return;
        // A separate disposable database is necessary: other CI fixtures create a
        // shared "financials" schema, so a per-test search_path alone is not isolated.
        // Never allow this synthetic test to create a database on a remote host.
        var adminBuilder = new NpgsqlConnectionStringBuilder(source);
        if (!string.Equals(adminBuilder.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && adminBuilder.Host != "127.0.0.1" && adminBuilder.Host != "::1")
            return;
        var databaseName = "task3b_ci_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(adminBuilder.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var tenantBuilder = new NpgsqlConnectionStringBuilder(source) { Database = databaseName };
            var dbOptions = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(tenantBuilder.ConnectionString).Options;
            await using var db = new BlissDbContext(dbOptions);
            await db.Database.MigrateAsync();

            async Task register(string subject)
            {
                var request = new AdvertiserRegistrationRequest(Guid.NewGuid(), "Synthetic " + subject,
                    "PH", "https://example.invalid", true, true, false,
                    AdvertiserRegistrationController.PrivacyNoticeVersion,
                    AdvertiserRegistrationController.TermsVersion);
                var result = await Registration(db, subject).Register(request, CancellationToken.None);
                Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(result).StatusCode);
            }

            await register("subject-a");
            await register("subject-b");

            var key = Guid.NewGuid();
            var caseRequest = new AdvertiserPrivacyCaseRequest(key, "MARKETING_WITHDRAWAL");
            var created = Assert.IsType<ObjectResult>(
                await Controller(db, "subject-a").Create(caseRequest, CancellationToken.None));
            Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
            var receipt = Assert.IsType<AdvertiserPrivacyCaseReceipt>(created.Value);
            Assert.Equal("RECEIVED", receipt.Status);
            Assert.Equal("MARKETING_WITHDRAWAL", receipt.RequestType);

            var replay = Assert.IsType<OkObjectResult>(
                await Controller(db, "subject-a").Create(caseRequest, CancellationToken.None));
            Assert.Equal(receipt.CaseId, Assert.IsType<AdvertiserPrivacyCaseReceipt>(replay.Value).CaseId);

            Assert.IsType<ConflictObjectResult>(await Controller(db, "subject-a")
                .Create(caseRequest with { RequestType = "ERASURE" }, CancellationToken.None));
            Assert.IsType<ConflictObjectResult>(await Controller(db, "subject-b")
                .Create(caseRequest, CancellationToken.None));

            var invalid = await Controller(db, "subject-a")
                .Create(new AdvertiserPrivacyCaseRequest(Guid.NewGuid(), "RAW_EXPORT_ALL_TENANTS"), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(invalid);

            var invalidKey = await Controller(db, "subject-a")
                .Create(new AdvertiserPrivacyCaseRequest(Guid.Empty, "ACCESS"), CancellationToken.None);
            Assert.IsType<BadRequestObjectResult>(invalidKey);

            var unauthorized = await Controller(db, "subject-a", authorized: false)
                .Create(new AdvertiserPrivacyCaseRequest(Guid.NewGuid(), "ACCESS"), CancellationToken.None);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(unauthorized).StatusCode);

            var second = await Controller(db, "subject-b")
                .Create(new AdvertiserPrivacyCaseRequest(Guid.NewGuid(), "ERASURE"), CancellationToken.None);
            Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ObjectResult>(second).StatusCode);
            var mineA = Assert.IsType<OkObjectResult>(await Controller(db, "subject-a").Mine(CancellationToken.None));
            var a = Assert.IsType<AdvertiserPrivacyCaseReceipt[]>(mineA.Value);
            Assert.Single(a);
            Assert.Equal(receipt.CaseId, a[0].CaseId);

            var deniedPendingQueue = await Controller(db, "subject-a").OperatorQueue(CancellationToken.None);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(deniedPendingQueue).StatusCode);
            var notApproved = await OperatorController(db, allowlisted: false).OperatorQueue(CancellationToken.None);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(notApproved).StatusCode);
            var forgedRole = await OperatorController(db, issuer: "untrusted.example.invalid").OperatorQueue(CancellationToken.None);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(forgedRole).StatusCode);
            var nonStaffRole = await OperatorController(db, role: "bliss.viewer").OperatorQueue(CancellationToken.None);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(nonStaffRole).StatusCode);

            var queue = Assert.IsType<OkObjectResult>(
                await OperatorController(db).OperatorQueue(CancellationToken.None));
            var queued = Assert.IsType<AdvertiserPrivacyOperatorCase[]>(queue.Value);
            Assert.Equal(2, queued.Length);
            var deniedAck = await Controller(db, "subject-a").Acknowledge(receipt.CaseId, CancellationToken.None);
            Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<StatusCodeResult>(deniedAck).StatusCode);
            var ack = Assert.IsType<OkObjectResult>(
                await OperatorController(db).Acknowledge(receipt.CaseId, CancellationToken.None));
            Assert.NotNull(ack.Value);
            Assert.IsType<ConflictObjectResult>(
                await OperatorController(db).Acknowledge(receipt.CaseId, CancellationToken.None));
            var acknowledged = await db.AdvertiserPrivacyRequests.AsNoTracking()
                .SingleAsync(x => x.Id == receipt.CaseId);
            Assert.Equal("VERIFYING", acknowledged.Status);
            Assert.Equal("operator-sub", acknowledged.AcknowledgedBySubject);
            Assert.NotNull(acknowledged.AcknowledgedAt);

            var records = await db.AdvertiserPrivacyRequests.AsNoTracking().ToArrayAsync();
            Assert.Equal(2, records.Length);
            Assert.Single(records, x => x.Status == "VERIFYING");
            Assert.Single(records, x => x.Status == "RECEIVED");
            Assert.Equal(2, await db.AdvertiserAccountMemberships.CountAsync());
            Assert.Equal(2, await db.AdvertiserRegistrationReceipts.CountAsync());
            // A request for withdrawal or erasure never mutates the immutable consent receipt.
            Assert.All(await db.AdvertiserRegistrationReceipts.ToListAsync(), x => Assert.False(x.MarketingOptIn));
        }
        finally
        {
            await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
            await admin.OpenAsync();
            // Force-closes idle connections left in the local Npgsql pool.
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
