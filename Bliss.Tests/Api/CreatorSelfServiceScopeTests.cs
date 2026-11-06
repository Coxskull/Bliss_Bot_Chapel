using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Api;

public sealed class CreatorSelfServiceScopeTests
{
    private static readonly Guid ApprovedCreator = Guid.Parse("86fadfbe-e167-4ca6-97e4-f8a96b0ca98d");
    private static readonly Guid OtherCreator = Guid.Parse("a1a059c1-b1d7-43a9-baea-d5a89a1610aa");
    private const string Subject = "d973dc4f-3b6c-4fd0-b881-c8e70f934844";
    private const string Issuer = "https://synthetic-issuer.supabase.co/auth/v1";

    private static BlissAuthenticationOptions Auth(bool enabled = true) => new()
    {
        Enabled = true, SupabaseEnabled = true, CreatorSelfServiceRequested = enabled,
        ApprovedCreatorSubjects = [Subject], Authority = Issuer
    };

    private static ClaimsPrincipal Principal(Guid creatorId)
    {
        var claims = new[] {
            new Claim("sub", Subject),
            new Claim("roles", "bliss.creator", ClaimValueTypes.String, Issuer),
            new Claim("bliss_creator_id", creatorId.ToString("D"),
                ClaimValueTypes.String, Issuer)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "verified-issuer", "name", "roles"));
    }

    private static BlissDbContext Database()
    {
        var opts = new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase("creator-own-profile-" + Guid.NewGuid())
            .Options;
        var db = new BlissDbContext(opts);
        db.Creators.AddRange(
            new Creator { Id = ApprovedCreator, Name = "Fixture Approved Creator",
                AuthorityStatus = "VERIFIED" },
            new Creator { Id = OtherCreator, Name = "Fixture Different Creator",
                AuthorityStatus = "VERIFIED" });
        db.SaveChanges();
        return db;
    }

    private static CreatorsController Controller(
        BlissDbContext db, BlissAuthenticationOptions options, ClaimsPrincipal user)
        => new(db, options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

    [Fact]
    public async Task Signed_verified_creator_can_read_only_own_minimal_profile()
    {
        await using var db = Database();
        var controller = Controller(db, Auth(), Principal(ApprovedCreator));
        var response = await controller.GetMyVerifiedCreator(default);
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var profile = Assert.IsType<CreatorSelfSummaryDto>(result.Value);
        Assert.Equal(ApprovedCreator, profile.CreatorId);
        Assert.Equal("VERIFIED", profile.AuthorityStatus);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        // Only five intentionally public fields; no CRM/receipts/contacts.
        Assert.Equal(5, typeof(CreatorSelfSummaryDto).GetProperties().Length);
    }

    [Fact]
    public async Task Verified_but_different_tenant_is_not_disclosed()
    {
        await using var db = Database();
        var controller = Controller(db, Auth(), Principal(Guid.NewGuid()));
        var response = await controller.GetMyVerifiedCreator(default);
        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task Unverified_creator_is_denied_even_with_valid_issuer_claims()
    {
        await using var db = Database();
        var existing = await db.Creators.FindAsync(ApprovedCreator);
        Assert.NotNull(existing);
        existing.AuthorityStatus = "UNVERIFIED";
        await db.SaveChangesAsync();
        var response = await Controller(db, Auth(), Principal(ApprovedCreator))
            .GetMyVerifiedCreator(default);
        Assert.IsType<NotFoundResult>(response.Result);
    }

    [Fact]
    public async Task Feature_off_and_unauthenticated_requests_do_not_read_creator()
    {
        await using var db = Database();
        var featureOff = await Controller(db, Auth(false), Principal(ApprovedCreator))
            .GetMyVerifiedCreator(default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(featureOff.Result).StatusCode);
        var unauth = await Controller(db, Auth(), new ClaimsPrincipal(new ClaimsIdentity()))
            .GetMyVerifiedCreator(default);
        Assert.Equal(403, Assert.IsType<ObjectResult>(unauth.Result).StatusCode);
    }
}
