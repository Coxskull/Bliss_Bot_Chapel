using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class LocalLineageAuthorityChecklistTests
{
    [CampaignAuthorityPostgresFact]
    public async Task Checklist_binds_exact_scope_marks_missing_creative_and_never_grants_authority()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")!;
        var schema = "authority_checklist_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
            await using var db = new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scoped).Options);
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            await db.Database.ExecuteSqlRawAsync(LocalLineageProposalStore.FixtureSchemaSql);
            var selected = await LocalCampaignLineageEvidenceTests.Seed(db);
            var author = new LocalLineageProposalGrant(new(true, "fixture-author", Phase2DataSeeder.Advertiser2Id, true), true);
            var captured = await new LocalCampaignLineageEvidence(db).ReadAsync(author.Reader, selected);
            var request = new LocalLineageProposalRequest(Guid.NewGuid(), "AGM-EV-" + Guid.NewGuid(), selected, captured.Sha256);
            var proposal = await new LocalLineageProposalStore(db).ProposeAsync(author, request);
            var grant = new LocalLineageInspectionGrant(author.Reader with { Subject = "fixture-inspector" }, true);
            var service = new LocalLineageAuthorityChecklist(db);
            foreach (var refused in new[] { grant with { ReviewInspectionGranted = false }, grant with { Reader = author.Reader },
                grant with { Reader = grant.Reader with { EvidenceReadGranted = false } },
                grant with { Reader = grant.Reader with { AdvertiserId = Phase1DataSeeder.AdvertiserId } } })
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(refused, proposal.Id));
            var first = await service.ReadAsync(grant, proposal.Id);
            Assert.Equal(first, await service.ReadAsync(grant, proposal.Id)); Assert.Empty(db.ChangeTracker.Entries());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Json))).ToLowerInvariant(), first.Sha256);
            using var doc = JsonDocument.Parse(first.Json); var checklist = doc.RootElement;
            using var frozenDoc = JsonDocument.Parse(captured.Json); var frozen = frozenDoc.RootElement;
            Assert.Equal("OWNER_POLICY_REVIEW_REQUIRED", checklist.GetProperty("policyStatus").GetString());
            Assert.Equal("MATCH_AT_INSPECTION_ONLY", checklist.GetProperty("sourceComparison").GetString());
            Assert.Equal(proposal.Id, checklist.GetProperty("proposalId").GetGuid());
            Assert.Equal(proposal.Sha256, checklist.GetProperty("frozenSha256").GetString());
            var items = checklist.GetProperty("requirements").EnumerateArray().ToArray(); Assert.Equal(3, items.Length);
            foreach (var item in items)
            {
                Assert.Equal("EVIDENCE_REQUIRED", item.GetProperty("status").GetString()); Assert.False(item.GetProperty("verified").GetBoolean());
                foreach (var binding in item.GetProperty("bindings").EnumerateObject())
                    if (binding.Value.ValueKind != JsonValueKind.Null) Assert.Equal(frozen.GetProperty(binding.Name).GetGuid(), binding.Value.GetGuid());
            }
            var creative = items.Single(x => x.GetProperty("kind").GetString() == "CREATIVE_USAGE_RIGHTS");
            Assert.Equal(2, creative.GetProperty("missingBindings").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, creative.GetProperty("bindings").GetProperty("advertiserCreativeAssetId").ValueKind);
            Assert.Equal(JsonValueKind.Null, creative.GetProperty("bindings").GetProperty("creativeVersionSha256").ValueKind);
            foreach (var flag in new[] { "consentVerified", "advertiserAuthorityVerified", "creativeRightsVerified", "paymentAuthorized", "activationAuthorized", "independentQaAccepted" })
                Assert.False(checklist.GetProperty(flag).GetBoolean());
            Assert.Equal("NOT_SENT", checklist.GetProperty("delivery").GetString());
            var path = Environment.GetEnvironmentVariable("AGM_AUTHORITY_CHECKLIST_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(path)) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(first));
            var intake = await db.CreatorIngestionRuns.SingleAsync(x => x.Id == selected.IngestionRunId);
            intake.InputSnapshot += " "; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, proposal.Id));
            db.DataProvenances.RemoveRange(await db.DataProvenances.Where(x => x.EntityId == intake.CreatorId).ToListAsync());
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, proposal.Id));
            Assert.Equal(proposal, await new LocalLineageProposalStore(db).ProposeAsync(author, request));
            await using var raw = new NpgsqlConnection(scoped); await raw.OpenAsync();
            await using var count = new NpgsqlCommand("SELECT count(*) FROM local_lineage_proposals", raw);
            Assert.Equal(1L, await count.ExecuteScalarAsync()); Assert.Empty(db.ChangeTracker.Entries());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
