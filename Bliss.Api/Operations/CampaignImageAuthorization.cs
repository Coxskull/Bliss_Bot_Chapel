using System.Text.Json;
using Bliss.Api.Controllers;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// A customer grant is tied to the current campaign, primary workspace,
// immutable recipe, latest confirmed brand and active consent-bearing subject.
public static class CampaignImageAuthorization
{
    public const string KeyPrefix = "campaign-image:";
    public static string RunKey(Guid briefId) => KeyPrefix + briefId.ToString("N");
    public sealed record Grant(Guid CampaignId, string CampaignDigest, Guid ReceiptId, string RecipeDigest, string Recipe);
    public static async Task<Grant?> Current(BlissDbContext db, Guid workspaceId, Guid briefId, CancellationToken ct)
    {
        var prepared = await (from b in db.AcademyProductionBriefs.AsNoTracking()
            join w in db.WeddingPlannerWorkspaces.AsNoTracking() on b.WorkspaceId equals w.Id
            where b.Id == briefId && w.Id == workspaceId && w.IsPrimary && w.Status == "ACTIVE"
            select new { Brief = b, w.AdvertiserId }).SingleOrDefaultAsync(ct);
        if (prepared is null) return null;
        var parts = prepared.Brief.IdempotencyKey.Split(':');
        if (parts.Length != 3 || parts[0] != "campaign" || !Guid.TryParseExact(parts[1], "N", out var campaignId)
            || campaignId == Guid.Empty) return null;
        var draft = await db.Campaigns.AsNoTracking().Where(x => x.Id == campaignId && x.Status == "DRAFT"
            && x.AuthorityStatus == "UNVERIFIED" && x.AdvertiserOpportunity != null
            && x.AdvertiserOpportunity.Status == "DRAFT" && x.AdvertiserOpportunity.AuthorityStatus == "UNVERIFIED"
            && x.AdvertiserOpportunity.AdvertiserProgram.Status == "DRAFT"
            && x.AdvertiserOpportunity.AdvertiserProgram.ExternalProgramId == "alpha:customer-campaign-draft:v1"
            && x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == prepared.AdvertiserId)
            .Select(x => new { x.Id, x.Name, Brief = x.AdvertiserOpportunity!.Description }).SingleOrDefaultAsync(ct);
        if (draft is null || draft.Brief is null) return null;
        var digest = StaffCreativePreparationController.CampaignDigest(draft.Id, draft.Name, draft.Brief);
        if (digest != parts[2]) return null;
        var brand = await db.WeddingPlannerBrandDna.AsNoTracking().Where(x => x.WorkspaceId == workspaceId)
            .OrderByDescending(x => x.Version).Select(x => new { x.Id, x.Status }).FirstOrDefaultAsync(ct);
        if (brand is null || brand.Status != "CONFIRMED" || brand.Id != prepared.Brief.BrandDnaId) return null;
        string? recipe;
        try
        {
            using var doc = JsonDocument.Parse(prepared.Brief.ProductionBriefJson);
            recipe = doc.RootElement.GetProperty("generationRecipe").GetString();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException) { return null; }
        if (string.IsNullOrWhiteSpace(recipe) || ControlledAiService.Hash(recipe) != prepared.Brief.RecipeSha256) return null;
        var scope = AdvertiserCampaignDraftsController.ProviderConsentScope(campaignId, digest, briefId, prepared.Brief.RecipeSha256);
        var latest = await db.WeddingPlannerAuditEvents.AsNoTracking()
            .Where(e => e.AdvertiserId == prepared.AdvertiserId && e.WorkspaceId == workspaceId
                && e.Action == AdvertiserCampaignDraftsController.ProviderConsentAction && e.ActorType == "ADVERTISER" && e.Detail == scope)
            .OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        if (latest is null || latest.Outcome != "GRANTED") return null;
        var active = await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
            join receipt in db.AdvertiserRegistrationReceipts.AsNoTracking()
                on new { m.Subject, m.AdvertiserId } equals new { receipt.Subject, receipt.AdvertiserId }
            where m.AdvertiserId == prepared.AdvertiserId && m.Subject == latest.ActorLabel && m.Status == "ACTIVE"
                && receipt.ProcessingConsent && receipt.TermsAccepted
                && receipt.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                && receipt.TermsVersion == AdvertiserRegistrationController.TermsVersion
            select m.Id).AnyAsync(ct);
        return active ? new Grant(campaignId, digest, latest.Id, prepared.Brief.RecipeSha256, recipe) : null;
    }

    // Never send the local consent ID, campaign identifiers or receipt metadata to the provider.
    public static string? ProviderInput(string snapshot, Grant grant)
    {
        try
        {
            using var json = JsonDocument.Parse(snapshot);
            var root = json.RootElement;
            if (!root.TryGetProperty("consentReceiptId", out var id) || !id.TryGetGuid(out var receiptId)
                || receiptId != grant.ReceiptId || root.GetProperty("recipeSha256").GetString() != grant.RecipeDigest
                || root.GetProperty("productionRecipe").GetString() != grant.Recipe) return null;
            return JsonSerializer.Serialize(new { productionRecipe = grant.Recipe });
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return null; }
    }
}
