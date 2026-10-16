using System.Security.Cryptography;
using System.Text;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

// ONE disposable local PostgreSQL schema. No real user, OIDC provider,
// historical transcript, consent agreement, export API or hosted DDL.
public sealed class AskAlphaVisitorPrivateSnapshotStagingPostgresTests
{
    [Fact]
    public async Task Fictional_enrollment_opt_in_private_snapshot_then_terminal_revocation()
    {
        var cs = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                throw new InvalidOperationException("Isolated planner PostgreSQL missing.");
            return;
        }
        var parsed = new NpgsqlConnectionStringBuilder(cs);
        Assert.True((parsed.Host == "127.0.0.1" && parsed.Database == "planner_lineage_ci") ||
                    (parsed.Host == "localhost" && parsed.Database == "bliss_ci"),
            "Only dedicated loopback PostgreSQL fixture databases are allowed.");
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
            var dbOptions = new DbContextOptionsBuilder<BlissDbContext>()
                .UseNpgsql(parsed.ConnectionString).Options;
            var advertiser = Guid.NewGuid();
            var workspace = Guid.NewGuid();
            var created = DateTime.UtcNow.AddMinutes(-5);
            await using (var db = new BlissDbContext(dbOptions))
            {
                await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                db.Advertisers.Add(new Advertiser
                {
                    Id = advertiser, Name = "Fictional consent scope", CreatedAt = created
                });
                db.WeddingPlannerWorkspaces.Add(new WeddingPlannerWorkspace
                {
                    Id = workspace, AdvertiserId = advertiser, Status = "ACTIVE",
                    SourceSystem = "SYNTHETIC_TEST_ONLY",
                    IdempotencyKey = Guid.NewGuid().ToString("N"),
                    CreatedAt = created, UpdatedAt = created
                });
                await db.SaveChangesAsync();
            }

            await using (var setup = new NpgsqlConnection(parsed.ConnectionString))
            {
                await setup.OpenAsync();
                foreach (var file in new[]
                {
                    "AskAlphaVisitorOwnershipStagingV1.sql",
                    "AskAlphaVisitorPrivateSnapshotStagingV1.sql"
                })
                {
                    await using var install = new NpgsqlCommand(
                        await File.ReadAllTextAsync(FindSql(file)), setup);
                    await install.ExecuteNonQueryAsync();
                }
            }

            await using var data = NpgsqlDataSource.Create(parsed.ConnectionString);
            var enroll = new AskAlphaVisitorStagingEnrollment(data);
            var consent = new AskAlphaVisitorStagingConsentLedger(data);
            var snapshots = new AskAlphaVisitorPrivateSnapshotStaging(data);
            const string issuer = "https://idp.fixture.invalid";
            var digest = new string('A', 64);
            var newSession = await enroll.CreateNewAsync(
                new AskAlphaStagingEnrollmentRequest(
                    workspace, issuer, digest, "snapshot_enroll_retry_12345"));
            Assert.NotEqual(Guid.Empty, newSession.SessionId);
            Assert.NotEqual(Guid.Empty, newSession.OwnershipId);
            Assert.False(newSession.RealVisitorAuthenticated);
            Assert.False(newSession.TranscriptExportAuthorized);

            var plain = Encoding.UTF8.GetBytes(
                "FICTIONAL ONLY: sample words created in isolated tests, no visitor conversation.");
            var key = RandomNumberGenerator.GetBytes(32);
            var request = new AskAlphaStagingSnapshotRequest(
                newSession.OwnershipId, workspace, issuer, digest,
                AskAlphaVisitorStagingConsentLedger.Purpose,
                AskAlphaVisitorStagingConsentLedger.NoticeVersion,
                "snapshot_retry_12345", plain, key);
            var scope = new AskAlphaStagingSnapshotScope(
                request.OwnershipId, request.WorkspaceId, request.Issuer,
                request.SubjectSha256, request.Purpose, request.NoticeVersion);

            // Even an enrolled synthetic owner cannot store content before
            // a separate exact-scope OPT_IN is recorded in the database.
            await Assert.ThrowsAsync<PostgresException>(() =>
                snapshots.SaveFictionalAsync(request));
            Assert.Equal("SYNTHETIC_SNAPSHOT_SCOPE_DENIED",
                (await snapshots.InspectAsync(scope)).Status);

            var optin = await consent.AppendAsync(new AskAlphaStagingConsentRequest(
                request.OwnershipId, workspace, issuer, digest, "OPT_IN",
                request.Purpose, request.NoticeVersion, "optin_retry_12345"));
            Assert.False(optin.AuthenticatedVisitorConsentRecorded);
            var concurrent = await Task.WhenAll(Enumerable.Range(0, 6)
                .Select(_ => snapshots.SaveFictionalAsync(request)));
            Assert.Single(concurrent.Select(x => x.SnapshotId).Distinct());
            Assert.Equal(1, concurrent.Count(x => !x.IsReplay));
            Assert.Equal(5, concurrent.Count(x => x.IsReplay));
            Assert.All(concurrent, x =>
            {
                Assert.False(x.RealVisitorAuthenticated);
                Assert.False(x.AuthenticatedConsentRecorded);
                Assert.False(x.ContentReadAuthorized);
                Assert.False(x.TranscriptExportAuthorized);
                Assert.False(x.PaymentAuthorized);
                Assert.False(x.CampaignDeliveryAuthorized);
                Assert.Equal("NOT_SENT", x.Delivery);
            });

            var stored = await snapshots.InspectAsync(scope);
            Assert.Equal("SYNTHETIC_ENCRYPTED_SNAPSHOT_PRESENT_NO_READ", stored.Status);
            Assert.Equal(concurrent[0].SnapshotId, stored.SnapshotId);
            Assert.False(stored.ContentReadAuthorized);

            // No plaintext or encryption key is stored by the service.
            await using (var verify = new NpgsqlConnection(parsed.ConnectionString))
            {
                await verify.OpenAsync();
                await using var command = new NpgsqlCommand("""
                    SELECT "Ciphertext","Nonce","Tag","ContentSha256"
                    FROM "AskAlphaVisitorSnapshotStaging" WHERE "Id"=@id
                    """, verify);
                command.Parameters.AddWithValue("id", stored.SnapshotId);
                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.False(plain.SequenceEqual(reader.GetFieldValue<byte[]>(0)));
                Assert.Equal(12, reader.GetFieldValue<byte[]>(1).Length);
                Assert.Equal(16, reader.GetFieldValue<byte[]>(2).Length);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(plain)),
                    reader.GetString(3).Trim());
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                snapshots.SaveFictionalAsync(request with
                {
                    FictionalContent = Encoding.UTF8.GetBytes("DIFFERENT FICTIONAL CONTENT")
                }));
            Assert.Equal("SYNTHETIC_SNAPSHOT_SCOPE_DENIED",
                (await snapshots.InspectAsync(scope with
                {
                    WorkspaceId = Guid.NewGuid()
                })).Status);
            await Assert.ThrowsAsync<PostgresException>(() =>
                snapshots.SaveFictionalAsync(request with
                {
                    OwnershipId = Guid.NewGuid(),
                    RetryKey = "foreign_owner_retry_123"
                }));

            var revoke = await consent.AppendAsync(new AskAlphaStagingConsentRequest(
                request.OwnershipId, workspace, issuer, digest, "REVOKE",
                request.Purpose, request.NoticeVersion, "withdraw_retry_12345"));
            Assert.False(revoke.TranscriptExportAuthorized);
            var withdrawn = await snapshots.InspectAsync(scope);
            Assert.Equal("SYNTHETIC_WITHDRAWN_SNAPSHOT_PERMANENTLY_DENIED",
                withdrawn.Status);
            Assert.False(withdrawn.ContentReadAuthorized);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                snapshots.SaveFictionalAsync(request));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                snapshots.SaveFictionalAsync(request with
                {
                    RetryKey = "post_revoke_retry_123"
                }));

            // A private snapshot and its scope can never be edited/deleted by
            // ordinary test writers; the disposable schema is dropped below.
            await using (var check = new NpgsqlConnection(parsed.ConnectionString))
            {
                await check.OpenAsync();
                foreach (var statement in new[]
                {
                    "UPDATE \"AskAlphaVisitorSnapshotStaging\" SET \"RetryKey\"='tampered'",
                    "DELETE FROM \"AskAlphaVisitorSnapshotStaging\"",
                    "TRUNCATE \"AskAlphaVisitorSnapshotStaging\""
                })
                {
                    await using var command = new NpgsqlCommand(statement, check);
                    var ex = await Assert.ThrowsAsync<PostgresException>(
                        () => command.ExecuteNonQueryAsync());
                    Assert.Equal("23514", ex.SqlState);
                }
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

        await using (var verifyCleanup = new NpgsqlConnection(cs))
        {
            await verifyCleanup.OpenAsync();
            await using var query = new NpgsqlCommand(
                "SELECT count(*) FROM pg_namespace WHERE nspname=@schema", verifyCleanup);
            query.Parameters.AddWithValue("schema", schema);
            Assert.Equal(0L, await query.ExecuteScalarAsync());
        }
    }

    private static string FindSql(string filename)
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName,
                "Bliss.Infrastructure", "Migrations", filename);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Required isolated staging schema is missing.");
    }
}
