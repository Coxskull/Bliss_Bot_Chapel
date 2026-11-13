using Bliss.Api.Financials;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class BlissStripeSandboxReceiptStoreTests
{
    [Fact]
    public async Task Staging_postgres_journal_is_durable_deduplicated_and_append_only()
    {
        // CI uses a disposable localhost database. Never run this test against hosted data.
        var fixture = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(fixture)) return;
        var target = new NpgsqlConnectionStringBuilder(fixture);
        Assert.Equal("localhost", target.Host);
        Assert.Equal("bliss_ci", target.Database);

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BlissBotChapel.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var script = await File.ReadAllTextAsync(Path.Combine(directory!.FullName,
            "Bliss.Infrastructure", "Migrations", "BlissStripeSandboxJournalStagingV1.sql"));
        await using (var admin = new NpgsqlConnection(fixture))
        {
            await admin.OpenAsync();
            await using var migration = new NpgsqlCommand(script, admin);
            await migration.ExecuteNonQueryAsync();
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BlissStripeWebhook:JournalConnectionString"] = fixture,
            ["BlissStripeWebhook:ExpectedJournalDatabase"] = "bliss_ci"
        }).Build();
        var store = new BlissStripeSandboxReceiptStore(config);
        var id = "evt_" + Guid.NewGuid().ToString("N");
        var verified = new BlissStripeVerifiedEvent(id, "checkout.session.expired",
            "cs_test_fixture", new string('A', 64), "NO_FULFILLMENT");
        Assert.True(await store.Record(verified, CancellationToken.None));
        Assert.False(await store.Record(verified, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.Record(verified with { PayloadSha256 = new string('B', 64) }, CancellationToken.None));

        await using var connection = new NpgsqlConnection(fixture);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM bliss_stripe_sandbox.event_receipts WHERE event_id=@event", connection);
        count.Parameters.AddWithValue("event", id);
        Assert.Equal(1L, await count.ExecuteScalarAsync());

        await using var tamper = new NpgsqlCommand(
            "UPDATE bliss_stripe_sandbox.event_receipts SET disposition='REVIEW_ONLY' WHERE event_id=@event",
            connection);
        tamper.Parameters.AddWithValue("event", id);
        await Assert.ThrowsAsync<PostgresException>(() => tamper.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Journal_target_must_match_explicit_staging_database()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BlissStripeWebhook:JournalConnectionString"] =
                "Host=localhost;Database=bliss_ci;Username=postgres;Password=synthetic",
            ["BlissStripeWebhook:ExpectedJournalDatabase"] = "different_database"
        }).Build();
        var store = new BlissStripeSandboxReceiptStore(config);
        var e = new BlissStripeVerifiedEvent("evt_a", "checkout.session.expired",
            "cs_test_a", new string('A', 64), "NO_FULFILLMENT");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Record(e, CancellationToken.None));
    }
    [Theory]
    [InlineData("Host=db.example.neon.tech;Database=bliss_stripe_sandbox;Username=neondb_owner;Ssl Mode=VerifyFull", "bliss_stripe_sandbox")]
    [InlineData("Host=db.example.neon.tech;Database=bliss_stripe_sandbox;Username=bliss_stripe_ingest;Ssl Mode=Require", "bliss_stripe_sandbox")]
    [InlineData("Host=db.example.neon.tech;Database=production;Username=bliss_stripe_ingest;Ssl Mode=VerifyFull", "production")]
    [InlineData("Host=127.0.0.1;Database=bliss_stripe_sandbox;Username=bliss_stripe_ingest;Ssl Mode=VerifyFull", "bliss_stripe_sandbox")]
    public async Task Hosted_journal_rejects_wrong_role_weak_tls_or_nonstaging_target(
        string connection, string expectedDatabase)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BlissStripeWebhook:JournalConnectionString"] = connection,
            ["BlissStripeWebhook:ExpectedJournalDatabase"] = expectedDatabase
        }).Build();
        var store = new BlissStripeSandboxReceiptStore(config);
        var e = new BlissStripeVerifiedEvent("evt_stage_fixture", "checkout.session.expired",
            "cs_test_fixture", new string('A', 64), "NO_FULFILLMENT");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Record(e, CancellationToken.None));
    }

}
