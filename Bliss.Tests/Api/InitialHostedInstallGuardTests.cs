using Bliss.Api.Runtime;

namespace Bliss.Tests.Api;

public sealed class InitialHostedInstallGuardTests
{
    private const string Target =
        "Host=postgres.railway.internal;Port=5432;Database=railway;Username=app;Password=synthetic-test-secret;SSL Mode=VerifyFull";
    private static readonly HostedDatabaseDiagnosticResult Empty = new(
        Connected: true, TlsVerified: true, MigrationHistoryPresent: false,
        PublicTableCount: 0, ErrorCode: "");

    [Fact]
    public void Explicit_initialization_requires_exactly_two_flags()
    {
        Assert.True(MigrationCommand.InitialInstallRequested(
            ["--migrate", "--initial-install-empty-hosted-db"]));
        Assert.True(MigrationCommand.InitialInstallRequested(
            ["--initial-install-empty-hosted-db", "--migrate"]));
        Assert.False(MigrationCommand.InitialInstallRequested(["--migrate"]));
        Assert.False(MigrationCommand.InitialInstallRequested(["--initial-install-empty-hosted-db"]));
        Assert.False(MigrationCommand.InitialInstallRequested(
            ["--migrate", "--initial-install-empty-hosted-db", "--extra"]));
    }

    [Fact]
    public void Verified_empty_target_with_separate_backup_evidence_is_eligible_for_one_time_initialization()
    {
        MigrationCommand.ValidateInitialEmptyHostedInstall(
            true, true, Target, Empty, "postgres.railway.internal", "railway", "BACKUP-VERIFIED-20261019");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Initial_install_requires_production_preview(bool restricted, bool production)
    {
        Assert.Throws<InvalidOperationException>(() => MigrationCommand.ValidateInitialEmptyHostedInstall(
            restricted, production, Target, Empty, "postgres.railway.internal", "railway", "BACKUP-VERIFIED-20261019"));
    }

    [Theory]
    [InlineData(false, true, false, 0)]
    [InlineData(true, false, false, 0)]
    [InlineData(true, true, true, 0)]
    [InlineData(true, true, false, 1)]
    public void Initial_install_refuses_unsafe_database_state(bool connected, bool tls, bool history, long tables)
    {
        var diagnostic = new HostedDatabaseDiagnosticResult(connected, tls, history, tables, "");
        Assert.Throws<InvalidOperationException>(() => MigrationCommand.ValidateInitialEmptyHostedInstall(
            true, true, Target, diagnostic, "postgres.railway.internal", "railway", "BACKUP-VERIFIED-20261019"));
    }

    [Theory]
    [InlineData(null, "railway", "BACKUP-VERIFIED-20261019")]
    [InlineData("other.railway.internal", "railway", "BACKUP-VERIFIED-20261019")]
    [InlineData("postgres.railway.internal", null, "BACKUP-VERIFIED-20261019")]
    [InlineData("postgres.railway.internal", "unrelated", "BACKUP-VERIFIED-20261019")]
    [InlineData("postgres.railway.internal", "railway", null)]
    [InlineData("postgres.railway.internal", "railway", "fake")]
    public void Initial_install_requires_explicit_target_and_backup_evidence(
        string? host, string? database, string? backup)
    {
        Assert.Throws<InvalidOperationException>(() => MigrationCommand.ValidateInitialEmptyHostedInstall(
            true, true, Target, Empty, host, database, backup));
    }

    [Theory]
    [InlineData("Require")]
    [InlineData("VerifyCA")]
    [InlineData("Disable")]
    public void Initial_install_rejects_non_verified_tls_modes(string sslMode)
    {
        var c = Target.Replace("VerifyFull", sslMode, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => MigrationCommand.ValidateInitialEmptyHostedInstall(
            true, true, c, Empty, "postgres.railway.internal", "railway", "BACKUP-VERIFIED-20261019"));
    }

    [Fact]
    public void Failure_never_contains_connection_secret()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MigrationCommand.ValidateInitialEmptyHostedInstall(
            true, true, Target, Empty, "wrong.railway.internal", "railway", "BACKUP-VERIFIED-20261019"));
        Assert.DoesNotContain("synthetic-test-secret", exception.Message);
        Assert.DoesNotContain("postgres.railway.internal", exception.Message);
    }
}
