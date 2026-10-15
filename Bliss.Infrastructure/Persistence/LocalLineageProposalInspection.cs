using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// Trusted identity and separate inspection permission; not a public request DTO.
public sealed record LocalLineageInspectionGrant(LocalLineageGrant Reader, bool ReviewInspectionGranted);
public sealed record LocalLineageInspection(Guid ProposalId, string EvidenceId, string SubmittedBy, string InspectedBy,
    string FrozenSha256, string CurrentLineageState, string? CurrentSha256,
    bool EvidenceIntegrityVerified, bool IndependentQaAccepted, bool ConsentVerified,
    bool CreativeRightsVerified, bool PaymentAuthorized, bool ActivationAuthorized);

/// <summary>Read-only technical inspection of an exact scoped proposal. Never a review decision.</summary>
public sealed class LocalLineageProposalInspection(BlissDbContext db)
{
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    public async Task<LocalLineageInspection> InspectAsync(LocalLineageInspectionGrant grant, Guid proposalId, CancellationToken ct = default)
    {
        await using var connection = new LocalLineageProposalStore(db).Connection(grant.Reader);
        if (!grant.ReviewInspectionGranted || proposalId == Guid.Empty)
            throw new InvalidOperationException("Separate trusted inspection permission and exact proposal ID required.");
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT advertiser_id,actor_subject,request_id,evidence_id,request_fingerprint,report_json,report_sha256,status
            FROM local_lineage_proposals WHERE id=@id AND advertiser_id=@advertiser
            """, connection);
        command.Parameters.AddWithValue("id", proposalId); command.Parameters.AddWithValue("advertiser", grant.Reader.AdvertiserId);
        Guid requestId; string actor, evidenceId, fingerprint, json, sha;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Proposal not found in granted advertiser scope.");
            actor = reader.GetString(1);
            if (string.Equals(actor, grant.Reader.Subject, StringComparison.Ordinal))
                throw new InvalidOperationException("A distinct trusted reviewer subject is required.");
            requestId = reader.GetGuid(2); evidenceId = reader.GetString(3); fingerprint = reader.GetString(4);
            json = reader.GetString(5); sha = reader.GetString(6);
            if (reader.GetGuid(0) != grant.Reader.AdvertiserId || reader.GetString(7) != "PENDING_REVIEW" || Hash(json) != sha)
                throw new InvalidOperationException("Frozen proposal integrity disagrees with stored scope/status/hash.");
        }
        LocalLineageSelection selection;
        try
        {
            using var doc = JsonDocument.Parse(json); var report = doc.RootElement;
            if (report.GetProperty("schema").GetString() != "AGM-CAMPAIGN-LINEAGE/1"
                || report.GetProperty("classification").GetString() != "LOCAL_SYNTHETIC"
                || report.GetProperty("advertiserId").GetGuid() != grant.Reader.AdvertiserId
                || report.GetProperty("selectedBy").GetString() != actor)
                throw new InvalidOperationException("Frozen report does not match proposal provenance.");
            foreach (var flag in new[] { "researchTruthVerified", "consentVerified", "creativeRightsVerified", "paymentAuthorized", "activationAuthorized", "independentQaAccepted" })
                if (report.GetProperty(flag).GetBoolean()) throw new InvalidOperationException("Frozen synthetic evidence claims authority.");
            var selected = report.GetProperty("selection");
            selection = new(selected.GetProperty("ingestionRunId").GetGuid(), selected.GetProperty("formationRunId").GetGuid(), selected.GetProperty("placementRunId").GetGuid());
            if (Hash(JsonSerializer.Serialize(new LocalLineageProposalRequest(requestId, evidenceId, selection, sha))) != fingerprint)
                throw new InvalidOperationException("Frozen selection does not match original request fingerprint.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException)
        { throw new InvalidOperationException("Frozen report cannot be safely interpreted.", ex); }

        string state; string? currentSha = null;
        try
        {
            // Preserve the captured actor for deterministic comparison, without granting that actor any write permission.
            var current = await new LocalCampaignLineageEvidence(db).ReadAsync(grant.Reader with { Subject = actor }, selection, ct);
            currentSha = current.Sha256; state = currentSha == sha ? "MATCHES_CAPTURED_REPORT" : "CHANGED_SINCE_CAPTURE";
        }
        catch (InvalidOperationException) { state = "CURRENT_LINEAGE_UNAVAILABLE"; }
        return new(proposalId, evidenceId, actor, grant.Reader.Subject, sha, state, currentSha,
            true, false, false, false, false, false);
    }
}
