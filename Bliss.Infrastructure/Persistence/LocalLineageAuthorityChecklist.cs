using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

public sealed record LocalLineageAuthorityChecklistReport(string Json, string Sha256);

/// <summary>Evidence-request template for exact local synthetic lineage; not an authority verifier.</summary>
public sealed class LocalLineageAuthorityChecklist(BlissDbContext db)
{
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public async Task<LocalLineageAuthorityChecklistReport> ReadAsync(LocalLineageInspectionGrant grant, Guid proposalId, CancellationToken ct = default)
    {
        var inspection = await new LocalLineageProposalInspection(db).InspectAsync(grant, proposalId, ct);
        if (!inspection.EvidenceIntegrityVerified || inspection.CurrentLineageState != "MATCHES_CAPTURED_REPORT")
            throw new InvalidOperationException("Obtain a fresh matching proposal before requesting bound authority evidence.");
        await using var connection = new LocalLineageProposalStore(db).Connection(grant.Reader);
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT report_json FROM local_lineage_proposals WHERE id=@id AND advertiser_id=@advertiser", connection);
        cmd.Parameters.AddWithValue("id", proposalId); cmd.Parameters.AddWithValue("advertiser", grant.Reader.AdvertiserId);
        var frozen = await cmd.ExecuteScalarAsync(ct) as string;
        if (frozen is null || Hash(frozen) != inspection.FrozenSha256)
            throw new InvalidOperationException("Frozen proposal changed during checklist construction.");
        using var doc = JsonDocument.Parse(frozen); var report = doc.RootElement;
        Guid Id(string property)
        {
            var id = report.GetProperty(property).GetGuid();
            return id != Guid.Empty ? id : throw new InvalidOperationException("Explicit nonempty binding IDs are required.");
        }
        var creatorId = Id("creatorId"); var platformId = Id("platformId"); var contentItemId = Id("contentItemId");
        var adInventorySlotId = Id("adInventorySlotId"); var campaignId = Id("campaignId");
        var campaignPlacementId = Id("campaignPlacementId"); var opportunityId = Id("opportunityId");
        var json = JsonSerializer.Serialize(new
        {
            schema = "AGM-LOCAL-AUTHORITY-CHECKLIST/1", classification = "LOCAL_SYNTHETIC",
            proposalId, inspection.EvidenceId, inspection.FrozenSha256, inspectedBy = grant.Reader.Subject,
            advertiserId = grant.Reader.AdvertiserId, sourceComparison = "MATCH_AT_INSPECTION_ONLY",
            policyStatus = "OWNER_POLICY_REVIEW_REQUIRED",
            requestedEvidenceFields = new[] { "evidence_reference_and_sha256", "issuer_and_signatory_authority",
                "binding_to_subject_ids", "permitted_actions_channels_territories_and_dates", "revocation_and_expiry_conditions" },
            requirements = new object[]
            {
                new { kind = "CREATOR_INVENTORY_CONSENT", status = "EVIDENCE_REQUIRED", verified = false,
                    bindings = new { creatorId, platformId, contentItemId, adInventorySlotId, campaignId, campaignPlacementId } },
                new { kind = "ADVERTISER_CAMPAIGN_AUTHORITY", status = "EVIDENCE_REQUIRED", verified = false,
                    bindings = new { advertiserId = grant.Reader.AdvertiserId, opportunityId, campaignId, campaignPlacementId } },
                new { kind = "CREATIVE_USAGE_RIGHTS", status = "EVIDENCE_REQUIRED", verified = false,
                    bindings = new { contentItemId, adInventorySlotId, campaignId, campaignPlacementId,
                        advertiserCreativeAssetId = (Guid?)null, creativeVersionSha256 = (string?)null },
                    missingBindings = new[] { "advertiserCreativeAssetId", "creativeVersionSha256" } }
            },
            consentVerified = false, advertiserAuthorityVerified = false, creativeRightsVerified = false,
            paymentAuthorized = false, activationAuthorized = false, independentQaAccepted = false,
            delivery = "NOT_SENT", humanReviewRequired = true,
            limitation = "Development evidence-request template, not an approved legal policy, authority verification or outreach. Creative asset/version is not present in the captured lineage. Source can change after inspection."
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(json, Hash(json));
    }
}
