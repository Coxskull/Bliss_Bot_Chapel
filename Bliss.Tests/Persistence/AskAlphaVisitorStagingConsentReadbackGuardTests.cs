using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Persistence;

// No database connection is opened: deny unsafe datasource targets up front.
public sealed class AskAlphaVisitorStagingConsentReadbackGuardTests
{
    [Theory]
    [InlineData("Host=prod.example.invalid;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=postgres;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=public")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,public")]
    [InlineData("Host=localhost;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Refuses_non_disposable_or_production_scope(string cs)
    {
        using var data=NpgsqlDataSource.Create(cs);
        Assert.Throws<InvalidOperationException>(()=>
            new AskAlphaVisitorStagingConsentReadback(data));
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=localhost;Database=bliss_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Only_accepts_dedicated_loopback_test_scope(string cs)
    {
        using var data=NpgsqlDataSource.Create(cs);
        Assert.NotNull(new AskAlphaVisitorStagingConsentReadback(data));
    }
}
