using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Persistence;

// No PostgreSQL server is opened: constructor guards must reject unsafe
// targets before the staging writer can attempt any operation.
public sealed class AskAlphaVisitorStagingConsentLedgerGuardTests
{
    [Theory]
    [InlineData("Host=db.production.invalid;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=postgres;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=public")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,public")]
    [InlineData("Host=localhost;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Consent_staging_ledger_rejects_non_disposable_database_or_schema(string cs)
    {
        using var source=NpgsqlDataSource.Create(cs);
        Assert.Throws<InvalidOperationException>(()=>
            new AskAlphaVisitorStagingConsentLedger(source));
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=localhost;Database=bliss_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Consent_staging_ledger_accepts_only_explicit_isolated_test_scope(string cs)
    {
        using var source=NpgsqlDataSource.Create(cs);
        Assert.NotNull(new AskAlphaVisitorStagingConsentLedger(source));
    }
}
