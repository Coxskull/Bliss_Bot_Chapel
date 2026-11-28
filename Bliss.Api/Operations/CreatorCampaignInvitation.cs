using System.Text.Json;
using Bliss.Api.Controllers;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// Non-binding planning invitation, not a contract, quote, booking or payment.
public static class CreatorCampaignInvitation
{
    public const string Action = "CREATOR_CAMPAIGN_INVITATION";
    public const string ResponseAction = "CREATOR_CAMPAIGN_INVITATION_RESPONSE";
    public sealed record Snapshot(Guid PlacementId, Guid CampaignId, Guid CreatorId, Guid SlotId,
        Guid AdvertiserId, Guid WorkspaceId, string CampaignName, string ContentTitle, string SlotType,
        int? StartSecond, int? DurationSeconds);
    public static Guid InvitationId(Guid placementId) => StarterArtworkDelivery.StableId(Action, placementId.ToString("N"));
    public static Guid ResponseId(Guid invitationId) => StarterArtworkDelivery.StableId(ResponseAction, invitationId.ToString("N"));
    public static string Digest(Snapshot row) => ControlledAiService.Hash(JsonSerializer.Serialize(row));
    public static string Detail(Snapshot row) => row.PlacementId.ToString("N") + ":" + Digest(row);
    public static async Task<Snapshot?> Current(BlissDbContext db, Guid placementId, CancellationToken ct)
    {
        var row = await db.CampaignPlacements.AsNoTracking()
            .Where(p => p.Id == placementId && p.Status == "PLANNED"
                && p.Campaign.Status == "DRAFT" && p.Campaign.AuthorityStatus == "VERIFIED"
                && p.Campaign.AdvertiserOpportunity != null && p.Campaign.AdvertiserOpportunity.Status == "ACTIVE"
                && p.Campaign.AdvertiserOpportunity.AuthorityStatus == "VERIFIED"
                && p.Campaign.AdvertiserOpportunity.AdvertiserProgram.Status == "ACTIVE"
                && p.BlissMatch != null && p.BlissMatch.Status == "APPROVED"
                && p.BlissMatch.AdvertiserOpportunityId == p.Campaign.AdvertiserOpportunityId
                && p.BlissMatch.CreatorId == p.ContentItem.CreatorId
                && p.AdInventorySlot.ContentItemId == p.ContentItemId && p.AdInventorySlot.IsAvailable
                && p.ContentItem.Creator.AuthorityStatus == "VERIFIED"
                && db.CreatorAccountMemberships.Any(m => m.CreatorId == p.ContentItem.CreatorId && m.Status == "ACTIVE"
                    && db.CreatorRegistrationReceipts.Any(r => r.CreatorId == m.CreatorId && r.Subject == m.Subject
                        && r.ProcessingConsent && r.TermsAccepted
                        && r.PrivacyNoticeVersion == CreatorRegistrationController.PrivacyNoticeVersion
                        && r.TermsVersion == CreatorRegistrationController.TermsVersion)))
            .Select(p => new { p.Id, p.CampaignId, p.ContentItem.CreatorId, p.AdInventorySlotId,
                p.Campaign.AdvertiserOpportunity!.AdvertiserProgram.AdvertiserId,
                CampaignName = p.Campaign.Name, ContentTitle = p.ContentItem.Title,
                p.AdInventorySlot.SlotType, p.AdInventorySlot.StartSecond, p.AdInventorySlot.DurationSeconds })
            .SingleOrDefaultAsync(ct);
        if (row is null || !await StarterServiceOffer.ActiveMember(db, row.AdvertiserId, null, ct)) return null;
        var workspace = await db.WeddingPlannerWorkspaces.AsNoTracking()
            .Where(w => w.AdvertiserId == row.AdvertiserId && w.IsPrimary && w.Status == "ACTIVE")
            .Select(w => (Guid?)w.Id).SingleOrDefaultAsync(ct);
        return workspace is null ? null : new Snapshot(row.Id,row.CampaignId,row.CreatorId,row.AdInventorySlotId,
            row.AdvertiserId,workspace.Value,row.CampaignName,row.ContentTitle,row.SlotType,row.StartSecond,row.DurationSeconds);
    }
    public static bool Matches(Bliss.Domain.Entities.WeddingPlannerAuditEvent audit, Snapshot row) =>
        audit.Id == InvitationId(row.PlacementId) && audit.Action == Action && audit.ActorType == "OPERATOR"
        && audit.Outcome == "INVITED" && audit.AdvertiserId == row.AdvertiserId && audit.WorkspaceId == row.WorkspaceId
        && audit.RequestId == row.CreatorId.ToString("N") && audit.Detail == Detail(row);
}
