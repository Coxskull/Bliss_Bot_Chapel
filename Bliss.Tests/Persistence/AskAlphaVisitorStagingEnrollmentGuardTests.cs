using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Persistence;

// No network calls: constructor guards execute before any connection opens.
public sealed class AskAlphaVisitorStagingEnrollmentGuardTests
{
    [Theory]
    [InlineData("Host=production.example.invalid;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=postgres;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=public")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,public")]
    [InlineData("Host=localhost;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Production_database_or_unscoped_schema_is_refused(string connectionString)
    {
        using var source=NpgsqlDataSource.Create(connectionString);
        Assert.Throws<InvalidOperationException>(()=>
            new AskAlphaVisitorStagingEnrollment(source));
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=localhost;Database=bliss_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Only_explicit_disposable_test_scopes_can_construct_fixture(string connectionString)
    {
        using var source=NpgsqlDataSource.Create(connectionString);
        Assert.NotNull(new AskAlphaVisitorStagingEnrollment(source));
    }
}
