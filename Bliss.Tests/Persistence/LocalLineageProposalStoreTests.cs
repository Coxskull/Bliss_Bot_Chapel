using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class LocalLineageProposalStoreTests
{
    [CampaignAuthorityPostgresFact]
    public async Task PostgreSql_proposals_are_scoped_idempotent_concurrent_and_append_only()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")!;
        var schema = "lineage_proposals_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scoped).Options;
            await using var db = new BlissDbContext(options);
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            await db.Database.ExecuteSqlRawAsync(LocalLineageProposalStore.FixtureSchemaSql);
            var selection = await LocalCampaignLineageEvidenceTests.Seed(db);
            var grant = new LocalLineageProposalGrant(new(true, "fixture-reviewer", Phase2DataSeeder.Advertiser2Id, true), true);
            var report = await new LocalCampaignLineageEvidence(db).ReadAsync(grant.Reader, selection);
            var request = new LocalLineageProposalRequest(Guid.NewGuid(), "AGM-EV-" + Guid.NewGuid().ToString("D"), selection, report.Sha256);
            var store = new LocalLineageProposalStore(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant with { ProposalWriteGranted = false }, request));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant with { Reader = grant.Reader with { Authenticated = false } }, request));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant with { Reader = grant.Reader with { EvidenceReadGranted = false } }, request));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant with { Reader = grant.Reader with { AdvertiserId = Phase1DataSeeder.AdvertiserId } }, request));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant, request with { ExpectedSha256 = new string('0', 64) }));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant, request with { EvidenceId = "AGM-EV-00000000-0000-1000-8000-000000000000" }));
            var winners = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
            {
                await using var concurrent = new BlissDbContext(options);
                return await new LocalLineageProposalStore(concurrent).ProposeAsync(grant, request);
            }));
            Assert.Single(winners.Select(x => x.Id).Distinct());
            var first = winners[0]; Assert.Equal("PENDING_REVIEW", first.Status); Assert.Equal(report.Json, first.Json);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant, request with { EvidenceId = "AGM-EV-" + Guid.NewGuid() }));
            // Source changes cannot rewrite an earlier proposal, and a new request cannot capture a stale report.
            var intake = await db.CreatorIngestionRuns.SingleAsync(x => x.Id == selection.IngestionRunId);
            intake.InputSnapshot += " "; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(first, await store.ProposeAsync(grant, request));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ProposeAsync(grant, request with { RequestId = Guid.NewGuid() }));
            var latest = await new LocalCampaignLineageEvidence(db).ReadAsync(grant.Reader, selection);
            var revision = await store.ProposeAsync(grant, request with { RequestId = Guid.NewGuid(), EvidenceId = "AGM-EV-" + Guid.NewGuid(), ExpectedSha256 = latest.Sha256 });
            Assert.NotEqual(first.Id, revision.Id); Assert.NotEqual(first.Sha256, revision.Sha256);
            foreach (var sql in new[] { "UPDATE local_lineage_proposals SET status='PENDING_REVIEW'", "DELETE FROM local_lineage_proposals", "TRUNCATE local_lineage_proposals" })
                await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql));
            await using var inspect = new NpgsqlConnection(scoped); await inspect.OpenAsync();
            await using var count = new NpgsqlCommand("SELECT count(*) FROM local_lineage_proposals", inspect);
            Assert.Equal(2L, await count.ExecuteScalarAsync());
            Assert.Equal(1, await db.CampaignPlacementRuns.CountAsync(x => x.Id == selection.PlacementRunId));
            Assert.Contains("\"paymentAuthorized\":false", first.Json);
            Assert.Contains("\"activationAuthorized\":false", first.Json);
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
