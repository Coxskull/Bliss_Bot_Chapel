using System.Security.Cryptography;
using System.Text;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

// AGM-COMM-015: same fictional visitor can have multiple already-created
// owned sessions before one terminal withdrawal. Only disposable PostgreSQL.
public sealed class AskAlphaIdentityWideWithdrawalPostgresTests
{
    [Fact]
    public async Task Withdrawal_from_one_session_denies_all_preexisting_same_identity_sessions()
    {
        var cs = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                throw new InvalidOperationException("Dedicated PostgreSQL fixture missing.");
            return;
        }
        var parsed = new NpgsqlConnectionStringBuilder(cs);
        Assert.True(
            (parsed.Host == "127.0.0.1" && parsed.Database == "planner_lineage_ci") ||
            (parsed.Host == "localhost" && parsed.Database == "bliss_ci"),
            "Synthetic identity tests must NEVER use a hosted database.");
        var schema = $"agm_j_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(cs))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            parsed.SearchPath = schema;
            var options = new DbContextOptionsBuilder<BlissDbContext>()
                .UseNpgsql(parsed.ConnectionString).Options;
            var advertiser = Guid.NewGuid();
            var workspace = Guid.NewGuid();
            var now = DateTime.UtcNow.AddMinutes(-5);
            await using (var db = new BlissDbContext(options))
            {
                await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                db.Advertisers.Add(new Advertiser
                {
                    Id = advertiser, Name = "Fictional two-owner visitor", CreatedAt = now
                });
                db.WeddingPlannerWorkspaces.Add(new WeddingPlannerWorkspace
                {
                    Id = workspace, AdvertiserId = advertiser, Status = "ACTIVE",
                    SourceSystem = "SYNTHETIC_TEST_ONLY",
                    IdempotencyKey = Guid.NewGuid().ToString("N"),
                    CreatedAt = now, UpdatedAt = now
                });
                await db.SaveChangesAsync();
            }
            await using (var setup = new NpgsqlConnection(parsed.ConnectionString))
            {
                await setup.OpenAsync();
                foreach (var name in new[]
                {
                    "AskAlphaVisitorOwnershipStagingV1.sql",
                    "AskAlphaVisitorPrivateSnapshotStagingV1.sql"
                })
                {
                    await using var create = new NpgsqlCommand(
                        await File.ReadAllTextAsync(FindSql(name)), setup);
                    await create.ExecuteNonQueryAsync();
                }
            }

            await using var data = NpgsqlDataSource.Create(parsed.ConnectionString);
            var enroll = new AskAlphaVisitorStagingEnrollment(data);
            var ledger = new AskAlphaVisitorStagingConsentLedger(data);
            var readback = new AskAlphaVisitorStagingConsentReadback(data);
            var store = new AskAlphaVisitorPrivateSnapshotStaging(data);
            const string issuer = "https://idp.fixture.invalid";
            var digest = new string('A', 64);

            var first = await enroll.CreateNewAsync(new AskAlphaStagingEnrollmentRequest(
                workspace, issuer, digest, "identity_wide_enroll_first"));
            var second = await enroll.CreateNewAsync(new AskAlphaStagingEnrollmentRequest(
                workspace, issuer, digest, "identity_wide_enroll_second"));
            Assert.NotEqual(first.OwnershipId, second.OwnershipId);
            Assert.NotEqual(first.SessionId, second.SessionId);
            Assert.False(first.RealVisitorAuthenticated);
            Assert.False(second.RealVisitorAuthenticated);

            foreach (var (ownership, retry) in new[]
            {
                (first.OwnershipId, "identity_optin_first"),
                (second.OwnershipId, "identity_optin_second")
            })
            {
                var consent = await ledger.AppendAsync(new AskAlphaStagingConsentRequest(
                    ownership, workspace, issuer, digest, "OPT_IN",
                    AskAlphaVisitorStagingConsentLedger.Purpose,
                    AskAlphaVisitorStagingConsentLedger.NoticeVersion, retry));
                Assert.False(consent.AuthenticatedVisitorConsentRecorded);
            }

            var sample = Encoding.UTF8.GetBytes("FICTIONAL encrypted owner-specific snapshot.");
            var key = RandomNumberGenerator.GetBytes(32);
            var request1 = new AskAlphaStagingSnapshotRequest(
                first.OwnershipId, workspace, issuer, digest,
                AskAlphaVisitorStagingConsentLedger.Purpose,
                AskAlphaVisitorStagingConsentLedger.NoticeVersion,
                "snapshot_first_identity_wide", sample, key);
            var request2 = request1 with
            {
                OwnershipId = second.OwnershipId,
                RetryKey = "snapshot_second_identity_wide"
            };
            var s1 = await store.SaveFictionalAsync(request1);
            var s2 = await store.SaveFictionalAsync(request2);
            Assert.NotEqual(s1.SnapshotId, s2.SnapshotId);
            Assert.False(s1.ContentReadAuthorized);
            Assert.False(s2.ContentReadAuthorized);

            var withdrawal = await ledger.AppendAsync(new AskAlphaStagingConsentRequest(
                first.OwnershipId, workspace, issuer, digest, "REVOKE",
                request1.Purpose, request1.NoticeVersion, "revoke_all_prior_owners"));
            Assert.False(withdrawal.AuthenticatedVisitorConsentRecorded);
            Assert.False(withdrawal.TranscriptExportAuthorized);

            // Critical regression: the other ownership ID has no REVOKE row,
            // yet same workspace+issuer+subject MUST be terminally withdrawn.
            foreach (var r in new[] { request1, request2 })
            {
                var scope = new AskAlphaStagingSnapshotScope(
                    r.OwnershipId, r.WorkspaceId, r.Issuer,
                    r.SubjectSha256, r.Purpose, r.NoticeVersion);
                var status = await store.InspectAsync(scope);
                Assert.Equal("SYNTHETIC_WITHDRAWN_SNAPSHOT_PERMANENTLY_DENIED",
                    status.Status);
                Assert.False(status.ContentReadAuthorized);
                Assert.False(status.TranscriptExportAuthorized);

                var consent = await readback.ReadAsync(new AskAlphaStagingConsentScope(
                    r.OwnershipId, r.WorkspaceId, r.Issuer,
                    r.SubjectSha256, r.Purpose, r.NoticeVersion));
                Assert.Equal("SYNTHETIC_STAGING_WITHDRAWN_NO_EXPORT", consent.Status);
                Assert.True(consent.SyntheticWithdrawn);
                Assert.False(consent.AuthenticatedConsentRecorded);
                Assert.False(consent.TranscriptExportAuthorized);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => store.SaveFictionalAsync(r));
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => store.SaveFictionalAsync(r with
                    {
                        RetryKey = "new_retry_after_identity_revoke"
                    }));
            }

            // The DB trigger itself must refuse direct SQL writes to an
            // already-opted-in second owner, independently of the C# guard.
            await using (var verify = new NpgsqlConnection(parsed.ConnectionString))
            {
                await verify.OpenAsync();
                await using var insert = new NpgsqlCommand("""
                    INSERT INTO "AskAlphaVisitorSnapshotStaging"
                      ("Id","OwnershipId","WorkspaceId","Issuer","SubjectSha256",
                       "Purpose","NoticeVersion","RetryKey","ContentSha256",
                       "Ciphertext","Nonce","Tag","CreatedAt")
                    VALUES (@id,@owner,@ws,@issuer,@subject,@purpose,@notice,@retry,
                            @hash,@bytes,@nonce,@tag,clock_timestamp())
                    """, verify);
                insert.Parameters.AddWithValue("id", Guid.NewGuid());
                insert.Parameters.AddWithValue("owner", second.OwnershipId);
                insert.Parameters.AddWithValue("ws", workspace);
                insert.Parameters.AddWithValue("issuer", issuer);
                insert.Parameters.AddWithValue("subject", digest);
                insert.Parameters.AddWithValue("purpose", request1.Purpose);
                insert.Parameters.AddWithValue("notice", request1.NoticeVersion);
                insert.Parameters.AddWithValue("retry", "direct_sql_replay_denied");
                insert.Parameters.AddWithValue("hash", Convert.ToHexString(SHA256.HashData(sample)));
                insert.Parameters.AddWithValue("bytes", new byte[] { 1, 2, 3 });
                insert.Parameters.AddWithValue("nonce", new byte[12]);
                insert.Parameters.AddWithValue("tag", new byte[16]);
                var err = await Assert.ThrowsAsync<PostgresException>(
                    () => insert.ExecuteNonQueryAsync());
                Assert.Equal("23514", err.SqlState);
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                enroll.CreateNewAsync(new AskAlphaStagingEnrollmentRequest(
                    workspace, issuer, digest, "another_session_after_revoke")));
        }
        finally
        {
            await using var admin = new NpgsqlConnection(cs);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static string FindSql(string file)
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "Bliss.Infrastructure", "Migrations", file);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Synthetic SQL schema missing from checkout.");
    }
}
