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
        var schema = "task3b_" + Guid.NewGuid().ToString("N");
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

            var records = await db.AdvertiserPrivacyRequests.AsNoTracking().ToArrayAsync();
            Assert.Equal(2, records.Length);
            Assert.All(records, x => Assert.Equal("RECEIVED", x.Status));
            Assert.Equal(2, await db.AdvertiserAccountMemberships.CountAsync());
            Assert.Equal(2, await db.AdvertiserRegistrationReceipts.CountAsync());
            // A request for withdrawal or erasure never mutates the immutable consent receipt.
            Assert.All(await db.AdvertiserRegistrationReceipts.ToListAsync(), x => Assert.False(x.MarketingOptIn));
        }
        finally
        {
            await using var admin = new NpgsqlConnection(adminBuilder.ConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
