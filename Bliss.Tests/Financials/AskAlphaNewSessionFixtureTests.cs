using System.Security.Claims;
using System.Security.Cryptography;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class AskAlphaNewSessionFixtureTests
{
    private const string Issuer="https://issuer.fixture.invalid";
    private const string Subject="fixture-visitor-12345678";
    private const string Slug="synthetic-advertiser-consent";
    private static ClaimsPrincipal Principal(Guid session,string role="bliss.ask_alpha_visitor")
    {
        var id=new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role,role,ClaimValueTypes.String,Issuer),
            new Claim("sub",Subject,ClaimValueTypes.String,Issuer),
            new Claim("ask_alpha_session_id",session.ToString("D"),ClaimValueTypes.String,Issuer)
        },"synthetic-oidc");
        return new ClaimsPrincipal(id);
    }
    private static BlissAuthenticationOptions Auth() => new()
    {
        Enabled=true,AskAlphaVisitorIssuer=Issuer
    };

    [FinancialPostgresFact]
    public async Task Persisted_scoped_fictional_session_and_at_creation_receipt_must_match_without_export_authority()
    {
        var connection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;
        var builder=new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1",builder.Host);
        Assert.Equal("financial_test",builder.Database);
        await using var pg=NpgsqlDataSource.Create(connection);
        var key=RandomNumberGenerator.GetBytes(32);
        var ledger=new AskAlphaOwnershipFixtureStore(pg,key);
        var when=DateTime.UtcNow;
        var workspace=new WeddingPlannerWorkspace {
            Id=Guid.NewGuid(), AdvertiserId=Guid.NewGuid(), Status="ACTIVE", CreatedAt=when,UpdatedAt=when
        };
        var session=new WeddingPlannerPlanningSession {
            Id=Guid.NewGuid(),WorkspaceId=workspace.Id,AdvertiserId=workspace.AdvertiserId,
            SourceSystem="FICTIONAL_ASK_ALPHA_WAKE",Status="OPEN",CreatedAt=when,UpdatedAt=when,
            IdempotencyKey=Guid.NewGuid().ToString("N")
        };
        var options=new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db=new BlissDbContext(options);
        db.WeddingPlannerWorkspaces.Add(workspace);
        db.WeddingPlannerPlanningSessions.Add(session);
        await db.SaveChangesAsync();
        var scope=new AskAlphaOwnershipFixtureScope(workspace.Id,session.Id,Slug,Issuer);
        await ledger.EnrollSyntheticAtCreationAsync(scope,Subject,new DateTimeOffset(when));
        var reader=new AskAlphaNewSessionFixturePreflight(db,ledger,Auth());
        var valid=await reader.InspectAsync(Principal(session.Id),
            workspace.Id,session.Id,Slug,new DateTimeOffset(when).AddSeconds(10),key);
        Assert.Equal("SYNTHETIC_STORED_NEW_SESSION_MATCH_HISTORY_UNVERIFIED",valid.Status);
        Assert.True(valid.ClaimsMatchIssuerAndSession);
        Assert.False(valid.HistoricalSessionOwnershipVerified);
        Assert.False(valid.VisitorConsentRecorded);
        Assert.False(valid.TranscriptLinked);
        Assert.False(valid.TranscriptExportAuthorized);
        Assert.False(valid.PaymentAuthorized);
        Assert.False(valid.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",valid.Delivery);

        async Task AssertRejected(Guid ws,Guid id,string slug,ClaimsPrincipal actor)
        {
            var result=await reader.InspectAsync(actor,ws,id,slug,
                new DateTimeOffset(when).AddSeconds(10),key);
            Assert.NotEqual("SYNTHETIC_STORED_NEW_SESSION_MATCH_HISTORY_UNVERIFIED",result.Status);
            Assert.False(result.HistoricalSessionOwnershipVerified);
            Assert.False(result.VisitorConsentRecorded);
            Assert.False(result.TranscriptExportAuthorized);
            Assert.False(result.CampaignDeliveryAuthorized);
        }
        await AssertRejected(Guid.NewGuid(),session.Id,Slug,Principal(session.Id));
        await AssertRejected(workspace.Id,Guid.NewGuid(),Slug,Principal(Guid.NewGuid()));
        await AssertRejected(workspace.Id,session.Id,"changed-source",Principal(session.Id));
        await AssertRejected(workspace.Id,session.Id,Slug,Principal(session.Id,"bliss.advertiser"));

        session.SourceSystem="LEGACY_UNVERIFIED";
        await db.SaveChangesAsync();
        await AssertRejected(workspace.Id,session.Id,Slug,Principal(session.Id));
        session.SourceSystem="FICTIONAL_ASK_ALPHA_WAKE";
        session.CreatedAt=when.AddMinutes(-5);
        await db.SaveChangesAsync();
        await AssertRejected(workspace.Id,session.Id,Slug,Principal(session.Id));
        session.CreatedAt=when;
        await db.SaveChangesAsync();

        // A message predating receipt enrollment must never be recast as
        // prospectively consented / linked by a later signed fixture record.
        var message=new WeddingPlannerConversationMessage {
            Id=Guid.NewGuid(),WorkspaceId=workspace.Id,SessionId=session.Id,
            AdvertiserId=workspace.AdvertiserId,CreatedAt=when.AddMilliseconds(-100),
            SequenceNumber=1,Body="Synthetic only",ActorType="ADVERTISER",SourceSystem="FIXTURE",
            ActorLabel="TEST",IdempotencyKey=Guid.NewGuid().ToString("N")
        };
        db.WeddingPlannerConversationMessages.Add(message);
        await db.SaveChangesAsync();
        await AssertRejected(workspace.Id,session.Id,Slug,Principal(session.Id));
        db.WeddingPlannerConversationMessages.Remove(message);
        await db.SaveChangesAsync();
        workspace.AdvertiserId=Guid.NewGuid();
        await db.SaveChangesAsync();
        await AssertRejected(workspace.Id,session.Id,Slug,Principal(session.Id));
    }

    [Fact]
    public async Task Authentication_missing_never_probes_nonexistent_local_session_or_receipt()
    {
        using var source=NpgsqlDataSource.Create(
            "Host=127.0.0.1;Database=financial_test;Username=fixture;Password=fixture");
        var store=new AskAlphaOwnershipFixtureStore(source,RandomNumberGenerator.GetBytes(32));
        await using var db=new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var sessionId=Guid.NewGuid();
        var preflight=new AskAlphaNewSessionFixturePreflight(db,store,
            new BlissAuthenticationOptions { Enabled=false });
        var row=await preflight.InspectAsync(Principal(sessionId),Guid.NewGuid(),
            sessionId,Slug,DateTimeOffset.UtcNow,RandomNumberGenerator.GetBytes(32));
        Assert.Equal("STORED_NEW_SESSION_PREREQUISITES_MISSING",row.Status);
        Assert.False(row.ClaimsMatchIssuerAndSession);
        Assert.False(row.TranscriptExportAuthorized);
    }
}
