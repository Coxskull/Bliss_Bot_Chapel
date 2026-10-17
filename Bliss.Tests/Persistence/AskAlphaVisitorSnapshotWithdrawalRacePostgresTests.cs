using System.Security.Cryptography;
using System.Text;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

// AGM-COMM-015: race test only. Uses a disposable, local synthetic schema.
// No production RLS, real consent, transcript read/export, IdP or hosted DDL.
public sealed class AskAlphaVisitorSnapshotWithdrawalRacePostgresTests
{
    [Fact]
    public async Task Withdrawal_racing_against_snapshot_writes_is_terminal_and_never_releases_content()
    {
        var cs = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                throw new InvalidOperationException("Isolated PostgreSQL fixture required in CI.");
            return;
        }

        var parsed = new NpgsqlConnectionStringBuilder(cs);
        Assert.True(
            (parsed.Host == "127.0.0.1" && parsed.Database == "planner_lineage_ci") ||
            (parsed.Host == "localhost" && parsed.Database == "bliss_ci"),
            "Refuse hosted PostgreSQL for withdrawal-race testing.");
        var schema = $"agm_j_{Guid.NewGuid():N}";
        await using (var create = new NpgsqlConnection(cs))
        {
            await create.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", create);
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            parsed.SearchPath = schema;
            var dbOptions = new DbContextOptionsBuilder<BlissDbContext>()
                .UseNpgsql(parsed.ConnectionString).Options;
            var advertiserId = Guid.NewGuid();
            var workspaceId = Guid.NewGuid();
            var now = DateTime.UtcNow.AddMinutes(-5);
            await using (var db = new BlissDbContext(dbOptions))
            {
                await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                db.Advertisers.Add(new Advertiser
                {
                    Id = advertiserId,
                    Name = "Fictional withdrawal race",
                    CreatedAt = now
                });
                db.WeddingPlannerWorkspaces.Add(new WeddingPlannerWorkspace
                {
                    Id = workspaceId,
                    AdvertiserId = advertiserId,
                    Status = "ACTIVE",
                    SourceSystem = "SYNTHETIC_TEST_ONLY",
                    IdempotencyKey = Guid.NewGuid().ToString("N"),
                    CreatedAt = now,
                    UpdatedAt = now
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
                    await using var install = new NpgsqlCommand(
                        await File.ReadAllTextAsync(FindSql(name)), setup);
                    await install.ExecuteNonQueryAsync();
                }
            }

            await using var data = NpgsqlDataSource.Create(parsed.ConnectionString);
            var enrollment = new AskAlphaVisitorStagingEnrollment(data);
            var ledger = new AskAlphaVisitorStagingConsentLedger(data);
            var store = new AskAlphaVisitorPrivateSnapshotStaging(data);
            const string issuer = "https://idp.fixture.invalid";
            var subject = new string('A', 64);
            var owner = await enrollment.CreateNewAsync(
                new AskAlphaStagingEnrollmentRequest(
                    workspaceId, issuer, subject, "race_enrollment_key_12345"));
            Assert.False(owner.RealVisitorAuthenticated);
            await ledger.AppendAsync(new AskAlphaStagingConsentRequest(
                owner.OwnershipId, workspaceId, issuer, subject, "OPT_IN",
                AskAlphaVisitorStagingConsentLedger.Purpose,
                AskAlphaVisitorStagingConsentLedger.NoticeVersion,
                "race_optin_retry_12345"));

            var plaintext = Encoding.UTF8.GetBytes(
                "FICTIONAL transcript created solely for disposable withdrawal race testing.");
            var key = RandomNumberGenerator.GetBytes(32);
            var write = new AskAlphaStagingSnapshotRequest(
                owner.OwnershipId, workspaceId, issuer, subject,
                AskAlphaVisitorStagingConsentLedger.Purpose,
                AskAlphaVisitorStagingConsentLedger.NoticeVersion,
                "race_snapshot_retry_12345", plaintext, key);
            var scope = new AskAlphaStagingSnapshotScope(
                write.OwnershipId, write.WorkspaceId, write.Issuer,
                write.SubjectSha256, write.Purpose, write.NoticeVersion);
            var revoke = new AskAlphaStagingConsentRequest(
                owner.OwnershipId, workspaceId, issuer, subject, "REVOKE",
                write.Purpose, write.NoticeVersion, "race_revoke_retry_12345");

            // The store, withdrawal ledger, and DB triggers use identical
            // identity and owner advisory locks in the same acquisition order.
            // Both orderings are valid: a write may commit BEFORE withdrawal,
            // or it must fail AFTER withdrawal. Neither allows a later read.
            using var start = new ManualResetEventSlim(false);
            async Task<AskAlphaStagingSnapshotDecision?> TryWrite()
            {
                await Task.Yield();
                start.Wait(TimeSpan.FromSeconds(10));
                try
                {
                    return await store.SaveFictionalAsync(write);
                }
                catch (InvalidOperationException ex)
                    when (ex.Message.Contains("withdrawal", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
            async Task<AskAlphaStagingConsentResult> Withdraw()
            {
                await Task.Yield();
                start.Wait(TimeSpan.FromSeconds(10));
                return await ledger.AppendAsync(revoke);
            }

            var writes = Enumerable.Range(0, 8).Select(_ => TryWrite()).ToArray();
            var withdrawal = Withdraw();
            start.Set();
            var results = await Task.WhenAll(writes);
            var revoked = await withdrawal;
            Assert.Equal("SYNTHETIC_STAGING_REVOCATION_RECORDED_NO_EXPORT", revoked.Status);
            Assert.False(revoked.AuthenticatedVisitorConsentRecorded);
            Assert.False(revoked.TranscriptExportAuthorized);

            var success = results.Where(x => x is not null).Select(x => x!).ToArray();
            Assert.True(success.Length <= 8);
            Assert.True(success.Select(x => x.SnapshotId).Distinct().Count() <= 1);
            Assert.All(success, x =>
            {
                Assert.False(x.ContentReadAuthorized);
                Assert.False(x.TranscriptExportAuthorized);
                Assert.False(x.CampaignDeliveryAuthorized);
                Assert.Equal("NOT_SENT", x.Delivery);
            });

            var read = await store.InspectAsync(scope);
            Assert.Contains(read.Status, new[]
            {
                "SYNTHETIC_WITHDRAWN_SNAPSHOT_PERMANENTLY_DENIED",
                "SYNTHETIC_SNAPSHOT_SCOPE_DENIED"
            });
            Assert.False(read.ContentReadAuthorized);
            Assert.False(read.TranscriptExportAuthorized);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SaveFictionalAsync(write));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.SaveFictionalAsync(write with { RetryKey = "race_after_revoke_new_key" }));
            var after = await ledger.AppendAsync(revoke);
            Assert.True(after.IsReplay);
            Assert.False(after.AuthenticatedVisitorConsentRecorded);

            // Verify actual immutable database content: at most one snapshot
            // and exactly one withdrawal. Any successful snapshot is still
            // inaccessible under the metadata-only post-revocation boundary.
            await using (var verify = new NpgsqlConnection(parsed.ConnectionString))
            {
                await verify.OpenAsync();
                await using var count = new NpgsqlCommand("""
                    SELECT
                      (SELECT count(*) FROM "AskAlphaVisitorSnapshotStaging"
                       WHERE "OwnershipId"=@owner),
                      (SELECT count(*) FROM "AskAlphaVisitorConsentStaging"
                       WHERE "OwnershipId"=@owner AND "Action"='REVOKE')
                    """, verify);
                count.Parameters.AddWithValue("owner", owner.OwnershipId);
                await using var reader = await count.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.InRange(reader.GetInt64(0), 0L, 1L);
                Assert.Equal(1L, reader.GetInt64(1));
            }
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
        throw new FileNotFoundException("Synthetic PostgreSQL SQL fixture is missing.");
    }
}
