using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

// AGM-COMM-015: temporary PostgreSQL role + schema, NEVER production.
// Custom SQL settings in the policy are caller-mutable. This tests the
// database's RLS isolation mechanics, NOT real authenticated tenant context.
public sealed class AskAlphaVisitorOwnershipRlsRehearsalPostgresTests
{
    [Fact]
    public async Task Restricted_fixture_reader_is_scoped_and_rollback_restores_schema()
    {
        var cs = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                throw new InvalidOperationException("Isolated planner PostgreSQL required.");
            return;
        }
        var connection = new NpgsqlConnectionStringBuilder(cs);
        Assert.True(
            (connection.Host == "127.0.0.1" && connection.Database == "planner_lineage_ci") ||
            (connection.Host == "localhost" && connection.Database == "bliss_ci"),
            "No hosted database can execute this candidate RLS rehearsal.");

        var schema = $"agm_j_{Guid.NewGuid():N}";
        var readerRole = $"agm_j_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(cs))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand(
                $"CREATE ROLE \"{readerRole}\" NOLOGIN; CREATE SCHEMA \"{schema}\";", admin);
            await create.ExecuteNonQueryAsync();
        }
        try
        {
            connection.SearchPath = schema;
            var efOptions = new DbContextOptionsBuilder<BlissDbContext>()
                .UseNpgsql(connection.ConnectionString).Options;
            var advertiser = Guid.NewGuid();
            var workspaceA = Guid.NewGuid();
            var workspaceB = Guid.NewGuid();
            var now = DateTime.UtcNow.AddMinutes(-5);
            await using (var ef = new BlissDbContext(efOptions))
            {
                await ef.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                ef.Advertisers.Add(new Advertiser
                {
                    Id = advertiser, Name = "Fictional two-tenant RLS study", CreatedAt = now
                });
                foreach (var workspace in new[] { workspaceA, workspaceB })
                    ef.WeddingPlannerWorkspaces.Add(new WeddingPlannerWorkspace
                    {
                        Id = workspace, AdvertiserId = advertiser, Status = "ACTIVE",
                        SourceSystem = "SYNTHETIC_TEST_ONLY",
                        IdempotencyKey = Guid.NewGuid().ToString("N"),
                        CreatedAt = now, UpdatedAt = now
                    });
                await ef.SaveChangesAsync();
            }

            await using var data = NpgsqlDataSource.Create(connection.ConnectionString);
            await using (var setup = await data.OpenConnectionAsync())
            {
                await using var install = new NpgsqlCommand(
                    await File.ReadAllTextAsync(FindSql("AskAlphaVisitorOwnershipStagingV1.sql")),
                    setup);
                await install.ExecuteNonQueryAsync();
            }

            var enrollment = new AskAlphaVisitorStagingEnrollment(data);
            var consent = new AskAlphaVisitorStagingConsentLedger(data);
            const string issuer = "https://idp.fixture.invalid";
            var digestA = new string('A', 64);
            var digestB = new string('B', 64);
            var ownedA = await enrollment.CreateNewAsync(
                new AskAlphaStagingEnrollmentRequest(
                    workspaceA, issuer, digestA, "fixture_rls_enroll_alpha"));
            var ownedB = await enrollment.CreateNewAsync(
                new AskAlphaStagingEnrollmentRequest(
                    workspaceB, issuer, digestB, "fixture_rls_enroll_bravo"));
            Assert.NotEqual(ownedA.OwnershipId, ownedB.OwnershipId);
            foreach (var record in new[] {
                (ownedA.OwnershipId, workspaceA, digestA, "fixture_rls_optin_alpha"),
                (ownedB.OwnershipId, workspaceB, digestB, "fixture_rls_optin_bravo")
            })
            {
                var received = await consent.AppendAsync(new AskAlphaStagingConsentRequest(
                    record.OwnershipId, record.Item2, issuer, record.Item3, "OPT_IN",
                    AskAlphaVisitorStagingConsentLedger.Purpose,
                    AskAlphaVisitorStagingConsentLedger.NoticeVersion, record.Item4));
                Assert.False(received.AuthenticatedVisitorConsentRecorded);
                Assert.False(received.TranscriptExportAuthorized);
            }

            // The candidate is installed only AFTER fixture seeding.
            // No actual production EF model migration or hosted role grants.
            await using (var admin = await data.OpenConnectionAsync())
            {
                await using var install = new NpgsqlCommand(
                    await File.ReadAllTextAsync(FindSql(
                        "AskAlphaVisitorOwnershipRlsRehearsalV1.sql")), admin);
                await install.ExecuteNonQueryAsync();
                await using var grants = new NpgsqlCommand(
                    $"GRANT USAGE ON SCHEMA \"{schema}\" TO \"{readerRole}\";" +
                    $" GRANT SELECT ON \"AskAlphaVisitorOwnershipStaging\", " +
                    $"\"AskAlphaVisitorConsentStaging\" TO \"{readerRole}\";", admin);
                await grants.ExecuteNonQueryAsync();
            }

            await using (var sql = await data.OpenConnectionAsync())
            await using (var tx = await sql.BeginTransactionAsync())
            {
                // Session-local role and fake tenant scope are NEVER
                // accepted as an OIDC or production identity proof.
                await using var setRole = new NpgsqlCommand(
                    $"SET LOCAL ROLE \"{readerRole}\"", sql, tx);
                await setRole.ExecuteNonQueryAsync();

                async Task<long> Count(string table)
                {
                    await using var query = new NpgsqlCommand(
                        $"SELECT count(*) FROM \"{table}\"", sql, tx);
                    return (long)(await query.ExecuteScalarAsync() ?? 0L);
                }
                async Task Bind(Guid workspace, string boundIssuer, string subject)
                {
                    await using var set = new NpgsqlCommand("""
                        SELECT set_config('agm.fixture_workspace_id', @ws, true),
                               set_config('agm.fixture_issuer', @issuer, true),
                               set_config('agm.fixture_subject_sha256', @subject, true)
                        """, sql, tx);
                    set.Parameters.AddWithValue("ws", workspace.ToString("D"));
                    set.Parameters.AddWithValue("issuer", boundIssuer);
                    set.Parameters.AddWithValue("subject", subject);
                    await set.ExecuteNonQueryAsync();
                }

                // With no trusted scope, both tables deny every row.
                Assert.Equal(0L, await Count("AskAlphaVisitorOwnershipStaging"));
                Assert.Equal(0L, await Count("AskAlphaVisitorConsentStaging"));

                await Bind(workspaceA, issuer, digestA);
                Assert.Equal(1L, await Count("AskAlphaVisitorOwnershipStaging"));
                Assert.Equal(1L, await Count("AskAlphaVisitorConsentStaging"));

                await Bind(workspaceA, issuer, digestB);
                Assert.Equal(0L, await Count("AskAlphaVisitorOwnershipStaging"));
                Assert.Equal(0L, await Count("AskAlphaVisitorConsentStaging"));

                await Bind(workspaceB, issuer, digestB);
                Assert.Equal(1L, await Count("AskAlphaVisitorOwnershipStaging"));
                Assert.Equal(1L, await Count("AskAlphaVisitorConsentStaging"));

                await Bind(workspaceB, "https://foreign.fixture.invalid", digestB);
                Assert.Equal(0L, await Count("AskAlphaVisitorOwnershipStaging"));
                Assert.Equal(0L, await Count("AskAlphaVisitorConsentStaging"));

                // No DML privileges granted to a reader under this contract.
                foreach (var table in new[] {
                    "AskAlphaVisitorOwnershipStaging", "AskAlphaVisitorConsentStaging"
                })
                foreach (var operation in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE" })
                {
                    await using var privilege = new NpgsqlCommand(
                        "SELECT has_table_privilege(current_user, @table, @operation)", sql, tx);
                    privilege.Parameters.AddWithValue("table", table);
                    privilege.Parameters.AddWithValue("operation", operation);
                    Assert.False((bool)(await privilege.ExecuteScalarAsync() ?? true));
                }
                await tx.RollbackAsync();
            }

            // Revert policy and extra indexes without deleting historical
            // test fixture ownership/consent records. This is NOT a real
            // production migration downgrade or RLS owner acceptance.
            await using (var admin = await data.OpenConnectionAsync())
            {
                await using var rollback = new NpgsqlCommand(
                    await File.ReadAllTextAsync(FindSql(
                        "AskAlphaVisitorOwnershipRlsRehearsalRollbackV1.sql")), admin);
                await rollback.ExecuteNonQueryAsync();
                await using var inspect = new NpgsqlCommand("""
                    SELECT (
                      SELECT count(*) FROM pg_policies
                      WHERE schemaname=@schema AND policyname LIKE 'agm_fixture_%'
                    ),(
                      SELECT count(*) FROM pg_class c
                      JOIN pg_namespace n ON n.oid=c.relnamespace
                      WHERE n.nspname=@schema AND
                        c.relname IN ('AskAlphaVisitorOwnershipStaging',
                                      'AskAlphaVisitorConsentStaging')
                        AND (c.relrowsecurity OR c.relforcerowsecurity)
                    ),(
                      SELECT count(*) FROM pg_indexes
                      WHERE schemaname=@schema AND indexname LIKE 'agm_fixture_%'
                    )
                    """, admin);
                inspect.Parameters.AddWithValue("schema", schema);
                await using var result = await inspect.ExecuteReaderAsync();
                Assert.True(await result.ReadAsync());
                Assert.Equal(0L, result.GetInt64(0));
                Assert.Equal(0L, result.GetInt64(1));
                Assert.Equal(0L, result.GetInt64(2));
            }
            await using (var check = new BlissDbContext(efOptions))
            {
                // Rollback changes only the trial RLS policies; no rows lost.
                Assert.Equal(2, await check.WeddingPlannerWorkspaces.CountAsync());
                Assert.Equal(2, await check.WeddingPlannerPlanningSessions.CountAsync());
            }
        }
        finally
        {
            await using var admin = new NpgsqlConnection(cs);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;" +
                $" DROP ROLE IF EXISTS \"{readerRole}\";", admin);
            await drop.ExecuteNonQueryAsync();
        }
        await using (var cleanup = new NpgsqlConnection(cs))
        {
            await cleanup.OpenAsync();
            await using var verify = new NpgsqlCommand(
                "SELECT count(*) FROM pg_namespace WHERE nspname=@schema", cleanup);
            verify.Parameters.AddWithValue("schema", schema);
            Assert.Equal(0L, await verify.ExecuteScalarAsync());
        }
    }

    private static string FindSql(string filename)
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(
                dir.FullName, "Bliss.Infrastructure", "Migrations", filename);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException(
            "Isolated RLS review SQL missing from the repository checkout.");
    }
}
