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
            var readback=new AskAlphaVisitorStagingConsentReadback(stagingData);
            var consentScope=new AskAlphaStagingConsentScope(
                enrolled.OwnershipId,workspace,issuer,newRequest.SubjectSha256,
                acceptedPurpose,acceptedVersion);
            var initial=await readback.ReadAsync(consentScope);
            Assert.Equal("SYNTHETIC_STAGING_NO_OPT_IN",initial.Status);
            Assert.False(initial.SyntheticOptInSeen);
            Assert.False(initial.AuthenticatedConsentRecorded);
            Assert.False(initial.TranscriptExportAuthorized);
            Assert.Equal("NOT_SENT",initial.Delivery);

            // Wrong ownership, issuer, digest or tenant must look identical:
            // never reveal whether another visitor has ever opted in.
            foreach(var wrongScope in new[] {
                consentScope with { OwnershipId=Guid.NewGuid() },
                consentScope with { WorkspaceId=Guid.NewGuid() },
                consentScope with { Issuer="https://other-idp.invalid" },
                consentScope with { SubjectSha256=new string('E',64) }
            })
            {
                var denied=await readback.ReadAsync(wrongScope);
                Assert.Equal("SYNTHETIC_STAGING_SCOPE_NOT_AUTHORIZED",denied.Status);
                Assert.False(denied.SyntheticOptInSeen);
                Assert.False(denied.SyntheticWithdrawn);
                Assert.False(denied.TranscriptExportAuthorized);
                Assert.False(denied.PaymentAuthorized);
                Assert.False(denied.CreatorPayoutAuthorized);
                Assert.False(denied.CampaignDeliveryAuthorized);
                Assert.Equal("NOT_SENT",denied.Delivery);
            }
            await Assert.ThrowsAsync<ArgumentException>(()=>
                readback.ReadAsync(consentScope with { Purpose="TRANSCRIPT_EXPORT" }));
            await Assert.ThrowsAsync<ArgumentException>(()=>
                readback.ReadAsync(consentScope with { NoticeVersion="REAL_LEGAL_NOTICE" }));

            // Create a second synthetic session BEFORE withdrawal so tests
            // can prove terminal revocation also blocks another ownership id,
            // not just the original consent row's unique action constraint.
            var secondOwner=await enrollment.CreateNewAsync(newRequest with {
                RequestKey="second_owner_before_revoke_123"
            });
            Assert.NotEqual(enrolled.OwnershipId,secondOwner.OwnershipId);
            Assert.NotEqual(enrolled.SessionId,secondOwner.SessionId);

            // An inactive planning session or workspace must never record
            // an OPT_IN, even if the immutable ownership row already exists.
            // Test both service guard and independently enforced SQL trigger.
            await Run("""
                UPDATE "WeddingPlannerPlanningSessions" SET "Status"='CLOSED'
                WHERE "Id"=@session
                """,("session",enrolled.SessionId));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(newlyScoped));
            await Rejected(()=>Run(consent,
                ("id",Guid.NewGuid()),("owner",enrolled.OwnershipId),
                ("ws",workspace),("issuer",issuer),
                ("digest",newRequest.SubjectSha256),("action","OPT_IN"),
                ("purpose",acceptedPurpose),("version",acceptedVersion),
                ("retry","closed_session_direct_optin"),
                ("at",DateTimeOffset.UtcNow)));
            await Run("""
                UPDATE "WeddingPlannerPlanningSessions" SET "Status"='OPEN'
                WHERE "Id"=@session
                """,("session",enrolled.SessionId));
            await Run("""
                UPDATE "WeddingPlannerWorkspaces" SET "Status"='SUSPENDED'
                WHERE "Id"=@workspace
                """,("workspace",workspace));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(newlyScoped));
            await Rejected(()=>Run(consent,
                ("id",Guid.NewGuid()),("owner",enrolled.OwnershipId),
                ("ws",workspace),("issuer",issuer),
                ("digest",newRequest.SubjectSha256),("action","OPT_IN"),
                ("purpose",acceptedPurpose),("version",acceptedVersion),
                ("retry","suspended_ws_direct_optin"),
                ("at",DateTimeOffset.UtcNow)));
            await Run("""
                UPDATE "WeddingPlannerWorkspaces" SET "Status"='ACTIVE'
                WHERE "Id"=@workspace
                """,("workspace",workspace));

            var newOptin=await ledger.AppendAsync(newlyScoped);
            Assert.False(newOptin.AuthenticatedVisitorConsentRecorded);
            Assert.False(newOptin.TranscriptExportAuthorized);
            var afterOptin=await readback.ReadAsync(consentScope);
            Assert.Equal("SYNTHETIC_STAGING_OPT_IN_UNVERIFIED_NO_EXPORT",afterOptin.Status);
            Assert.True(afterOptin.SyntheticOptInSeen);
            Assert.False(afterOptin.SyntheticWithdrawn);
            Assert.False(afterOptin.AuthenticatedConsentRecorded);
            Assert.False(afterOptin.TranscriptExportAuthorized);
            Assert.Equal("NOT_SENT",afterOptin.Delivery);

            // Revocation must remain possible after both the session and
            // workspace close. An existing OPT_IN replay, by contrast, now
            // denies inactive scope even if its idempotency key exists.
            await Run("""
                UPDATE "WeddingPlannerPlanningSessions" SET "Status"='CLOSED'
                WHERE "Id"=@session
                """,("session",enrolled.SessionId));
            await Run("""
                UPDATE "WeddingPlannerWorkspaces" SET "Status"='SUSPENDED'
                WHERE "Id"=@workspace
                """,("workspace",workspace));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(newlyScoped));
            var newRevoke=await ledger.AppendAsync(newlyScoped with {
                Action="REVOKE",RetryKey="new_scope_revoke_123"
            });
            Assert.False(newRevoke.AuthenticatedVisitorConsentRecorded);
            await Run("""
                UPDATE "WeddingPlannerPlanningSessions" SET "Status"='OPEN'
                WHERE "Id"=@session
                """,("session",enrolled.SessionId));
            await Run("""
                UPDATE "WeddingPlannerWorkspaces" SET "Status"='ACTIVE'
                WHERE "Id"=@workspace
                """,("workspace",workspace));
            Assert.False(newRevoke.AuthenticatedVisitorConsentRecorded);
            Assert.Equal("NOT_SENT",newRevoke.Delivery);
            var afterRevoke=await readback.ReadAsync(consentScope);
            Assert.Equal("SYNTHETIC_STAGING_WITHDRAWN_NO_EXPORT",afterRevoke.Status);
            Assert.True(afterRevoke.SyntheticOptInSeen);
            Assert.True(afterRevoke.SyntheticWithdrawn);
            Assert.False(afterRevoke.AuthenticatedConsentRecorded);
            Assert.False(afterRevoke.TranscriptExportAuthorized);
            Assert.False(afterRevoke.PaymentAuthorized);
            Assert.False(afterRevoke.CreatorPayoutAuthorized);
            Assert.False(afterRevoke.CampaignDeliveryAuthorized);
            Assert.Equal("NOT_SENT",afterRevoke.Delivery);

            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(newlyScoped));

            // Terminal withdrawal must not be evaded by switching request
            // keys or choosing an additional ownership created earlier.
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                enrollment.CreateNewAsync(newRequest with {
                    RequestKey="fresh_request_after_revoke_123"
                }));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                enrollment.CreateNewAsync(newRequest));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(newlyScoped with {
                    OwnershipId=secondOwner.OwnershipId,
                    RetryKey="second_owner_after_revoke_123"
                }));
            // The disposable SQL trigger enforces the same tombstone even
            // when the application writer is bypassed by a raw SQL INSERT.
            await Rejected(()=>Run(consent,
                ("id",Guid.NewGuid()),("owner",secondOwner.OwnershipId),
                ("ws",workspace),("issuer",issuer),
                ("digest",newRequest.SubjectSha256),("action","OPT_IN"),
                ("purpose",acceptedPurpose),("version",acceptedVersion),
                ("retry","raw_sql_reoptin_123"),
                ("at",DateTimeOffset.UtcNow)));

            // Concurrent requests with distinct retry keys must all fail
            // after the same synthetic identity has been withdrawn.
            var deniedRetries=await Task.WhenAll(Enumerable.Range(0,8)
                .Select(async i => {
                    await Assert.ThrowsAsync<InvalidOperationException>(()=>
                        enrollment.CreateNewAsync(newRequest with {
                            RequestKey=$"post_revoke_parallel_{i:D2}_123"
                        }));
                    return i;
                }));
            Assert.Equal(8,deniedRetries.Length);
            Assert.Equal(4,await check.WeddingPlannerPlanningSessions
                .AsNoTracking().CountAsync());

            // Simulate a bypass of the application enrollment writer:
            // insert a fresh synthetic planning session in the SAME SQL
            // transaction, then try to attach ownership through direct SQL.
            // The disposable ownership trigger must reject it. Rolling back
            // the transaction must also remove the session inserted first.
            var bypassSession=Guid.NewGuid();
            var bypassOwner=Guid.NewGuid();
            await using(var rawTxn=await db.BeginTransactionAsync())
            {
                await using(var addSession=new NpgsqlCommand("""
                    INSERT INTO "WeddingPlannerPlanningSessions"
                      ("Id","WorkspaceId","AdvertiserId","Status",
                       "SourceSystem","IdempotencyKey","CreatedAt","UpdatedAt")
                    VALUES (@session,@ws,@advertiser,'OPEN',
                            'STAGING_SYNTHETIC_VISITOR_NEW_SESSION',
                            @key,@created,@created)
                    """,db,rawTxn))
                {
                    addSession.Parameters.AddWithValue("session",bypassSession);
                    addSession.Parameters.AddWithValue("ws",workspace);
                    addSession.Parameters.AddWithValue("advertiser",advertiser);
                    addSession.Parameters.AddWithValue("key",Guid.NewGuid().ToString("N"));
                    addSession.Parameters.AddWithValue("created",DateTime.UtcNow);
                    Assert.Equal(1,await addSession.ExecuteNonQueryAsync());
                }
                await using(var addOwner=new NpgsqlCommand("""
                    INSERT INTO "AskAlphaVisitorOwnershipStaging"
                      ("Id","WorkspaceId","SessionId","AdvertiserId",
                       "Issuer","SubjectSha256","RequestKey","CreatedAt")
                    VALUES (@owner,@ws,@session,@advertiser,
                            @issuer,@subject,@key,@created)
                    """,db,rawTxn))
                {
                    addOwner.Parameters.AddWithValue("owner",bypassOwner);
                    addOwner.Parameters.AddWithValue("ws",workspace);
                    addOwner.Parameters.AddWithValue("session",bypassSession);
                    addOwner.Parameters.AddWithValue("advertiser",advertiser);
                    addOwner.Parameters.AddWithValue("issuer",issuer);
                    addOwner.Parameters.AddWithValue("subject",newRequest.SubjectSha256);
                    addOwner.Parameters.AddWithValue("key","direct_session_bypass_123");
                    addOwner.Parameters.AddWithValue("created",DateTime.UtcNow);
                    var denied=await Assert.ThrowsAsync<PostgresException>(
                        async()=>await addOwner.ExecuteNonQueryAsync());
                    Assert.Equal("23514",denied.SqlState);
                }
                await rawTxn.RollbackAsync();
            }
            Assert.Equal(4,await check.WeddingPlannerPlanningSessions
                .AsNoTracking().CountAsync());
            await using(var noOrphan=new NpgsqlCommand("""
                SELECT count(*) FROM "WeddingPlannerPlanningSessions" WHERE "Id"=@id
                """,db))
            {
                noOrphan.Parameters.AddWithValue("id",bypassSession);
                Assert.Equal(0L,await noOrphan.ExecuteScalarAsync());
            }

            // Cross-tenant isolation: the same synthetic OIDC issuer/subject
            // and identical retry key in another actual advertiser/workspace
            // must never adopt the first workspace's visitor session or
            // inherit its opt-in or terminal withdrawal record.
            var otherAdvertiser=Guid.NewGuid();
            var otherWorkspace=Guid.NewGuid();
            await using(var otherTenant=new BlissDbContext(options))
            {
                otherTenant.Advertisers.Add(new Advertiser {
                    Id=otherAdvertiser,Name="Other isolated test advertiser",CreatedAt=now
                });
                otherTenant.WeddingPlannerWorkspaces.Add(new WeddingPlannerWorkspace {
                    Id=otherWorkspace,AdvertiserId=otherAdvertiser,
                    Status="ACTIVE",SourceSystem="STAGING_ONLY",
                    IdempotencyKey=Guid.NewGuid().ToString("N"),
                    CreatedAt=now,UpdatedAt=now
                });
                await otherTenant.SaveChangesAsync();
            }
            var otherRequest=newRequest with { WorkspaceId=otherWorkspace };
            var otherEnrollment=await enrollment.CreateNewAsync(otherRequest);
            Assert.NotEqual(enrolled.OwnershipId,otherEnrollment.OwnershipId);
            Assert.NotEqual(enrolled.SessionId,otherEnrollment.SessionId);
            Assert.False(otherEnrollment.RealVisitorAuthenticated);
            Assert.False(otherEnrollment.VisitorConsentRecorded);
            Assert.False(otherEnrollment.TranscriptExportAuthorized);
            Assert.Equal("NOT_SENT",otherEnrollment.Delivery);

            var otherScope=consentScope with {
                OwnershipId=otherEnrollment.OwnershipId,
                WorkspaceId=otherWorkspace
            };
            var otherBefore=await readback.ReadAsync(otherScope);
            Assert.Equal("SYNTHETIC_STAGING_NO_OPT_IN",otherBefore.Status);
            Assert.False(otherBefore.SyntheticOptInSeen);
            Assert.False(otherBefore.SyntheticWithdrawn);
            Assert.False(otherBefore.TranscriptExportAuthorized);

            // Swapping only the owner or workspace UUID must not disclose
            // a consent record belonging to the other synthetic tenant.
            foreach(var crossed in new[] {
                consentScope with { OwnershipId=otherEnrollment.OwnershipId },
                otherScope with { OwnershipId=enrolled.OwnershipId }
            })
            {
                var denied=await readback.ReadAsync(crossed);
                Assert.Equal("SYNTHETIC_STAGING_SCOPE_NOT_AUTHORIZED",denied.Status);
                Assert.False(denied.SyntheticOptInSeen);
                Assert.False(denied.SyntheticWithdrawn);
                Assert.False(denied.TranscriptExportAuthorized);
                Assert.False(denied.CampaignDeliveryAuthorized);
            }

            // A separate synthetic opt-in in the other tenant cannot undo
            // the first tenant's permanent withdrawal.
            var otherOptIn=newlyScoped with {
                OwnershipId=otherEnrollment.OwnershipId,
                WorkspaceId=otherWorkspace,
                RetryKey="other_tenant_optin_123"
            };
            var otherStored=await ledger.AppendAsync(otherOptIn);
            Assert.False(otherStored.AuthenticatedVisitorConsentRecorded);
            Assert.False(otherStored.TranscriptExportAuthorized);
            var otherAfter=await readback.ReadAsync(otherScope);
            Assert.Equal("SYNTHETIC_STAGING_OPT_IN_UNVERIFIED_NO_EXPORT",otherAfter.Status);
            Assert.True(otherAfter.SyntheticOptInSeen);
            Assert.False(otherAfter.SyntheticWithdrawn);
            Assert.False(otherAfter.TranscriptExportAuthorized);
            Assert.Equal("NOT_SENT",otherAfter.Delivery);

            var originalStillRevoked=await readback.ReadAsync(consentScope);
            Assert.Equal("SYNTHETIC_STAGING_WITHDRAWN_NO_EXPORT",originalStillRevoked.Status);
            Assert.True(originalStillRevoked.SyntheticWithdrawn);
            Assert.False(originalStillRevoked.TranscriptExportAuthorized);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>
                ledger.AppendAsync(newlyScoped));

            // Disabling the second workspace must fail the readback closed,
            // rather than showing opt-in state for an inactive tenant.
            await using(var suspend=new NpgsqlCommand("""
                UPDATE "WeddingPlannerWorkspaces"
                SET "Status"='INACTIVE'
                WHERE "Id"=@id
                """,db))
            {
                suspend.Parameters.AddWithValue("id",otherWorkspace);
                Assert.Equal(1,await suspend.ExecuteNonQueryAsync());
            }
            var suspended=await readback.ReadAsync(otherScope);
            Assert.Equal("SYNTHETIC_STAGING_SCOPE_NOT_AUTHORIZED",suspended.Status);
            Assert.False(suspended.SyntheticOptInSeen);
            Assert.False(suspended.SyntheticWithdrawn);
            Assert.False(suspended.TranscriptExportAuthorized);
            Assert.False(suspended.CampaignDeliveryAuthorized);
            Assert.Equal("NOT_SENT",suspended.Delivery);


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
