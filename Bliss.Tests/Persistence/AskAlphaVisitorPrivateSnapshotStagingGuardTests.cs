using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Persistence;

// Does not open a database connection. Reject production/hosted destinations.
public sealed class AskAlphaVisitorPrivateSnapshotStagingGuardTests
{
    [Theory]
    [InlineData("Host=db.production.invalid;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=postgres;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=public")]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,public")]
    [InlineData("Host=localhost;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Rejects_unsafe_database_or_schema(string cs)
    {
        using var source = NpgsqlDataSource.Create(cs);
        Assert.Throws<InvalidOperationException>(() =>
            new AskAlphaVisitorPrivateSnapshotStaging(source));
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Host=localhost;Database=bliss_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Accepts_only_isolated_loopback_disposable_schema(string cs)
    {
        using var source = NpgsqlDataSource.Create(cs);
        Assert.NotNull(new AskAlphaVisitorPrivateSnapshotStaging(source));
    }

    [Fact]
    public async Task Rejects_invalid_scope_and_encryption_key_before_connection()
    {
        using var source = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Database=planner_lineage_ci;Search Path=agm_j_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var storage = new AskAlphaVisitorPrivateSnapshotStaging(source);
        var invalid = new AskAlphaStagingSnapshotRequest(
            Guid.NewGuid(), Guid.NewGuid(), "https://idp.fixture.invalid",
            new string('A', 64), AskAlphaVisitorStagingConsentLedger.Purpose,
            AskAlphaVisitorStagingConsentLedger.NoticeVersion,
            "fixture_retry_12345", new byte[] { 1, 2, 3 }, new byte[12]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveFictionalAsync(invalid));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveFictionalAsync(invalid with { EphemeralEncryptionKey = new byte[32], FictionalContent = Array.Empty<byte>() }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveFictionalAsync(invalid with { EphemeralEncryptionKey = new byte[32], FictionalContent = new byte[65537] }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveFictionalAsync(invalid with { EphemeralEncryptionKey = new byte[32], RetryKey = "!" }));
    }
}
