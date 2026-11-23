using Bliss.Api.Runtime;

namespace Bliss.Tests.Api;

public sealed class CreatorIncrementalMigrationSafetyTests
{
    private const string Host = "bliss-production.internal";
    private const string Db = "bliss";
    private const string Evidence = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Connection = "Host=bliss-production.internal;Database=bliss;Username=synthetic;Password=synthetic;Ssl Mode=VerifyFull;Trust Server Certificate=false";
    private static readonly HostedDatabaseDiagnosticResult Ready = new(true, true, true, 99, "");

    [Fact]
    public void Creator_mode_requires_exact_two_arguments()
    {
        Assert.True(MigrationCommand.CreatorIncrementalRequested(
            ["--migrate", MigrationCommand.CreatorIncrementalArgument]));
        Assert.True(MigrationCommand.CreatorIncrementalRequested(
            [MigrationCommand.CreatorIncrementalArgument, "--migrate"]));
        Assert.False(MigrationCommand.CreatorIncrementalRequested(["--migrate"]));
        Assert.False(MigrationCommand.CreatorIncrementalRequested(
            ["--migrate", MigrationCommand.CreatorIncrementalArgument, "--initial-install-empty-hosted-db"]));
        Assert.False(MigrationCommand.CreatorIncrementalRequested(
            [MigrationCommand.CreatorIncrementalArgument]));
    }

    [Fact]
    public void Secure_populated_production_target_accepts_only_held_creator_release()
    {
        MigrationCommand.ValidateCreatorIncrementalHostedInstall(
            true, true, true, false, Connection, Ready, Host, Db, Evidence);
    }

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void Unsafe_release_or_environment_fails_closed(
        bool preview, bool production, bool advertiser, bool creator)
    {
        Assert.Throws<InvalidOperationException>(() =>
            MigrationCommand.ValidateCreatorIncrementalHostedInstall(
                preview, production, advertiser, creator, Connection, Ready, Host, Db, Evidence));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0123456789abcdef")]
    [InlineData("BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB")]
    public void Recovery_evidence_must_be_exact_lowercase_sha256(string evidence)
    {
        Assert.Throws<InvalidOperationException>(() =>
            MigrationCommand.ValidateCreatorIncrementalHostedInstall(
                true, true, true, false, Connection, Ready, Host, Db, evidence));
    }

    [Theory]
    [InlineData("wrong-host", "bliss")]
    [InlineData("bliss-production.internal", "factory")]
    public void Pinned_target_mismatch_is_refused(string host, string db)
    {
        Assert.Throws<InvalidOperationException>(() =>
            MigrationCommand.ValidateCreatorIncrementalHostedInstall(
                true, true, true, false, Connection, Ready, host, db, Evidence));
    }

    [Theory]
    [InlineData("Host=bliss-production.internal;Database=bliss;Ssl Mode=Require")]
    [InlineData("Host=bliss-production.internal;Database=factory;Ssl Mode=VerifyFull")]
    [InlineData("Host=bliss-production.internal;Database=bliss;Ssl Mode=VerifyFull;Trust Server Certificate=true")]
    public void Insecure_tls_or_other_database_fails_closed(string connection)
    {
        Assert.Throws<InvalidOperationException>(() =>
            MigrationCommand.ValidateCreatorIncrementalHostedInstall(
                true, true, true, false, connection, Ready, Host, Db, Evidence));
    }

    [Fact]
    public void Empty_or_unmigrated_catalog_is_not_an_incremental_target()
    {
        foreach (var diagnostic in new[]
        {
            new HostedDatabaseDiagnosticResult(true, true, false, 99, "migration_history_missing"),
            new HostedDatabaseDiagnosticResult(true, true, true, 0, "public_tables_missing"),
            new HostedDatabaseDiagnosticResult(true, false, true, 99, "tls_not_active")
        })
            Assert.Throws<InvalidOperationException>(() =>
                MigrationCommand.ValidateCreatorIncrementalHostedInstall(
                    true, true, true, false, Connection, diagnostic, Host, Db, Evidence));
    }
}
