using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

// Runs ONLY in the isolated planner_lineage_ci postgres fixture.
// Tests a candidate schema against the real Wedding Planner EF tables.
public sealed class AskAlphaVisitorOwnershipStagingPostgresTests
{
    [Fact]
    public async Task Scoped_synthetic_new_session_can_record_append_only_consent_then_revoke_and_rollback_schema()
    {
        var cs=Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if(string.IsNullOrWhiteSpace(cs))
        {
            if(Environment.GetEnvironmentVariable("CI") == "true")
                throw new InvalidOperationException("Isolated planner PostgreSQL missing.");
            return;
        }
        var parsed=new NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("127.0.0.1",parsed.Host);
        Assert.Equal("planner_lineage_ci",parsed.Database);
        var schema=$"agm_j_{Guid.NewGuid():N}";
        await using(var conn=new NpgsqlConnection(cs))
        {
            await conn.OpenAsync();
            await using var command=new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"",conn);
            await command.ExecuteNonQueryAsync();
        }
        try
        {
            parsed.SearchPath=schema;
            var options=new DbContextOptionsBuilder<BlissDbContext>()
                .UseNpgsql(parsed.ConnectionString).Options;
            var advertiser=Guid.NewGuid();
            var workspace=Guid.NewGuid();
            var session=Guid.NewGuid();
            var legacy=Guid.NewGuid();
            var now=DateTime.UtcNow;
            await using(var setup=new BlissDbContext(options))
            {
                await setup.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                setup.Advertisers.Add(new Advertiser {
                    Id=advertiser,Name="Synthetic staging visitor test",CreatedAt=now
                });
                setup.WeddingPlannerWorkspaces.Add(new WeddingPlannerWorkspace {
                    Id=workspace,AdvertiserId=advertiser,Status="ACTIVE",
                    SourceSystem="STAGING_ONLY",IdempotencyKey=Guid.NewGuid().ToString("N"),
                    CreatedAt=now,UpdatedAt=now
                });
                setup.WeddingPlannerPlanningSessions.AddRange(
                    new WeddingPlannerPlanningSession {
                        Id=session,WorkspaceId=workspace,AdvertiserId=advertiser,Status="OPEN",
                        SourceSystem="STAGING_SYNTHETIC_VISITOR_NEW_SESSION",
                        IdempotencyKey=Guid.NewGuid().ToString("N"),CreatedAt=now,UpdatedAt=now
                    },
                    new WeddingPlannerPlanningSession {
                        Id=legacy,WorkspaceId=workspace,AdvertiserId=advertiser,Status="OPEN",
                        SourceSystem="FICTIONAL_ASK_ALPHA_WAKE",
                        IdempotencyKey=Guid.NewGuid().ToString("N"),CreatedAt=now,UpdatedAt=now
                    }
                );
                await setup.SaveChangesAsync();
            }

            var script=FindSql();
            await using var db=new NpgsqlConnection(parsed.ConnectionString);
            await db.OpenAsync();
            await using(var install=new NpgsqlCommand(await File.ReadAllTextAsync(script),db))
                await install.ExecuteNonQueryAsync();
            var owner=Guid.NewGuid();
            const string issuer="https://idp.fixture.invalid";
            var subject=new string('A',64);
            async Task Run(string sql, params (string key,object value)[] args)
            {
                await using var command=new NpgsqlCommand(sql,db);
                foreach(var (key,value) in args)command.Parameters.AddWithValue(key,value);
                await command.ExecuteNonQueryAsync();
            }
            async Task Rejected(Func<Task> fn)
            {
                var ex=await Assert.ThrowsAsync<PostgresException>(fn);
                Assert.Contains(ex.SqlState,new[]{"23514","23505","23503","0A000"});
            }
            const string ownership="""
                INSERT INTO "AskAlphaVisitorOwnershipStaging"
                ("Id","WorkspaceId","SessionId","AdvertiserId","Issuer","SubjectSha256","RequestKey","CreatedAt")
                VALUES (@id,@ws,@sess,@adv,@issuer,@digest,@retry,@at)
                """;
            (string key,object value)[] Args(Guid id,Guid sess,Guid ws,string retry) =>
                new[] {
                    ("id",(object)id),("ws",(object)ws),("sess",(object)sess),
                    ("adv",(object)advertiser),("issuer",(object)issuer),
                    ("digest",(object)subject),("retry",(object)retry),
                    ("at",(object)new DateTimeOffset(now.AddSeconds(1)))
                };

            // Existing advertiser/staff session cannot be adopted even with
            // matching advertiser/workspace and a random ownership UUID.
            await Rejected(()=>Run(ownership,Args(Guid.NewGuid(),legacy,workspace,"legacy_retry_123")));
            await Rejected(()=>Run(ownership,Args(Guid.NewGuid(),session,Guid.NewGuid(),"foreign_retry_123")));
            await Run(ownership,Args(owner,session,workspace,"staging_req_12345"));
            await Rejected(()=>Run(ownership,Args(Guid.NewGuid(),session,workspace,"duplicate_req_123")));

            const string consent="""
                INSERT INTO "AskAlphaVisitorConsentStaging"
                ("Id","OwnershipId","WorkspaceId","Issuer","SubjectSha256",
                 "Action","Purpose","NoticeVersion","RetryKey","RecordedAt")
                VALUES (@id,@owner,@ws,@issuer,@digest,@action,@purpose,@version,@retry,@at)
                """;
            Task WriteConsent(string action,string version,string purpose,string retry,
                Guid? scope=null,string? digest=null,int offset=3)
              => Run(consent,
                ("id",Guid.NewGuid()),("owner",owner),("ws",scope??workspace),
                ("issuer",issuer),("digest",digest??subject),("action",action),
                ("purpose",purpose),("version",version),("retry",retry),
                ("at",new DateTimeOffset(now.AddSeconds(offset))));
            const string acceptedPurpose="CREATIVE_PREVIEW_ONLY";
            const string acceptedVersion="STAGING-NOT-LEGAL-V1";
            await Rejected(()=>WriteConsent("REVOKE",acceptedVersion,acceptedPurpose,"early_revoke_1234"));
            await Rejected(()=>WriteConsent("OPT_IN",acceptedVersion,acceptedPurpose,"foreign_scope_1234",Guid.NewGuid()));
            await Rejected(()=>WriteConsent("OPT_IN",acceptedVersion,acceptedPurpose,"foreign_subject_123",digest:new string('B',64)));
            await Rejected(()=>WriteConsent("OPT_IN",acceptedVersion,"TRANSCRIPT_EXPORT","export_attempt_123"));
            await Rejected(()=>WriteConsent("OPT_IN","OTHER_NOTICE","CREATIVE_PREVIEW_ONLY","wrong_notice_123"));
            await Rejected(()=>WriteConsent("OPT_IN",acceptedVersion,acceptedPurpose,"before_creation_123",offset:-1));

            await WriteConsent("OPT_IN",acceptedVersion,acceptedPurpose,"approved_fixture_123");
            await Rejected(()=>WriteConsent("OPT_IN",acceptedVersion,acceptedPurpose,"duplicate_optin_123"));
            await Rejected(()=>WriteConsent("REVOKE",acceptedVersion,acceptedPurpose,"wrong_clock_1234",offset:2));
            await WriteConsent("REVOKE",acceptedVersion,acceptedPurpose,"withdraw_fixture_123",offset:4);
            await Rejected(()=>WriteConsent("OPT_IN",acceptedVersion,acceptedPurpose,"reoptin_attempt_123",offset:5));
            await Rejected(()=>WriteConsent("REVOKE",acceptedVersion,acceptedPurpose,"double_revoke_123",offset:6));

            await using(var count=new NpgsqlCommand("""
                SELECT count(*) FROM "AskAlphaVisitorConsentStaging"
                WHERE "OwnershipId"=@owner
                """,db))
            {
                count.Parameters.AddWithValue("owner",owner);
                Assert.Equal(2L,await count.ExecuteScalarAsync());
            }
            foreach(var statement in new[] {
                "UPDATE \"AskAlphaVisitorOwnershipStaging\" SET \"Issuer\"='https://other.invalid'",
                "DELETE FROM \"AskAlphaVisitorOwnershipStaging\"",
                "TRUNCATE \"AskAlphaVisitorOwnershipStaging\"",
                "TRUNCATE \"AskAlphaVisitorOwnershipStaging\" CASCADE",
                "UPDATE \"AskAlphaVisitorConsentStaging\" SET \"Action\"='OPT_IN'",
                "DELETE FROM \"AskAlphaVisitorConsentStaging\"",
                "TRUNCATE \"AskAlphaVisitorConsentStaging\""
            })
                await Rejected(()=>Run(statement));

            // Schema candidate does not register app-side permission or expose
            // transcript/finance delivery. No global schemas or migrations.
            await using var check=new BlissDbContext(options);
            Assert.Equal(2,await check.WeddingPlannerPlanningSessions.CountAsync());
            Assert.Equal("FICTIONAL_ASK_ALPHA_WAKE",
                (await check.WeddingPlannerPlanningSessions.AsNoTracking()
                    .SingleAsync(x=>x.Id==legacy)).SourceSystem);
        }
        finally
        {
            await using var admin=new NpgsqlConnection(cs);
            await admin.OpenAsync();
            await using var drop=new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",admin);
            await drop.ExecuteNonQueryAsync();
        }
        await using(var check=new NpgsqlConnection(cs))
        {
            await check.OpenAsync();
            await using var query=new NpgsqlCommand(
                "SELECT count(*) FROM pg_namespace WHERE nspname=@schema",check);
            query.Parameters.AddWithValue("schema",schema);
            Assert.Equal(0L,await query.ExecuteScalarAsync());
        }
    }

    private static string FindSql()
    {
        for(var directory=new DirectoryInfo(Directory.GetCurrentDirectory());
            directory!=null;directory=directory.Parent)
        {
            var path=Path.Combine(directory.FullName,"Bliss.Infrastructure","Migrations",
                "AskAlphaVisitorOwnershipStagingV1.sql");
            if(File.Exists(path))return path;
        }
        throw new FileNotFoundException("Stage J candidate schema not found in repository checkout.");
    }
}
