using System.Security.Claims;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Api;

public sealed class AskAlphaAuthoritativeEnrollmentPlannerTests
{
    private const string Issuer = "https://visitor-idp.fixture.invalid/realm";
    private static BlissAuthenticationOptions ApprovedFixtureOptions(bool on=true) => new()
    {
        Enabled=true,Authority=Issuer,ClientId="fictional-visitor-client",
        AskAlphaVisitorIssuer=Issuer,AskAlphaVisitorEnrollmentRequested=on
    };

    private static ClaimsPrincipal Principal(string role="bliss.ask_alpha_visitor")
    {
        var identity=new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role,role,ClaimValueTypes.String,Issuer),
            new Claim("sub","fixture-visitor-subject-12345",ClaimValueTypes.String,Issuer)
        },"synthetic-authenticated-cookie");
        return new ClaimsPrincipal(identity);
    }

    private static BlissDbContext Db()
    {
        var options=new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new BlissDbContext(options);
    }

    private static async Task<WeddingPlannerWorkspace> SeedWorkspace(
        BlissDbContext db, string status="ACTIVE")
    {
        var when=DateTime.UtcNow;
        var workspace=new WeddingPlannerWorkspace {
            Id=Guid.NewGuid(),AdvertiserId=Guid.NewGuid(),
            Status=status,SourceSystem="FICTIONAL_SEEDED_FOR_REVIEW",
            CreatedAt=when,UpdatedAt=when,IdempotencyKey=Guid.NewGuid().ToString("N")
        };
        db.WeddingPlannerWorkspaces.Add(workspace);
        await db.SaveChangesAsync();
        return workspace;
    }

    [Fact]
    public async Task Scoped_application_workspace_is_read_but_real_ownership_schema_absence_blocks_all_creation()
    {
        await using var db=Db();
        var workspace=await SeedWorkspace(db);
        var planner=new AskAlphaAuthoritativeEnrollmentPlanner(db,ApprovedFixtureOptions());
        var before=await db.WeddingPlannerPlanningSessions.CountAsync();
        var result=await planner.InspectAsync(Principal(),workspace.Id,
            "fictional-new-visit","new_visit_12345");
        Assert.Equal("REAL_VISITOR_OWNERSHIP_SCHEMA_AND_SIGNED_IDP_MISSING",result.Status);
        Assert.True(result.AuthenticatedClaimPrerequisitesMatch);
        Assert.True(result.WorkspaceExistsAndActive);
        Assert.False(result.VisitorOwnershipSchemaAvailable);
        Assert.False(result.NewVisitorSessionCreated);
        Assert.False(result.RealVisitorIdentityVerified);
        Assert.False(result.HistoricalSessionOwnershipVerified);
        Assert.False(result.VisitorConsentRecorded);
        Assert.False(result.TranscriptLinked);
        Assert.False(result.TranscriptExportAuthorized);
        Assert.False(result.PaymentAuthorized);
        Assert.False(result.CreatorPayoutAuthorized);
        Assert.False(result.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",result.Delivery);
        Assert.Equal(before,await db.WeddingPlannerPlanningSessions.CountAsync());
        Assert.DoesNotContain(db.ChangeTracker.Entries(),
            e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Fact]
    public async Task Missing_disabled_foreign_and_inactive_scopes_fail_without_data_mutation()
    {
        await using var db=Db();
        var active=await SeedWorkspace(db);
        var inactive=await SeedWorkspace(db,"CLOSED");
        var gate=new AskAlphaAuthoritativeEnrollmentPlanner(db,ApprovedFixtureOptions());
        var off=new AskAlphaAuthoritativeEnrollmentPlanner(db,ApprovedFixtureOptions(false));
        var unknown=await gate.InspectAsync(Principal(),Guid.NewGuid(),
            "fictional-new-visit","new_visit_12345");
        Assert.Equal("SCOPED_WORKSPACE_UNAVAILABLE",unknown.Status);
        Assert.False(unknown.WorkspaceExistsAndActive);
        Assert.Equal("SCOPED_WORKSPACE_UNAVAILABLE",
            (await gate.InspectAsync(Principal(),inactive.Id,"fictional-new-visit","new_visit_12345")).Status);
        Assert.Equal("REAL_VISITOR_ENROLLMENT_NOT_AUTHORIZED",
            (await off.InspectAsync(Principal(),active.Id,"fictional-new-visit","new_visit_12345")).Status);
        Assert.Equal("REAL_VISITOR_ENROLLMENT_NOT_AUTHORIZED",
            (await gate.InspectAsync(Principal("bliss.advertiser"),active.Id,
                "fictional-new-visit","new_visit_12345")).Status);
        Assert.Equal("REAL_VISITOR_ENROLLMENT_NOT_AUTHORIZED",
            (await gate.InspectAsync(null,active.Id,
                "fictional-new-visit","new_visit_12345")).Status);
        Assert.Equal("INVALID_NEW_SESSION_SCOPE",
            (await gate.InspectAsync(Principal(),active.Id,"../forged","new_visit_12345")).Status);
        Assert.Equal("INVALID_NEW_SESSION_SCOPE",
            (await gate.InspectAsync(Principal(),active.Id,"fictional-new-visit","bad")).Status);
        Assert.Equal("INVALID_NEW_SESSION_SCOPE",
            (await gate.InspectAsync(Principal(),Guid.Empty,"fictional-new-visit","new_visit_12345")).Status);
        Assert.Equal(0,await db.WeddingPlannerPlanningSessions.CountAsync());
    }

    [Fact]
    public async Task Advertiser_or_staff_session_with_matching_retry_never_becomes_visitor_owned()
    {
        await using var db=Db();
        var workspace=await SeedWorkspace(db);
        var other=await SeedWorkspace(db);
        var when=DateTime.UtcNow;
        var existing=new WeddingPlannerPlanningSession {
            Id=Guid.NewGuid(),WorkspaceId=workspace.Id,AdvertiserId=workspace.AdvertiserId,
            Status="OPEN",SourceSystem="FICTIONAL_ASK_ALPHA_WAKE",IdempotencyKey="new_visit_12345",
            CreatedAt=when,UpdatedAt=when
        };
        db.WeddingPlannerPlanningSessions.Add(existing);
        await db.SaveChangesAsync();
        var gate=new AskAlphaAuthoritativeEnrollmentPlanner(db,ApprovedFixtureOptions());
        foreach(var ws in new[]{workspace.Id,other.Id})
        {
            var reading=await gate.InspectAsync(Principal(),ws,"fictional-new-visit","new_visit_12345");
            Assert.Equal("UNOWNED_SESSION_REPLAY_DENIED",reading.Status);
            Assert.True(reading.WorkspaceExistsAndActive);
            Assert.False(reading.NewVisitorSessionCreated);
            Assert.False(reading.HistoricalSessionOwnershipVerified);
            Assert.False(reading.VisitorConsentRecorded);
            Assert.False(reading.TranscriptExportAuthorized);
            Assert.Equal("NOT_SENT",reading.Delivery);
        }
        var stored=await db.WeddingPlannerPlanningSessions.AsNoTracking()
            .SingleAsync(x=>x.Id==existing.Id);
        Assert.Equal(workspace.AdvertiserId,stored.AdvertiserId);
        Assert.Equal("FICTIONAL_ASK_ALPHA_WAKE",stored.SourceSystem);
        Assert.Single(await db.WeddingPlannerPlanningSessions.ToListAsync());
    }
}
