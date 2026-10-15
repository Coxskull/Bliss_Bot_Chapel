using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class LocalLineageProposalInspectionTests
{
    [Theory]
    [InlineData("remote.example.test", "bliss_ci")]
    [InlineData("localhost", "production")]
    [InlineData("127.0.0.1", "postgres")]
    public async Task Non_fixture_targets_are_refused_before_connection(string host, string database)
    {
        await using var db = new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder { Host = host, Database = database }.ConnectionString).Options);
        var grant = new LocalLineageInspectionGrant(new(true, "reviewer", Phase2DataSeeder.Advertiser2Id, true), true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LocalLineageProposalInspection(db).InspectAsync(grant, Guid.NewGuid()));
    }

    [CampaignAuthorityPostgresFact]
    public async Task Scoped_distinct_reviewer_detects_changes_without_approving_or_mutating_evidence()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")!;
        var schema = "lineage_inspection_" + Guid.NewGuid().ToString("N");
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
            var submitter = new LocalLineageProposalGrant(new(true, "fixture-submitter", Phase2DataSeeder.Advertiser2Id, true), true);
            var report = await new LocalCampaignLineageEvidence(db).ReadAsync(submitter.Reader, selection);
            var request = new LocalLineageProposalRequest(Guid.NewGuid(), "AGM-EV-" + Guid.NewGuid(), selection, report.Sha256);
            var proposal = await new LocalLineageProposalStore(db).ProposeAsync(submitter, request);
            var reviewer = new LocalLineageInspectionGrant(submitter.Reader with { Subject = "fixture-inspector" }, true);
            var service = new LocalLineageProposalInspection(db);
            foreach (var refused in new[] { reviewer with { ReviewInspectionGranted = false },
                reviewer with { Reader = submitter.Reader },
                reviewer with { Reader = reviewer.Reader with { Authenticated = false } },
                reviewer with { Reader = reviewer.Reader with { EvidenceReadGranted = false } },
                reviewer with { Reader = reviewer.Reader with { AdvertiserId = Phase1DataSeeder.AdvertiserId } } })
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.InspectAsync(refused, proposal.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.InspectAsync(reviewer, Guid.Empty));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.InspectAsync(reviewer, Guid.NewGuid()));
            var initial = await service.InspectAsync(reviewer, proposal.Id);
            Assert.Equal("MATCHES_CAPTURED_REPORT", initial.CurrentLineageState);
            Assert.Equal(proposal.Sha256, initial.CurrentSha256); Assert.True(initial.EvidenceIntegrityVerified);
            Assert.Equal(submitter.Reader.Subject, initial.SubmittedBy); Assert.Equal(reviewer.Reader.Subject, initial.InspectedBy);
            Assert.False(initial.IndependentQaAccepted); Assert.False(initial.ConsentVerified); Assert.False(initial.CreativeRightsVerified);
            Assert.False(initial.PaymentAuthorized); Assert.False(initial.ActivationAuthorized);
            Assert.Equal(initial, await service.InspectAsync(reviewer, proposal.Id)); Assert.Empty(db.ChangeTracker.Entries());
            var intake = await db.CreatorIngestionRuns.SingleAsync(x => x.Id == selection.IngestionRunId);
            intake.InputSnapshot += " "; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var changed = await service.InspectAsync(reviewer, proposal.Id);
            Assert.Equal("CHANGED_SINCE_CAPTURE", changed.CurrentLineageState); Assert.NotEqual(proposal.Sha256, changed.CurrentSha256);
            Assert.Equal(proposal.Sha256, changed.FrozenSha256);
            db.DataProvenances.RemoveRange(await db.DataProvenances.Where(x => x.EntityId == intake.CreatorId).ToListAsync());
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var unavailable = await service.InspectAsync(reviewer, proposal.Id);
            Assert.Equal("CURRENT_LINEAGE_UNAVAILABLE", unavailable.CurrentLineageState); Assert.Null(unavailable.CurrentSha256);
            Assert.True(unavailable.EvidenceIntegrityVerified); Assert.False(unavailable.IndependentQaAccepted);
            Assert.Equal(proposal, await new LocalLineageProposalStore(db).ProposeAsync(submitter, request));
            // Simulate malformed direct inserts in the disposable owner-controlled schema: hash/fingerprint checks refuse both.
            await using var raw = new NpgsqlConnection(scoped); await raw.OpenAsync();
            foreach (var field in new[] { "report_sha256", "request_fingerprint" })
            {
                var badId = Guid.NewGuid();
                var fingerprintExpression = field == "request_fingerprint" ? "repeat('0',64)" : "request_fingerprint";
                var shaExpression = field == "report_sha256" ? "repeat('0',64)" : "report_sha256";
                await using var insert = new NpgsqlCommand($"""
                    INSERT INTO local_lineage_proposals(id,advertiser_id,actor_subject,request_id,evidence_id,request_fingerprint,report_json,report_sha256)
                    SELECT @bad,advertiser_id,actor_subject,@request,evidence_id,{fingerprintExpression},report_json,{shaExpression}
                    FROM local_lineage_proposals WHERE id=@original
                    """, raw);
                insert.Parameters.AddWithValue("bad", badId); insert.Parameters.AddWithValue("original", proposal.Id);
                insert.Parameters.AddWithValue("request", Guid.NewGuid());
                await insert.ExecuteNonQueryAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.InspectAsync(reviewer, badId));
            }
            Assert.Equal(1, await db.CampaignPlacementRuns.CountAsync(x => x.Id == selection.PlacementRunId));
            Assert.Empty(db.ChangeTracker.Entries());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
