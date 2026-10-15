using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

public sealed record LocalCreativeHandoffReport(string Json, string Sha256);

/// <summary>Correlates an exact existing planned handoff, never authorizes delivery or legal rights.</summary>
public sealed class LocalCreativeHandoffEvidence(BlissDbContext db)
{
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public async Task<LocalCreativeHandoffReport> ReadAsync(LocalLineageInspectionGrant grant, Guid proposalId,
        LocalCreativeSelection creative, Guid handoffId, CancellationToken ct = default)
    {
        if (handoffId == Guid.Empty) throw new InvalidOperationException("Exact stored handoff ID required.");
        var inspected = await new LocalLineageProposalInspection(db).InspectAsync(grant, proposalId, ct);
        var binding = await new LocalCreativeBindingProposal(db).ReadAsync(grant, proposalId, creative, ct);
        string frozen;
        await using (var connection = new LocalLineageProposalStore(db).Connection(grant.Reader))
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT report_json FROM local_lineage_proposals WHERE id=@id AND advertiser_id=@advertiser", connection);
            command.Parameters.AddWithValue("id", proposalId); command.Parameters.AddWithValue("advertiser", grant.Reader.AdvertiserId);
            frozen = await command.ExecuteScalarAsync(ct) as string ?? throw new InvalidOperationException("Scoped proposal missing.");
            if (Hash(frozen) != inspected.FrozenSha256) throw new InvalidOperationException("Frozen proposal changed during correlation.");
        }
        using var frozenDoc = JsonDocument.Parse(frozen); var lineage = frozenDoc.RootElement;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var handoff = await db.CreativeCampaignHandoffs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == handoffId && x.WorkspaceId == creative.WorkspaceId, ct)
            ?? throw new InvalidOperationException("Handoff not found in selected workspace scope.");
        if (handoff.CandidateId != creative.CandidateId || handoff.CampaignPlacementId != lineage.GetProperty("campaignPlacementId").GetGuid()
            || handoff.CampaignPlacementRunId != lineage.GetProperty("selection").GetProperty("placementRunId").GetGuid()
            || handoff.Status != "PLANNED" || handoff.Delivery != "NOT_SENT"
            || string.IsNullOrWhiteSpace(handoff.SnapshotJson) || handoff.SnapshotJson.Length > 65536)
            throw new InvalidOperationException("Exact selected planned, unsent handoff lineage is required.");
        using var snapshotDoc = JsonDocument.Parse(handoff.SnapshotJson); var snapshot = snapshotDoc.RootElement;
        bool Same(string property, Guid id) => snapshot.GetProperty(property).GetGuid() == id && id != Guid.Empty;
        if (!Same("id", creative.CandidateId) || snapshot.GetProperty("imageSha256").GetString() != creative.ExpectedImageSha256
            || !Same("reviewId", handoff.ReviewId) || !Same("advertiserApproval", handoff.AdvertiserApprovalId)
            || !Same("qaApproval", handoff.AlphaQaApprovalId) || !Same("inventoryFit", handoff.InventoryFitId)
            || !Same("quoteLineId", handoff.QuoteLineItemId)
            || !Same("campaignId", lineage.GetProperty("campaignId").GetGuid())
            || !Same("matchId", lineage.GetProperty("matchId").GetGuid())
            || !Same("contentItemId", lineage.GetProperty("contentItemId").GetGuid())
            || !Same("slotId", lineage.GetProperty("adInventorySlotId").GetGuid())
            || !Same("ruleVersionId", lineage.GetProperty("ruleVersionId").GetGuid())
            || !await db.AcademyProductionReviews.AnyAsync(x => x.Id == handoff.ReviewId && x.CandidateId == handoff.CandidateId, ct))
            throw new InvalidOperationException("Stored handoff snapshot disagrees with selected records.");
        var decisions = await db.CandidateApprovalDecisions.AsNoTracking().Where(x => x.CandidateId == handoff.CandidateId
            && x.ReviewId == handoff.ReviewId && (x.Id == handoff.AdvertiserApprovalId || x.Id == handoff.AlphaQaApprovalId || x.Id == handoff.InventoryFitId)).ToListAsync(ct);
        var opportunityId = lineage.GetProperty("opportunityId").GetGuid(); var matchId = lineage.GetProperty("matchId").GetGuid();
        var slotId = lineage.GetProperty("adInventorySlotId").GetGuid();
        if (!decisions.Any(x => x.Id == handoff.AdvertiserApprovalId && x.Stage == "ADVERTISER")
            || !decisions.Any(x => x.Id == handoff.AlphaQaApprovalId && x.Stage == "ALPHA_QA")
            || !decisions.Any(x => x.Id == handoff.InventoryFitId && x.Stage == "INVENTORY_FIT" && x.AdInventorySlotId == slotId)
            || !await db.QuoteLineItems.AnyAsync(x => x.Id == handoff.QuoteLineItemId
                && x.QuoteVersion.Quote.AdvertiserOpportunityId == opportunityId
                && x.RateRecommendation.BlissMatchId == matchId && x.RateRecommendation.AdInventorySlotId == slotId, ct))
            throw new InvalidOperationException("Stored approval or quote references disagree with selected lineage.");
        var json = JsonSerializer.Serialize(new
        {
            schema = "AGM-LOCAL-CREATIVE-HANDOFF-EVIDENCE/1", classification = "LOCAL_SYNTHETIC", proposalId,
            inspected.EvidenceId, frozenSha256 = inspected.FrozenSha256, creativeBindingSha256 = binding.Sha256,
            handoffId, handoff.WorkspaceId, handoff.CandidateId, candidateVersion = creative.ExpectedVersion,
            candidateImageSha256 = creative.ExpectedImageSha256, handoff.ReviewId,
            handoff.CampaignPlacementId, handoff.CampaignPlacementRunId, handoff.QuoteLineItemId,
            handoffSnapshotSha256 = Hash(handoff.SnapshotJson),
            assignmentStatus = "STORED_PLANNED_HANDOFF_CORRELATED", storedHandoffCorrelationVerified = true,
            handoffPolicyRevalidated = false, creativeRightsVerified = false, creatorConsentVerified = false,
            advertiserAuthorityVerified = false, paymentAuthorized = false, activationAuthorized = false,
            independentQaAccepted = false, delivery = "NOT_SENT", humanReviewRequired = true,
            limitation = "Stored synthetic record correlation only; approval IDs are references, not proof of current approval policy, real-party authority or image quality. Separate snapshots can change after inspection."
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await tx.CommitAsync(ct); return new(json, Hash(json));
    }
}
