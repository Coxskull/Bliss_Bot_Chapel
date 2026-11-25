using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

// Reuse the existing primary workspace and production brief service. No provider
// consent, financial authorization, queued job or release is implied.
[ApiController]
[Authorize]
[Route("api/internal/creative-preparation")]
public sealed class StaffCreativePreparationController(
    BlissDbContext db, BlissAuthenticationOptions auth,
    WeddingPlannerService planner, AcademyProductionService production) : ControllerBase
{
    public static string CampaignDigest(Guid id, string name, string brief) =>
        ControlledAiService.Hash(id.ToString("D") + "\n" + name + "\n" + brief);

    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Prepare([FromBody] StaffCampaignPreparationRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.StaffCreativePreparationRequested || !StaffCreativeReadinessController.IsStaff(User, auth))
            return StatusCode(403);
        if (request.CampaignId == Guid.Empty || request.BriefSha256 is not { Length: 64 }
            || request.BriefSha256.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            || !request.OperatorReviewedCampaign)
            return BadRequest(new { error = "Exact campaign digest and explicit staff review required." });
        // A saved recipe is optional; if requested, all inputs must be provided.
        var saveRecipe = request.BrandDnaId.HasValue;
        if (saveRecipe ? request.BrandDnaId == Guid.Empty || string.IsNullOrWhiteSpace(request.Family)
            || string.IsNullOrWhiteSpace(request.Headline) || string.IsNullOrWhiteSpace(request.Cta)
            : request.Family is not null || request.Headline is not null || request.Cta is not null)
            return BadRequest(new { error = "Provide an existing confirmed brand and complete production copy, or prepare only the workspace." });
        var draft = await db.Campaigns.AsNoTracking().Where(c => c.Id == request.CampaignId
            && c.Status == "DRAFT" && c.AuthorityStatus == "UNVERIFIED"
            && c.AdvertiserOpportunity != null
            && c.AdvertiserOpportunity.Status == "DRAFT" && c.AdvertiserOpportunity.AuthorityStatus == "UNVERIFIED"
            && c.AdvertiserOpportunity.AdvertiserProgram.Status == "DRAFT"
            && c.AdvertiserOpportunity.AdvertiserProgram.ExternalProgramId == "alpha:customer-campaign-draft:v1")
            .Select(c => new { c.Id, c.Name, Brief = c.AdvertiserOpportunity!.Description,
                AdvertiserId = c.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId })
            .SingleOrDefaultAsync(ct);
        if (draft is null || draft.Brief is null
            || CampaignDigest(draft.Id, draft.Name, draft.Brief) != request.BriefSha256)
            return NotFound(new { error = "Matching current campaign draft not found." });
        var eligible = await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
            join r in db.AdvertiserRegistrationReceipts.AsNoTracking()
                on new { m.Subject, m.AdvertiserId } equals new { r.Subject, r.AdvertiserId }
            where m.AdvertiserId == draft.AdvertiserId && m.Status == "ACTIVE"
                && r.ProcessingConsent && r.TermsAccepted
                && r.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                && r.TermsVersion == AdvertiserRegistrationController.TermsVersion
            select m.Id).AnyAsync(ct);
        if (!eligible) return Conflict(new { error = "Active membership with current registration consent required." });
        var subject = User.FindFirst("sub")!.Value;
        try
        {
            var workspace = await planner.OpenPrimaryWorkspaceAsync(draft.AdvertiserId,
                "STAFF_CAMPAIGN_PREPARATION", draft.AdvertiserId.ToString("N"), true,
                null, "OPERATOR", subject, RequestCorrelation.Resolve(HttpContext), ct);
            if (workspace.AdvertiserId != draft.AdvertiserId || workspace.Status != "ACTIVE" || !workspace.IsPrimary)
                return Conflict(new { error = "Active primary workspace required." });
            var brand = await db.WeddingPlannerBrandDna.AsNoTracking()
                .Where(b => b.WorkspaceId == workspace.WorkspaceId)
                .OrderByDescending(b => b.Version).Select(b => new { b.Id, b.Status }).FirstOrDefaultAsync(ct);
            Guid? productionBriefId = null;
            string? recipeSha256 = null;
            if (saveRecipe)
            {
                if (brand is null || brand.Status != "CONFIRMED" || brand.Id != request.BrandDnaId)
                    return Conflict(new { error = "Latest confirmed brand DNA required; no recipe created." });
                // A distinct campaign digest identifies one immutable preparation.
                // Existing service rejects same-key changed-input requests.
                var key = "campaign:" + draft.Id.ToString("N") + ":" + request.BriefSha256;
                var result = await production.CreateAsync(workspace,
                    new CreateProductionBrief(brand.Id, request.Family!, request.Headline!, request.Cta!, key),
                    "OPERATOR", subject, RequestCorrelation.Resolve(HttpContext), ct);
                productionBriefId = result.Row.Id;
                recipeSha256 = result.Row.RecipeSha256;
            }
            return Ok(new { campaignId = draft.Id, briefSha256 = request.BriefSha256,
                workspaceId = workspace.WorkspaceId, brandDnaId = brand?.Id,
                brandStatus = brand?.Status ?? "NOT_PREPARED", productionBriefId, recipeSha256,
                preparation = saveRecipe ? "RECIPE_SAVED" : "WORKSPACE_READY",
                customerProviderConsent = "NOT_COLLECTED", billingAuthorization = "NOT_GRANTED",
                providerCall = "NOT_REQUESTED", dispatchAuthorized = false, release = "HELD", delivery = "NOT_SENT" });
        }
        catch (WeddingPlannerNotFoundException) { return NotFound(); }
        catch (WeddingPlannerForbiddenException) { return StatusCode(403); }
        catch (WeddingPlannerConflictException) { return Conflict(new { error = "Preparation conflicts with existing state. Refresh before retrying." }); }
        catch (DbUpdateException) { return Conflict(new { error = "Preparation changed concurrently. Refresh before retrying." }); }
        catch (InvalidOperationException) { return BadRequest(new { error = "Production preparation requirements not met." }); }
    }
}

public sealed record StaffCampaignPreparationRequest(Guid CampaignId, string BriefSha256,
    bool OperatorReviewedCampaign, Guid? BrandDnaId, string? Family, string? Headline, string? Cta);
