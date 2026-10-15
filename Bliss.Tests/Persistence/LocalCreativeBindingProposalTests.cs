using System.Text.Json;
using Bliss.Api.Operations;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class LocalCreativeBindingProposalTests(BlissApiFactory factory) : IClassFixture<BlissApiFactory>
{
    [CampaignAuthorityPostgresFact]
    public async Task Actual_candidate_bytes_and_version_are_scoped_without_claiming_assignment_or_rights()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")!;
        var schema = "creative_binding_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
            await using var db = new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scoped).Options);
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            await db.Database.ExecuteSqlRawAsync(LocalLineageProposalStore.FixtureSchemaSql);
            var lineage = await LocalCampaignLineageEvidenceTests.Seed(db);
            var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
            var creative = await CreativeHandoffTestData.Seed(db, environment); db.ChangeTracker.Clear();
            var author = new LocalLineageProposalGrant(new(true, "fixture-author", Phase2DataSeeder.Advertiser2Id, true), true);
            var captured = await new LocalCampaignLineageEvidence(db).ReadAsync(author.Reader, lineage);
            var proposal = await new LocalLineageProposalStore(db).ProposeAsync(author, new(Guid.NewGuid(), "AGM-EV-" + Guid.NewGuid(), lineage, captured.Sha256));
            var grant = new LocalLineageInspectionGrant(author.Reader with { Subject = "fixture-inspector" }, true);
            var candidate = await db.AcademyProductionCandidates.AsNoTracking().SingleAsync(x => x.Id == creative.Candidate);
            var selected = new LocalCreativeSelection(creative.Workspace.WorkspaceId, candidate.Id, candidate.Version, candidate.ImageSha256);
            var service = new LocalCreativeBindingProposal(db);
            var first = await service.ReadAsync(grant, proposal.Id, selected);
            Assert.Equal(first, await service.ReadAsync(grant, proposal.Id, selected)); Assert.Empty(db.ChangeTracker.Entries());
            using var doc = JsonDocument.Parse(first.Json); var report = doc.RootElement;
            Assert.Equal("PROPOSED_ONLY", report.GetProperty("assignmentStatus").GetString());
            Assert.True(report.GetProperty("candidateBytesVerified").GetBoolean());
            Assert.Equal(candidate.ImageSha256, report.GetProperty("candidateBytesSha256").GetString());
            foreach (var flag in new[] { "existingCampaignHandoffVerified", "campaignAssignmentVerified", "creativeRightsVerified", "consentVerified", "advertiserAuthorityVerified", "paymentAuthorized", "activationAuthorized", "independentQaAccepted" })
                Assert.False(report.GetProperty(flag).GetBoolean());
            foreach (var bad in new[] { selected with { WorkspaceId = Guid.NewGuid() }, selected with { CandidateId = Guid.NewGuid() },
                selected with { ExpectedVersion = selected.ExpectedVersion + 1 }, selected with { ExpectedImageSha256 = new string('0',64) },
                selected with { ExpectedVersion = 0 }, selected with { ExpectedImageSha256 = "invalid" } })
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, proposal.Id, bad));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant with { ReviewInspectionGranted = false }, proposal.Id, selected));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant with { Reader = grant.Reader with { AdvertiserId = Phase1DataSeeder.AdvertiserId } }, proposal.Id, selected));
            var artifact = Environment.GetEnvironmentVariable("AGM_CREATIVE_BINDING_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(artifact)) await File.WriteAllTextAsync(artifact, JsonSerializer.Serialize(first));
            // The existing submission service appends a new revision: the old selected candidate must then be refused.
            var production = new AcademyProductionService(db, new BlueprintPhaseService(db, environment));
            var newer = (await production.SubmitAsync(creative.Workspace, creative.Brief,
                new SubmitProductionCandidate(candidate.Version, "TEST revised", "Synthetic only", CreativeHandoffTestData.Png, "binding-revision"),
                "ADVERTISER", "Fixture owner", "fixture", default)).Row;
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, proposal.Id, selected));
            var latest = selected with { CandidateId = newer.Id, ExpectedVersion = newer.Version, ExpectedImageSha256 = newer.ImageSha256 };
            Assert.NotEqual(first, await service.ReadAsync(grant, proposal.Id, latest));
            await using var raw = new NpgsqlConnection(scoped); await raw.OpenAsync();
            await using (var corrupt = new NpgsqlCommand("UPDATE \"AcademyProductionCandidates\" SET \"ImageBytes\"=@bytes WHERE \"Id\"=@id", raw))
            { corrupt.Parameters.AddWithValue("bytes", new byte[] { 1, 2, 3 }); corrupt.Parameters.AddWithValue("id", newer.Id); await corrupt.ExecuteNonQueryAsync(); }
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, proposal.Id, latest));
            Assert.Empty(await db.CreativeCampaignHandoffs.ToListAsync()); Assert.Empty(db.ChangeTracker.Entries());
            await using var count = new NpgsqlCommand("SELECT count(*) FROM local_lineage_proposals", raw);
            Assert.Equal(1L, await count.ExecuteScalarAsync());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
