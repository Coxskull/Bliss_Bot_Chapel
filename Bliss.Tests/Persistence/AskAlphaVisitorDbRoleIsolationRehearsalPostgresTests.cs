using System.Security.Cryptography;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

// AGM-COMM-015: disposable CI-only role-isolated tenant proof.
// A database login is NOT an OIDC verifier or a production credential strategy.
// The permanent visitor consent endpoint and all commercial permissions stay OFF.
public sealed class AskAlphaVisitorDbRoleIsolationRehearsalPostgresTests
{
    [Fact]
    public async Task Separate_login_roles_prevent_cross_tenant_reads_and_scope_variable_spoofing()
    {
        var cs = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                throw new InvalidOperationException("Disposable planner PostgreSQL is mandatory in CI.");
            return;
        }

        var baseCs = new NpgsqlConnectionStringBuilder(cs);
        Assert.True(
            (baseCs.Host == "127.0.0.1" && baseCs.Database == "planner_lineage_ci") ||
            (baseCs.Host == "localhost" && baseCs.Database == "bliss_ci"),
            "Never create temporary tenant logins in a hosted database.");

        var suffix = Guid.NewGuid().ToString("N");
        var schema = "agm_j_" + suffix;
        var roleA = "agm_ta_" + suffix;
        var roleB = "agm_tb_" + suffix;
        var passwordA = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var passwordB = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        await using (var admin = new NpgsqlConnection(cs))
        {
            await admin.OpenAsync();
            await using var bootstrap = new NpgsqlCommand(
                $"CREATE SCHEMA \"{schema}\"; " +
                $"CREATE ROLE \"{roleA}\" LOGIN NOINHERIT PASSWORD '{passwordA}'; " +
                $"CREATE ROLE \"{roleB}\" LOGIN NOINHERIT PASSWORD '{passwordB}';", admin);
            await bootstrap.ExecuteNonQueryAsync();
        }

        try
        {
            baseCs.SearchPath = schema;
            var options = new DbContextOptionsBuilder<BlissDbContext>()
                .UseNpgsql(baseCs.ConnectionString).Options;
            var advertiserA = Guid.NewGuid();
            var advertiserB = Guid.NewGuid();
            var workspaceA = Guid.NewGuid();
            var workspaceB = Guid.NewGuid();
            var created = DateTime.UtcNow.AddMinutes(-5);
            await using (var db = new BlissDbContext(options))
            {
                await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
                db.Advertisers.AddRange(
                    new Advertiser { Id = advertiserA, Name = "Fictional role A",
                        CreatedAt = created },
                    new Advertiser { Id = advertiserB, Name = "Fictional role B",
                        CreatedAt = created });
                db.WeddingPlannerWorkspaces.AddRange(
                    new WeddingPlannerWorkspace {
                        Id = workspaceA, AdvertiserId = advertiserA, Status = "ACTIVE",
                        SourceSystem = "STAGING_ROLE_FIXTURE", CreatedAt = created,
                        UpdatedAt = created, IdempotencyKey = Guid.NewGuid().ToString("N")
                    },
                    new WeddingPlannerWorkspace {
                        Id = workspaceB, AdvertiserId = advertiserB, Status = "ACTIVE",
                        SourceSystem = "STAGING_ROLE_FIXTURE", CreatedAt = created,
                        UpdatedAt = created, IdempotencyKey = Guid.NewGuid().ToString("N")
                    });
                await db.SaveChangesAsync();
            }

            await using var data = NpgsqlDataSource.Create(baseCs.ConnectionString);
            await using (var setup = await data.OpenConnectionAsync())
            {
                await using var cmd = new NpgsqlCommand(
                    await File.ReadAllTextAsync(FindSql(
                        "AskAlphaVisitorOwnershipStagingV1.sql")), setup);
                await cmd.ExecuteNonQueryAsync();
            }

            const string issuer = "https://idp.fixture.invalid";
            var subjectA = new string('A', 64);
            var subjectB = new string('B', 64);
            var enrollment = new AskAlphaVisitorStagingEnrollment(data);
            var consent = new AskAlphaVisitorStagingConsentLedger(data);
            var a = await enrollment.CreateNewAsync(
                new AskAlphaStagingEnrollmentRequest(
                    workspaceA, issuer, subjectA, "tenant_role_enroll_alpha"));
            var b = await enrollment.CreateNewAsync(
                new AskAlphaStagingEnrollmentRequest(
                    workspaceB, issuer, subjectB, "tenant_role_enroll_bravo"));
            Assert.NotEqual(a.OwnershipId, b.OwnershipId);
            await consent.AppendAsync(new AskAlphaStagingConsentRequest(
                a.OwnershipId, workspaceA, issuer, subjectA, "OPT_IN",
                AskAlphaVisitorStagingConsentLedger.Purpose,
                AskAlphaVisitorStagingConsentLedger.NoticeVersion,
                "tenant_role_consent_alpha"));
            await consent.AppendAsync(new AskAlphaStagingConsentRequest(
                b.OwnershipId, workspaceB, issuer, subjectB, "OPT_IN",
                AskAlphaVisitorStagingConsentLedger.Purpose,
                AskAlphaVisitorStagingConsentLedger.NoticeVersion,
                "tenant_role_consent_bravo"));

            // Role-specific policies bind access to PostgreSQL's authenticated
            // database role, not caller-settable custom configuration values.
            // Literal IDs below originate only from the disposable test seed.
            await using (var admin = await data.OpenConnectionAsync())
            {
                var ddl = $"""
                    ALTER TABLE "AskAlphaVisitorOwnershipStaging" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE "AskAlphaVisitorOwnershipStaging" FORCE ROW LEVEL SECURITY;
                    ALTER TABLE "AskAlphaVisitorConsentStaging" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE "AskAlphaVisitorConsentStaging" FORCE ROW LEVEL SECURITY;
                    CREATE POLICY agm_role_owner_a ON "AskAlphaVisitorOwnershipStaging"
                      FOR SELECT TO "{roleA}"
                      USING ("WorkspaceId"='{workspaceA:D}'::uuid
                        AND "Issuer"='{issuer}'
                        AND "SubjectSha256"='{subjectA}');
                    CREATE POLICY agm_role_owner_b ON "AskAlphaVisitorOwnershipStaging"
                      FOR SELECT TO "{roleB}"
                      USING ("WorkspaceId"='{workspaceB:D}'::uuid
                        AND "Issuer"='{issuer}'
                        AND "SubjectSha256"='{subjectB}');
                    CREATE POLICY agm_role_consent_a ON "AskAlphaVisitorConsentStaging"
                      FOR SELECT TO "{roleA}"
                      USING ("WorkspaceId"='{workspaceA:D}'::uuid
                        AND "Issuer"='{issuer}'
                        AND "SubjectSha256"='{subjectA}');
                    CREATE POLICY agm_role_consent_b ON "AskAlphaVisitorConsentStaging"
                      FOR SELECT TO "{roleB}"
                      USING ("WorkspaceId"='{workspaceB:D}'::uuid
                        AND "Issuer"='{issuer}'
                        AND "SubjectSha256"='{subjectB}');
                    GRANT USAGE ON SCHEMA "{schema}" TO "{roleA}","{roleB}";
                    GRANT SELECT ON "AskAlphaVisitorOwnershipStaging",
                      "AskAlphaVisitorConsentStaging" TO "{roleA}","{roleB}";
                    """;
                await using var cmd = new NpgsqlCommand(ddl, admin);
                await cmd.ExecuteNonQueryAsync();
            }

            async Task VerifyRole(
                string role, string password, Guid ownWorkspace, Guid otherWorkspace,
                string expectedSubject, string otherSubject, string otherRole)
            {
                var login = new NpgsqlConnectionStringBuilder(baseCs.ConnectionString) {
                    Username = role, Password = password, Pooling = false
                };
                await using var user = new NpgsqlConnection(login.ConnectionString);
                await user.OpenAsync();
                async Task<long> Count(string table, Guid? workspace = null)
                {
                    await using var query = new NpgsqlCommand(
                        $"SELECT count(*) FROM \"{table}\"" +
                        (workspace is null ? "" : " WHERE \"WorkspaceId\"=@workspace"), user);
                    if (workspace is not null)
                        query.Parameters.AddWithValue("workspace", workspace.Value);
                    return (long)(await query.ExecuteScalarAsync() ?? 0L);
                }

                foreach (var table in new[] {
                    "AskAlphaVisitorOwnershipStaging", "AskAlphaVisitorConsentStaging"
                })
                {
                    Assert.Equal(1L, await Count(table));
                    Assert.Equal(1L, await Count(table, ownWorkspace));
                    Assert.Equal(0L, await Count(table, otherWorkspace));
                    foreach (var verb in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE" })
                    {
                        await using var privilege = new NpgsqlCommand(
                            "SELECT has_table_privilege(current_user, @relation, @verb)", user);
                        privilege.Parameters.AddWithValue("relation", $"\"{table}\"");
                        privilege.Parameters.AddWithValue("verb", verb);
                        Assert.False((bool)(await privilege.ExecuteScalarAsync() ?? true));
                    }
                }

                // These same SQL-session settings controlled the earlier
                // spoofable RLS candidate. Setting them to the other tenant
                // MUST NOT expand these role-bound results.
                await using (var fake = new NpgsqlCommand("""
                    SELECT set_config('agm.fixture_workspace_id',@workspace,false),
                           set_config('agm.fixture_issuer',@issuer,false),
                           set_config('agm.fixture_subject_sha256',@subject,false)
                    """, user))
                {
                    fake.Parameters.AddWithValue("workspace", otherWorkspace.ToString("D"));
                    fake.Parameters.AddWithValue("issuer", issuer);
                    fake.Parameters.AddWithValue("subject", otherSubject);
                    await fake.ExecuteNonQueryAsync();
                }
                Assert.Equal(1L, await Count("AskAlphaVisitorOwnershipStaging"));
                Assert.Equal(0L, await Count("AskAlphaVisitorOwnershipStaging", otherWorkspace));
                Assert.Equal(1L, await Count("AskAlphaVisitorConsentStaging"));

                // Each login lacks membership in the other login's role.
                // Do not test this using the admin connection: superusers may
                // switch to any role, which would be a misleading test.
                await using (var switchRole = new NpgsqlCommand(
                    $"SET ROLE \"{otherRole}\"", user))
                {
                    var denied = await Assert.ThrowsAsync<PostgresException>(
                        () => switchRole.ExecuteNonQueryAsync());
                    Assert.Equal("42501", denied.SqlState);
                }

                // Scope source claims are fixture-only, not cryptographic OIDC.
                Assert.NotEqual(expectedSubject, otherSubject);
            }

            await VerifyRole(roleA, passwordA, workspaceA, workspaceB,
                subjectA, subjectB, roleB);
            await VerifyRole(roleB, passwordB, workspaceB, workspaceA,
                subjectB, subjectA, roleA);

            // The rehearsed rollback restores the original schema metadata
            // without deleting application records, then disposes the role
            // grants by dropping the entire random test schema.
            await using (var db = await data.OpenConnectionAsync())
            {
                await using var rollback = new NpgsqlCommand("""
                    DROP POLICY agm_role_owner_a ON "AskAlphaVisitorOwnershipStaging";
                    DROP POLICY agm_role_owner_b ON "AskAlphaVisitorOwnershipStaging";
                    DROP POLICY agm_role_consent_a ON "AskAlphaVisitorConsentStaging";
                    DROP POLICY agm_role_consent_b ON "AskAlphaVisitorConsentStaging";
                    ALTER TABLE "AskAlphaVisitorOwnershipStaging" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE "AskAlphaVisitorOwnershipStaging" DISABLE ROW LEVEL SECURITY;
                    ALTER TABLE "AskAlphaVisitorConsentStaging" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE "AskAlphaVisitorConsentStaging" DISABLE ROW LEVEL SECURITY;
                    """, db);
                await rollback.ExecuteNonQueryAsync();
                foreach (var table in new[] {
                    "AskAlphaVisitorOwnershipStaging", "AskAlphaVisitorConsentStaging"
                })
                {
                    await using var count = new NpgsqlCommand(
                        $"SELECT count(*) FROM \"{table}\"", db);
                    Assert.Equal(2L, (long)(await count.ExecuteScalarAsync() ?? 0L));
                }
                await using var policies = new NpgsqlCommand("""
                    SELECT count(*) FROM pg_policies WHERE schemaname=@schema
                      AND policyname LIKE 'agm_role_%'
                    """, db);
                policies.Parameters.AddWithValue("schema", schema);
                Assert.Equal(0L, (long)(await policies.ExecuteScalarAsync() ?? 0L));
            }
        }
        finally
        {
            await using var admin = new NpgsqlConnection(cs);
            await admin.OpenAsync();
            // Drop dependent schema privileges before the ephemeral logins.
            await using var teardown = new NpgsqlCommand(
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE; " +
                $"DROP ROLE IF EXISTS \"{roleA}\"; DROP ROLE IF EXISTS \"{roleB}\";", admin);
            await teardown.ExecuteNonQueryAsync();
        }

        await using (var verify = new NpgsqlConnection(cs))
        {
            await verify.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT count(*) FROM pg_namespace WHERE nspname=@schema", verify);
            cmd.Parameters.AddWithValue("schema", schema);
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync() ?? 0L));
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
        throw new FileNotFoundException("Disposable SQL fixture was not found.");
    }
}
