using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record LocalCreativeSelection(Guid WorkspaceId, Guid CandidateId, int ExpectedVersion, string ExpectedImageSha256);
public sealed record LocalCreativeBindingReport(string Json, string Sha256);

/// <summary>Proposes an existing Academy candidate for local lineage; does not prove a handoff or rights.</summary>
public sealed class LocalCreativeBindingProposal(BlissDbContext db)
{
    public async Task<LocalCreativeBindingReport> ReadAsync(LocalLineageInspectionGrant grant, Guid proposalId,
        LocalCreativeSelection selection, CancellationToken ct = default)
    {
        var checklist = await new LocalLineageAuthorityChecklist(db).ReadAsync(grant, proposalId, ct);
        if (selection.WorkspaceId == Guid.Empty || selection.CandidateId == Guid.Empty || selection.ExpectedVersion < 1
            || selection.ExpectedImageSha256.Length != 64 || selection.ExpectedImageSha256.Any(c => !"0123456789abcdef".Contains(c)))
            throw new InvalidOperationException("Explicit workspace/candidate, positive version and canonical image SHA256 required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", ct);
        var workspace = await db.WeddingPlannerWorkspaces.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selection.WorkspaceId
            && x.AdvertiserId == grant.Reader.AdvertiserId && x.Status == "ACTIVE", ct)
            ?? throw new InvalidOperationException("Active workspace not found in granted advertiser scope.");
        var candidate = await db.AcademyProductionCandidates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selection.CandidateId
            && x.ImageBytes.Length > 0 && x.ImageBytes.Length <= 10485760
            && db.AcademyProductionBriefs.Any(b => b.Id == x.BriefId && b.WorkspaceId == workspace.Id), ct)
            ?? throw new InvalidOperationException("Candidate not found in selected workspace scope.");
        var latest = await db.AcademyProductionCandidates.Where(x => x.BriefId == candidate.BriefId).MaxAsync(x => x.Version, ct);
        if (candidate.Version != selection.ExpectedVersion || candidate.Version != latest || candidate.ImageBytes.Length is 0 or > 10485760)
            throw new InvalidOperationException("Exact latest bounded stored candidate version required.");
        var actualSha = Convert.ToHexString(SHA256.HashData(candidate.ImageBytes)).ToLowerInvariant();
        if (actualSha != candidate.ImageSha256 || actualSha != selection.ExpectedImageSha256)
            throw new InvalidOperationException("Stored bytes disagree with selected candidate image hash.");
        var json = JsonSerializer.Serialize(new
        {
            schema = "AGM-LOCAL-CREATIVE-BINDING-PROPOSAL/1", classification = "LOCAL_SYNTHETIC",
            proposalId, advertiserId = grant.Reader.AdvertiserId, inspectedBy = grant.Reader.Subject,
            checklistSha256 = checklist.Sha256, selection, briefId = candidate.BriefId,
            candidateByteCount = candidate.ImageBytes.Length, candidateBytesSha256 = actualSha,
            assignmentStatus = "PROPOSED_ONLY", candidateBytesVerified = true,
            existingCampaignHandoffVerified = false, campaignAssignmentVerified = false,
            creativeRightsVerified = false, consentVerified = false, advertiserAuthorityVerified = false,
            paymentAuthorized = false, activationAuthorized = false, independentQaAccepted = false,
            missingEvidence = new[] { "EXACT_CAMPAIGN_HANDOFF_CORRELATION", "CREATIVE_USAGE_RIGHTS", "CREATOR_INVENTORY_CONSENT", "ADVERTISER_AUTHORITY" },
            delivery = "NOT_SENT", humanReviewRequired = true,
            limitation = "Stored image-byte identity and version only; not image quality, source truth, existing campaign assignment, independent QA or rights. Lineage and candidate snapshots are separate and may change later."
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await tx.CommitAsync(ct);
        return new(json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant());
    }
}
