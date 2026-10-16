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
        Assert.True(
            parsed.Host == "127.0.0.1" && parsed.Database == "planner_lineage_ci" ||
            parsed.Host == "localhost" && parsed.Database == "bliss_ci",
            "Only dedicated loopback CI databases may run Stage J schema tests.");
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
            // DB-assigned event times must be after the synthetic ownership creation.
            var now=DateTime.UtcNow.AddMinutes(-5);
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

            // Staging-only application writer: atomic retry serialization, DB clock,
            // exact binding and default-off real authorization.
            await using var stagingData=NpgsqlDataSource.Create(parsed.ConnectionString);
            var ledger=new AskAlphaVisitorStagingConsentLedger(stagingData);
            var optin=new AskAlphaStagingConsentRequest(
                owner,workspace,issuer,subject,"OPT_IN",acceptedPurpose,
                acceptedVersion,"approved_fixture_123");
            var competing=await Task.WhenAll(Enumerable.Range(0,8)
                .Select(_=>ledger.AppendAsync(optin)));
            Assert.Single(competing.Select(x=>x.EventId).Distinct());
            Assert.Equal(1,competing.Count(x=>!x.IsReplay));
            Assert.Equal(7,competing.Count(x=>x.IsReplay));
            Assert.All(competing,x=>{
                Assert.False(x.RealVisitorIdentityVerified);
                Assert.False(x.AuthenticatedVisitorConsentRecorded);
                Assert.False(x.TranscriptExportAuthorized);
                Assert.False(x.PaymentAuthorized);
                Assert.False(x.CreatorPayoutAuthorized);
                Assert.False(x.CampaignDeliveryAuthorized);
                Assert.Equal("NOT_SENT",x.Delivery);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(optin with { Action="REVOKE" }));
            await Assert.ThrowsAsync<ArgumentException>(()=>
                ledger.AppendAsync(optin with { NoticeVersion="unreviewed-notice" }));
            await Assert.ThrowsAsync<PostgresException>(()=>
                ledger.AppendAsync(optin with {
                    RetryKey="wrong_digest_key_123",SubjectSha256=new string('B',64)
                }));
            await Rejected(()=>WriteConsent("OPT_IN",acceptedVersion,acceptedPurpose,"duplicate_optin_123"));
            await Rejected(()=>WriteConsent("REVOKE",acceptedVersion,acceptedPurpose,"wrong_clock_1234",offset:2));
            var revoked=new AskAlphaStagingConsentRequest(
                owner,workspace,issuer,subject,"REVOKE",acceptedPurpose,
                acceptedVersion,"withdraw_fixture_123");
            var withdrawals=await Task.WhenAll(Enumerable.Range(0,5)
                .Select(_=>ledger.AppendAsync(revoked)));
            Assert.Single(withdrawals.Select(x=>x.EventId).Distinct());
            Assert.Equal(1,withdrawals.Count(x=>!x.IsReplay));
            Assert.Equal(4,withdrawals.Count(x=>x.IsReplay));
            Assert.All(withdrawals,x=>{
                Assert.Equal("SYNTHETIC_STAGING_REVOCATION_RECORDED_NO_EXPORT",x.Status);
                Assert.False(x.AuthenticatedVisitorConsentRecorded);
                Assert.False(x.TranscriptExportAuthorized);
                Assert.Equal("NOT_SENT",x.Delivery);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(optin));
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
            
            // The NEW synthetic visitor session and immutable ownership must
            // commit together against the actual Wedding Planner EF tables.
            var enrollment=new AskAlphaVisitorStagingEnrollment(stagingData);
            var newRequest=new AskAlphaStagingEnrollmentRequest(
                workspace,issuer,new string('C',64),"atomic_fixture_request_123");
            var attempts=await Task.WhenAll(Enumerable.Range(0,8)
                .Select(_=>enrollment.CreateNewAsync(newRequest)));
            Assert.Single(attempts.Select(x=>x.SessionId).Distinct());
            Assert.Single(attempts.Select(x=>x.OwnershipId).Distinct());
            Assert.Equal(1,attempts.Count(x=>!x.IsReplay));
            Assert.Equal(7,attempts.Count(x=>x.IsReplay));
            var enrolled=attempts[0];
            Assert.NotEqual(session,enrolled.SessionId);
            Assert.NotEqual(legacy,enrolled.SessionId);
            Assert.All(attempts,x=>{
                Assert.False(x.RealVisitorAuthenticated);
                Assert.False(x.VisitorConsentRecorded);
                Assert.False(x.TranscriptExportAuthorized);
                Assert.False(x.PaymentAuthorized);
                Assert.False(x.CreatorPayoutAuthorized);
                Assert.False(x.CampaignDeliveryAuthorized);
                Assert.Equal("NOT_SENT",x.Delivery);
            });
            await using(var exact=new NpgsqlCommand("""
                SELECT count(*)
                FROM "AskAlphaVisitorOwnershipStaging" o
                JOIN "WeddingPlannerPlanningSessions" s ON s."Id"=o."SessionId"
                WHERE o."Id"=@owner AND s."Id"=@session
                  AND s."WorkspaceId"=@workspace
                  AND s."SourceSystem"='STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
                """,db))
            {
                exact.Parameters.AddWithValue("owner",enrolled.OwnershipId);
                exact.Parameters.AddWithValue("session",enrolled.SessionId);
                exact.Parameters.AddWithValue("workspace",workspace);
                Assert.Equal(1L,await exact.ExecuteScalarAsync());
            }

            // Missing workspace cannot leave a partially-created session.
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                enrollment.CreateNewAsync(newRequest with {
                    WorkspaceId=Guid.NewGuid(),RequestKey="missing_workspace_retry_123"
                }));
            await Assert.ThrowsAsync<ArgumentException>(()=>
                enrollment.CreateNewAsync(newRequest with { SubjectSha256="forged" }));
            await Assert.ThrowsAsync<ArgumentException>(()=>
                enrollment.CreateNewAsync(newRequest with { Issuer="http://evil.invalid" }));
            Assert.Equal(3,await check.WeddingPlannerPlanningSessions.AsNoTracking().CountAsync());
            Assert.Equal("FICTIONAL_ASK_ALPHA_WAKE",
                (await check.WeddingPlannerPlanningSessions.AsNoTracking()
                    .SingleAsync(x=>x.Id==legacy)).SourceSystem);

            // Prove atomic rollback when the second DB write fails AFTER
            // the new EF planning session insert. This guard exists only
            // inside the disposable schema and is dropped with that schema.
            await using(var injectFailure = new NpgsqlCommand("""
                CREATE FUNCTION agm_j_reject_test_ownership()
                RETURNS trigger LANGUAGE plpgsql AS $agm$
                BEGIN
                  RAISE EXCEPTION 'synthetic ownership insert rejected'
                    USING ERRCODE='23514';
                END $agm$;
                CREATE TRIGGER agm_j_force_test_owner_rejection
                  BEFORE INSERT ON "AskAlphaVisitorOwnershipStaging"
                  FOR EACH ROW
                  WHEN (NEW."SubjectSha256" =
                    'DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD')
                  EXECUTE FUNCTION agm_j_reject_test_ownership();
                """, db))
                await injectFailure.ExecuteNonQueryAsync();
            await Assert.ThrowsAsync<PostgresException>(()=>
                enrollment.CreateNewAsync(newRequest with {
                    SubjectSha256=new string('D',64),
                    RequestKey="forced_ownership_failure_123"
                }));
            Assert.Equal(3, await check.WeddingPlannerPlanningSessions
                .AsNoTracking().CountAsync());
            await using(var noOrphan = new NpgsqlCommand("""
                SELECT count(*) FROM "WeddingPlannerPlanningSessions"
                WHERE "SourceSystem"='STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
                """, db))
                Assert.Equal(2L, await noOrphan.ExecuteScalarAsync());

            // Consent remains independent and voluntarily asserted even for
            // the synthetic new ownership; a session cannot imply consent.
            var newlyScoped=new AskAlphaStagingConsentRequest(
                enrolled.OwnershipId,workspace,issuer,newRequest.SubjectSha256,
                "OPT_IN",acceptedPurpose,acceptedVersion,"new_scope_optin_123");
            var newOptin=await ledger.AppendAsync(newlyScoped);
            Assert.False(newOptin.AuthenticatedVisitorConsentRecorded);
            Assert.False(newOptin.TranscriptExportAuthorized);
            var newRevoke=await ledger.AppendAsync(newlyScoped with {
                Action="REVOKE",RetryKey="new_scope_revoke_123"
            });
            Assert.False(newRevoke.AuthenticatedVisitorConsentRecorded);
            Assert.Equal("NOT_SENT",newRevoke.Delivery);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(newlyScoped));

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
