using Bliss.Api.Runtime;

namespace Bliss.Tests.Api;

// Pure guard tests: no PostgreSQL connection, migration, provider call or
// production write. The hosted operator must provide its private backup receipt.
public sealed class GuardedInitialInstallTests
{
    private const string VerifiedTarget =
        "Host=bliss-postgres.railway.internal;Port=5432;Database=bliss;"
        + "Username=bliss_migrator;Password=fictional-local-test-password;"
        + "SSL Mode=VerifyFull;Trust Server Certificate=false";

    private static readonly HostedDatabaseDiagnosticResult EmptyVerifiedCatalog =
        new(true, true, false, 0, "schema_not_initialized");

    [Fact]
    public void Guarded_install_only_accepts_exact_two_argument_opt_in()
    {
        Assert.True(MigrationCommand.Requested(new[] {
            "--migrate", "--initial-install-empty-hosted-db" }));
        Assert.True(MigrationCommand.InitialInstallRequested(new[] {
            "--migrate", "--initial-install-empty-hosted-db" }));
        Assert.True(MigrationCommand.InitialInstallRequested(new[] {
            "--initial-install-empty-hosted-db", "--migrate" }));
        Assert.False(MigrationCommand.InitialInstallRequested(new[] {
            "--initial-install-empty-hosted-db" }));
        Assert.False(MigrationCommand.InitialInstallRequested(new[] {
            "--migrate" }));
        Assert.False(MigrationCommand.InitialInstallRequested(new[] {
            "--migrate", "--initial-install-empty-hosted-db", "--skip-backup" }));
        Assert.False(MigrationCommand.InitialInstallRequested(Array.Empty<string>()));
    }

    [Fact]
    public void Guarded_install_accepts_only_recovered_identity_matched_empty_tls_host()
    {
        MigrationCommand.ValidateInitialEmptyHostedInstall(
            restrictedPreview: true, isProduction: true,
            connectionString: VerifiedTarget, diagnostic: EmptyVerifiedCatalog,
            expectedHost: "bliss-postgres.railway.internal",
            expectedDatabase: "bliss",
            backupEvidenceId: "DO-RESTORE-RECEIPT-20261120");
    }

    [Fact]
    public void Guarded_install_refuses_every_missing_isolation_or_recovery_predicate()
    {
        void Verify(bool preview = true, bool prod = true,
            string? dsn = VerifiedTarget,
            HostedDatabaseDiagnosticResult? diagnostic = null,
            string? host = "bliss-postgres.railway.internal",
            string? database = "bliss",
            string? receipt = "DO-RESTORE-RECEIPT-20261120") =>
            MigrationCommand.ValidateInitialEmptyHostedInstall(
                preview, prod, dsn, diagnostic ?? EmptyVerifiedCatalog,
                host, database, receipt);

        Assert.Throws<InvalidOperationException>(() => Verify(preview: false));
        Assert.Throws<InvalidOperationException>(() => Verify(prod: false));
        Assert.Throws<InvalidOperationException>(() => Verify(dsn: null));
        Assert.Throws<InvalidOperationException>(() => Verify(host: null));
        Assert.Throws<InvalidOperationException>(() => Verify(database: null));
        Assert.Throws<InvalidOperationException>(() => Verify(receipt: null));
        Assert.Throws<InvalidOperationException>(() => Verify(receipt: "short"));
        Assert.Throws<InvalidOperationException>(() => Verify(host: "other.railway.internal"));
        Assert.Throws<InvalidOperationException>(() => Verify(database: "production"));
        Assert.Throws<InvalidOperationException>(() => Verify(
            dsn: VerifiedTarget.Replace("SSL Mode=VerifyFull", "SSL Mode=Require",
                StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() => Verify(
            dsn: VerifiedTarget.Replace("Trust Server Certificate=false",
                "Trust Server Certificate=true", StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() => Verify(
            diagnostic: new(true, false, false, 0, "tls_not_active")));
        Assert.Throws<InvalidOperationException>(() => Verify(
            diagnostic: new(true, true, true, 10, "")));
        Assert.Throws<InvalidOperationException>(() => Verify(
            diagnostic: new(true, true, false, 1, "migration_history_missing")));
    }
}
