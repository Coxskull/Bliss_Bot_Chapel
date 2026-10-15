using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// Trusted caller policy, not a public request DTO. No route or DI registration.
public sealed record LocalLineageGrant(bool Authenticated, string Subject, Guid AdvertiserId, bool EvidenceReadGranted);
public sealed record LocalLineageSelection(Guid IngestionRunId, Guid FormationRunId, Guid PlacementRunId);
public sealed record LocalLineageReport(string Json, string Sha256);

/// <summary>Explicit read-only correlation of selected local synthetic audit records.</summary>
public sealed class LocalCampaignLineageEvidence(BlissDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public async Task<LocalLineageReport> ReadAsync(LocalLineageGrant grant, LocalLineageSelection selection, CancellationToken ct = default)
    {
        if (!grant.Authenticated || !grant.EvidenceReadGranted || string.IsNullOrWhiteSpace(grant.Subject)
            || grant.Subject.Length > 128 || grant.AdvertiserId == Guid.Empty)
            throw new InvalidOperationException("Trusted scoped local evidence-read grant is required.");
        if (selection.IngestionRunId == Guid.Empty || selection.FormationRunId == Guid.Empty || selection.PlacementRunId == Guid.Empty)
            throw new InvalidOperationException("All three explicit audit selections are required.");
        if (db.ChangeTracker.HasChanges() || db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Use a clean dedicated read context.");
        if (db.Database.IsRelational())
        {
            if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
                throw new InvalidOperationException("Only the isolated PostgreSQL fixture is supported.");
            var target = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
            if (target.Host is not ("localhost" or "127.0.0.1" or "::1") || target.Database is not ("campaign_test" or "bliss_ci"))
                throw new InvalidOperationException("Local lineage evidence requires a named loopback disposable fixture.");
        }
        else if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            throw new InvalidOperationException("Only local fixture providers are supported.");

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        if (tx is not null) await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        // Ownership is enforced before retrieving other audit records; no arbitrary enumeration.
        var placement = await db.CampaignPlacementRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selection.PlacementRunId
            && x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == grant.AdvertiserId, ct)
            ?? throw new InvalidOperationException("Selected lineage was not found in the granted advertiser scope.");
        var formation = await db.MatchFormationRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selection.FormationRunId
            && x.BlissMatchId == placement.BlissMatchId && x.CreatorId == placement.CreatorId
            && x.AdvertiserOpportunityId == placement.AdvertiserOpportunityId, ct)
            ?? throw new InvalidOperationException("Selected formation does not match the placement lineage.");
        var intake = await db.CreatorIngestionRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selection.IngestionRunId
            && x.CreatorId == formation.CreatorId, ct)
            ?? throw new InvalidOperationException("Selected intake does not match the explicit formation creator.");
        var match = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == formation.BlissMatchId, ct);
        var creator = await db.Creators.AsNoTracking().SingleAsync(x => x.Id == intake.CreatorId, ct);
        var platform = await db.CreatorPlatforms.AsNoTracking().SingleAsync(x => x.Id == intake.CreatorPlatformId, ct);
        var opportunity = await db.AdvertiserOpportunities.AsNoTracking().SingleAsync(x => x.Id == formation.AdvertiserOpportunityId, ct);
        var campaign = await db.Campaigns.AsNoTracking().SingleAsync(x => x.Id == placement.CampaignId, ct);
        var content = await db.ContentItems.AsNoTracking().SingleAsync(x => x.Id == placement.ContentItemId, ct);
        var slot = await db.AdInventorySlots.AsNoTracking().SingleAsync(x => x.Id == placement.AdInventorySlotId, ct);
        var planned = await db.CampaignPlacements.AsNoTracking().SingleAsync(x => x.Id == placement.CampaignPlacementId, ct);
        if (creator.AuthorityStatus != RecordAuthorityStatuses.SyntheticFixture
            || opportunity.AuthorityStatus != RecordAuthorityStatuses.SyntheticFixture || campaign.AuthorityStatus != RecordAuthorityStatuses.SyntheticFixture)
            throw new InvalidOperationException("This local evidence builder accepts entirely SYNTHETIC_FIXTURE lineage only.");
        if (intake.Status != EntityStatuses.Completed || formation.Status != EntityStatuses.Completed || placement.Status != EntityStatuses.Completed
            || placement.Outcome != EntityStatuses.Planned || planned.Status != EntityStatuses.Planned || campaign.Status != EntityStatuses.Draft
            || match.Status != EntityStatuses.Approved || opportunity.Status != EntityStatuses.Active
            || platform.CreatorId != creator.Id || platform.IdentityKey != intake.IdentityKey
            || match.CreatorId != creator.Id || match.AdvertiserOpportunityId != opportunity.Id || match.RuleVersionId != formation.RuleVersionId
            || campaign.AdvertiserOpportunityId != opportunity.Id || content.CreatorId != creator.Id || slot.ContentItemId != content.Id
            || planned.BlissMatchId != match.Id || planned.CampaignId != campaign.Id || planned.ContentItemId != content.Id || planned.AdInventorySlotId != slot.Id
            || planned.StartAt is not null || planned.EndAt is not null
            || intake.CompletedAt > formation.StartedAt || formation.CompletedAt > placement.StartedAt)
            throw new InvalidOperationException("Stored graph, status or chronology disagrees with the selected planning lineage.");
        var note = $"Creator ingestion run {intake.Id}.";
        var provenance = await db.DataProvenances.AsNoTracking().Where(x => x.Notes == note
            && x.SourceType == "PROVIDER_OBSERVATION" && x.SourceName == intake.SourceSystem
            && ((x.EntityType == nameof(Creator) && x.EntityId == creator.Id)
                || (x.EntityType == nameof(CreatorPlatform) && x.EntityId == platform.Id)))
            .OrderBy(x => x.Id).Take(65).ToListAsync(ct);
        if (provenance.Count > 64 || !provenance.Any(x => x.EntityType == nameof(Creator) && x.FieldName == nameof(Creator.Name))
            || !provenance.Any(x => x.EntityType == nameof(CreatorPlatform) && x.FieldName == nameof(CreatorPlatform.IdentityKey)))
            throw new InvalidOperationException("Exact intake provenance is missing or exceeds the evidence bound.");
        if (new[] { intake.InputSnapshot, formation.InputSnapshot, placement.InputSnapshot }.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 65536))
            throw new InvalidOperationException("Bounded stored audit snapshots are required.");
        var json = JsonSerializer.Serialize(new
        {
            schema = "AGM-CAMPAIGN-LINEAGE/1", classification = "LOCAL_SYNTHETIC", grant.AdvertiserId,
            selectedBy = grant.Subject, selection, creatorId = creator.Id, platformId = platform.Id,
            opportunityId = opportunity.Id, matchId = match.Id, formation.RuleVersionId,
            campaignId = campaign.Id, placement.CampaignPlacementId, placement.ContentItemId, placement.AdInventorySlotId,
            intakeSnapshotSha256 = Hash(intake.InputSnapshot), formationSnapshotSha256 = Hash(formation.InputSnapshot),
            placementSnapshotSha256 = Hash(placement.InputSnapshot),
            provenance = provenance.Select(x => new { x.Id, x.EntityType, x.EntityId, x.FieldName,
                snapshotSha256 = Hash(JsonSerializer.Serialize(new { x.SourceType, x.SourceName, x.SourceUrl, x.ConfidenceLevel, x.CollectedAt, x.Notes }, Json)) }).ToArray(),
            traceVerified = true, researchTruthVerified = false, consentVerified = false, creativeRightsVerified = false,
            paymentAuthorized = false, activationAuthorized = false, independentQaAccepted = false,
            delivery = "NOT_SENT", humanReviewRequired = true,
            limitation = "Selected stored record correlation only; not proof intake informed scoring, external research truth, consent or independent QA."
        }, Json);
        if (tx is not null) await tx.CommitAsync(ct);
        return new(json, Hash(json));
    }
}
