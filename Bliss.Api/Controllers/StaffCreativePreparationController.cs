using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using Bliss.Domain.Entities;
using Npgsql;
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
    WeddingPlannerService planner, AcademyProductionService production, WeddingPlannerBrandDnaService brands) : ControllerBase
{
    public static string CampaignDigest(Guid id, string name, string brief) =>
        ControlledAiService.Hash(id.ToString("D") + "\n" + name + "\n" + brief);

    public static bool ValidBrandAction(StaffCampaignPreparationRequest request)
    {
        if (request.BrandAction is null)
            return request.BrandFields is null && request.BrandExpectedVersion is null
                && request.BrandIdempotencyKey is null && !request.CustomerBrandApproved;
        if (request.BrandDnaId is not null || request.Family is not null || request.Headline is not null || request.Cta is not null
            || request.BrandExpectedVersion is null or < 0 || !Guid.TryParseExact(request.BrandIdempotencyKey, "D", out var key)
            || key == Guid.Empty) return false;
        return request.BrandAction switch
        {
            "SAVE_DRAFT" => request.BrandFields is not null && !request.CustomerBrandApproved,
            "CONFIRM_DRAFT" => request.BrandFields is null && request.BrandExpectedVersion > 0 && request.CustomerBrandApproved,
            _ => false
        };
    }

    // Existing staff authorization only. No workspace creation, customer consent,
    // or provider dispatch on this read. Limit to eligible current drafts.
    [HttpGet("queue")]
    public async Task<IActionResult> Queue([FromQuery] Guid? campaignId, CancellationToken ct, [FromQuery] bool includeProgress = false)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!auth.StaffCreativePreparationRequested || !StaffCreativeReadinessController.IsStaff(User, auth))
            return StatusCode(403);
        if (campaignId == Guid.Empty) return BadRequest();
        var eligible = from m in db.AdvertiserAccountMemberships.AsNoTracking()
            join receipt in db.AdvertiserRegistrationReceipts.AsNoTracking()
                on new { m.Subject, m.AdvertiserId } equals new { receipt.Subject, receipt.AdvertiserId }
            where m.Status == "ACTIVE" && receipt.ProcessingConsent && receipt.TermsAccepted
                && receipt.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                && receipt.TermsVersion == AdvertiserRegistrationController.TermsVersion
            select m.AdvertiserId;
        var rows = await db.Campaigns.AsNoTracking().Where(c => c.Status == "DRAFT" && c.AuthorityStatus == "UNVERIFIED"
            && (!campaignId.HasValue || c.Id == campaignId.Value)
            && c.AdvertiserOpportunity != null && c.AdvertiserOpportunity.Description != null
            && c.AdvertiserOpportunity.Status == "DRAFT" && c.AdvertiserOpportunity.AuthorityStatus == "UNVERIFIED"
            && c.AdvertiserOpportunity.AdvertiserProgram.Status == "DRAFT"
            && c.AdvertiserOpportunity.AdvertiserProgram.ExternalProgramId == "alpha:customer-campaign-draft:v1"
            && eligible.Contains(c.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId))
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id).Take(50)
            .Select(c => new { c.Id, c.Name, Brief = c.AdvertiserOpportunity!.Description!, AdvertiserId = c.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId }).ToListAsync(ct);
        var families = production.References().Select(x => x.NicheKey).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var progress = new Dictionary<Guid, string>();
        // Optional bounded progress readback; normal campaign listing remains unchanged.
        // These are recorded milestones, not an assertion of current consent or authorization.
        if (includeProgress)
        {
            foreach (var row in rows.Take(20))
            {
                var digest = CampaignDigest(row.Id, row.Name, row.Brief);
                var recipe = await CampaignRecipeRevision.ForCampaign(
                    from p in db.AcademyProductionBriefs.AsNoTracking()
                    join w in db.WeddingPlannerWorkspaces.AsNoTracking() on p.WorkspaceId equals w.Id
                    where w.AdvertiserId == row.AdvertiserId && w.IsPrimary && w.Status == "ACTIVE"
                    select p, row.Id, digest).Select(x => new { x.Id, x.WorkspaceId }).FirstOrDefaultAsync(ct);
                if (recipe is null) { progress[row.Id] = "RECIPE_NOT_RECORDED"; continue; }
                var run = await db.ControlledAiRuns.AsNoTracking().Where(x => x.WorkspaceId == recipe.WorkspaceId
                    && x.BriefId == recipe.Id && x.Kind == "IMAGE"
                    && x.IdempotencyKey == CampaignImageAuthorization.RunKey(recipe.Id))
                    .Select(x => new { x.Id, x.Status, x.OutputSha256 }).SingleOrDefaultAsync(ct);
                if (run is null) { progress[row.Id] = "RECIPE_RECORDED"; continue; }
                if (run.Status != "SUCCEEDED_PENDING_REVIEW") { progress[row.Id] = "IMAGE_RUN_RECORDED"; continue; }
                var candidate = await db.AcademyProductionCandidates.AsNoTracking().Where(x => x.BriefId == recipe.Id)
                    .OrderByDescending(x => x.Version).Select(x => new { x.Id, x.ImageSha256, x.IdempotencyKey }).FirstOrDefaultAsync(ct);
                if (candidate is null || candidate.ImageSha256 != run.OutputSha256 || candidate.IdempotencyKey != "ai-run-" + run.Id)
                { progress[row.Id] = "IMAGE_RECORDED"; continue; }
                var review = await db.AcademyProductionReviews.AsNoTracking().Where(x => x.CandidateId == candidate.Id)
                    .OrderByDescending(x => x.Version).Select(x => new { x.Id, x.Decision }).FirstOrDefaultAsync(ct);
                if (review is null) { progress[row.Id] = "STAFF_REVIEW_PENDING"; continue; }
                if (review.Decision == "REVISE") { progress[row.Id] = "STAFF_REVISION_RECORDED"; continue; }
                if (review.Decision != "PASS") { progress[row.Id] = "STAFF_REVIEW_RECORDED"; continue; }
                var approval = await db.CandidateApprovalDecisions.AsNoTracking().Where(x => x.CandidateId == candidate.Id
                    && x.ScopeKey == "ADVERTISER").OrderByDescending(x => x.Version)
                    .Select(x => new { x.ReviewId, x.Decision }).FirstOrDefaultAsync(ct);
                progress[row.Id] = approval?.ReviewId != review.Id ? "ADVERTISER_REVIEW_PENDING"
                    : approval.Decision == "WITHHOLD" ? "ADVERTISER_REVISION_RECORDED"
                    : approval.Decision == "APPROVE" ? "ADVERTISER_APPROVAL_RECORDED" : "ADVERTISER_REVIEW_PENDING";
            }
        }
        return Ok(new { campaigns = rows.Select(x => new { campaignId = x.Id, campaignName = x.Name,
            creativeBrief = x.Brief, briefSha256 = CampaignDigest(x.Id, x.Name, x.Brief),
            creativeMilestone = progress.GetValueOrDefault(x.Id, "NOT_LOADED") }),
            families, dispatchAuthorized = false, release = "HELD", delivery = "NOT_SENT" });
    }

    public const string StarterCheckoutIssued = "STARTER_CHECKOUT_INSTRUCTIONS_ISSUED";
    // Exact existing live Stripe link, verified against merchant and line items.
    // A reusable link is not a single-use payment authorization or payer identity.
    public static string CheckoutInstructions(Guid offerId) =>
        "https://buy.stripe.com/14A00j8fN6El79Q8Df0Jq00?client_reference_id=" + offerId.ToString("D");

    [HttpPost("starter-checkout")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> StarterCheckout([FromBody] StaffStarterCheckoutRequest request,CancellationToken ct)
    {
        Response.Headers.CacheControl="private, no-store";
        if(!auth.StarterServiceOfferRequested || !auth.StarterCheckoutInstructionsRequested
            || !auth.StarterPaymentReconciliationRequested || !auth.StaffCreativePreparationRequested
            || !StaffCreativeReadinessController.IsStaff(User,auth))return StatusCode(403);
        if(request.CampaignId==Guid.Empty || request.OfferId==Guid.Empty
            || !request.MerchantPriceAndTaxVerified || !request.NoPriorOrOpenPaymentVerified)
            return BadRequest(new{error="Review current merchant, price, tax and prior/open checkouts before issuing payment instructions."});
        var subject=User.FindFirst("sub")!.Value;
        try{
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var accepted=await StarterServiceOffer.CurrentAccepted(db,request.CampaignId,request.OfferId,ct);
            if(accepted is null)return Conflict(new{error="Current customer-accepted service offer required."});
            await using(var cmd=db.Database.GetDbConnection().CreateCommand()){
                cmd.Transaction=tx.GetDbTransaction();cmd.CommandTimeout=10;
                cmd.CommandText="SELECT EXISTS(SELECT 1 FROM public.alpha_starter_stripe_receipts WHERE lower(quote_reference) IN (@d,@n))";
                foreach(var (name,value) in new[]{("d",request.OfferId.ToString("D")),("n",request.OfferId.ToString("N"))}){
                    var param=cmd.CreateParameter();param.ParameterName=name;param.DbType=DbType.String;param.Value=value;cmd.Parameters.Add(param);
                }
                if(await cmd.ExecuteScalarAsync(ct) is not false)return Conflict(new{error="A signed payment notification already exists. Reconcile instead of issuing checkout."});
            }
            var prefix="quote:"+request.OfferId.ToString("N")+":";
            if(await db.WeddingPlannerAuditEvents.AnyAsync(x=>x.Action==AdvertiserOperatorReviewController.StarterReconciliationAction
                && x.Detail!=null && x.Detail.StartsWith(prefix),ct))return Conflict(new{error="Order already reconciled."});
            var id=StarterArtworkDelivery.StableId(StarterCheckoutIssued,request.OfferId.ToString("N"));
            var detail=request.OfferId.ToString("N")+":"+request.CampaignId.ToString("N")+":"+StarterStripeReceipt.PaymentLinkId;
            if(await db.WeddingPlannerAuditEvents.AnyAsync(x=>x.Id==id,ct))
                return Conflict(new{error="Payment instructions already issued. Do not issue another; investigate original checkout in Stripe."});
            db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent{Id=id,AdvertiserId=accepted.AdvertiserId,WorkspaceId=accepted.WorkspaceId,
                Action=StarterCheckoutIssued,ActorType="OPERATOR",ActorLabel=subject,Outcome="ISSUED",Detail=detail,OccurredAt=DateTime.UtcNow});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
            return Ok(new{campaignId=request.CampaignId,offerId=request.OfferId,instructionsId=id,
                paymentUrl=CheckoutInstructions(request.OfferId),amountCents=14900,currency="usd",
                status="INSTRUCTIONS_ISSUED",paymentInitiated=false,fulfillmentAuthorized=false,release="HELD"});
        }catch(PostgresException pg) when(pg.SqlState==PostgresErrorCodes.SerializationFailure || pg.SqlState==PostgresErrorCodes.UniqueViolation){return Conflict();}
        catch(DbUpdateException){return Conflict();}
        catch(System.Data.Common.DbException){return StatusCode(503);}
    }

    [HttpPost("starter-offer")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> Offer([FromBody] StaffStarterOfferRequest request,CancellationToken ct)
    {
        Response.Headers.CacheControl="private, no-store";
        if(!auth.StarterServiceOfferRequested || !auth.StaffCreativePreparationRequested || !StaffCreativeReadinessController.IsStaff(User,auth))return StatusCode(403);
        if(request.CampaignId==Guid.Empty || request.WorkspaceId==Guid.Empty || request.BriefSha256 is not {Length:64}
            || request.Scope is not {Length:>0 and <=1000} || request.Terms is not {Length:>0 and <=700}
            || !request.ScopeAndTermsApproved || !request.TaxTreatmentApproved)return BadRequest();
        var terms=new StarterServiceOffer.Terms(request.CampaignId,request.BriefSha256,request.Scope.Trim(),request.Terms.Trim(),14900,"usd");
        var detail=StarterServiceOffer.Serialize(terms);
        if(StarterServiceOffer.Parse(detail) is null)return BadRequest(new{error="Complete scope and terms required (max 2000 serialized characters)."});
        var subject=User.FindFirst("sub")!.Value;
        try{
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var campaign=await db.Campaigns.AsNoTracking().Where(x=>x.Id==request.CampaignId && x.Status=="DRAFT" && x.AuthorityStatus=="UNVERIFIED"
                && x.AdvertiserOpportunity!=null && x.AdvertiserOpportunity.Status=="DRAFT" && x.AdvertiserOpportunity.AuthorityStatus=="UNVERIFIED"
                && x.AdvertiserOpportunity.AdvertiserProgram.Status=="DRAFT"
                && x.AdvertiserOpportunity.AdvertiserProgram.ExternalProgramId=="alpha:customer-campaign-draft:v1")
                .Select(x=>new{x.Name,Brief=x.AdvertiserOpportunity!.Description,AdvertiserId=x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId}).SingleOrDefaultAsync(ct);
            if(campaign?.Brief is null || CampaignDigest(request.CampaignId,campaign.Name,campaign.Brief)!=request.BriefSha256
                || !await StarterServiceOffer.ActiveMember(db,campaign.AdvertiserId,null,ct)
                || !await db.WeddingPlannerWorkspaces.AnyAsync(x=>x.Id==request.WorkspaceId && x.AdvertiserId==campaign.AdvertiserId && x.IsPrimary && x.Status=="ACTIVE",ct))return Conflict();
            var id=StarterServiceOffer.OfferId(request.CampaignId,request.BriefSha256);
            var existing=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
            if(existing is not null && (existing.Action!=StarterServiceOffer.OfferAction || existing.Detail!=detail
                || existing.AdvertiserId!=campaign.AdvertiserId || existing.WorkspaceId!=request.WorkspaceId))return Conflict(new{error="Existing immutable offer differs; do not overwrite."});
            if(existing is null){
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent{Id=id,AdvertiserId=campaign.AdvertiserId,WorkspaceId=request.WorkspaceId,
                    Action=StarterServiceOffer.OfferAction,ActorType="OPERATOR",ActorLabel=subject,Outcome="OFFERED",Detail=detail,OccurredAt=DateTime.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new{campaignId=request.CampaignId,offerId=id,offerSha256=ControlledAiService.Hash(detail),amountCents=14900,currency="usd",status="OFFERED",paymentAuthorized=false,release="HELD"});
        }catch(PostgresException pg) when(pg.SqlState==PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
    }

    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Prepare([FromBody] StaffCampaignPreparationRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.StaffCreativePreparationRequested || !StaffCreativeReadinessController.IsStaff(User, auth))
            return StatusCode(403);
        if (request.CampaignId == Guid.Empty || request.BriefSha256 is not { Length: 64 }
            || request.BriefSha256.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            || !request.OperatorReviewedCampaign)
            return BadRequest(new { error = "Exact campaign digest and explicit staff review required." });
        if (!ValidBrandAction(request))
            return BadRequest(new { error = "Invalid brand action. Confirmation requires explicit customer brand approval." });
        // A saved recipe is optional; if requested, all inputs must be provided.
        var saveRecipe = request.BrandDnaId.HasValue;
        if(request.RevisionReviewId.HasValue && (!saveRecipe || request.RevisionReviewId==Guid.Empty || request.PreviousBriefId is null || request.PreviousBriefId==Guid.Empty)
            || !request.RevisionReviewId.HasValue && request.PreviousBriefId.HasValue)
            return BadRequest(new {error="Exact previous brief and saved revision review required."});
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
                .OrderByDescending(b => b.Version).FirstOrDefaultAsync(ct);
            if (request.BrandAction is not null)
            {
                var confirm = request.BrandAction == "CONFIRM_DRAFT";
                var revision = await brands.AppendAsync(workspace, request.BrandFields, request.BrandExpectedVersion!.Value,
                    request.BrandIdempotencyKey, confirm, "OPERATOR", subject, RequestCorrelation.Resolve(HttpContext), ct);
                brand = await db.WeddingPlannerBrandDna.AsNoTracking().Where(b => b.WorkspaceId == workspace.WorkspaceId)
                    .OrderByDescending(b => b.Version).FirstOrDefaultAsync(ct);
                if (brand is null || brand.Id != revision.Revision.Id)
                    return Conflict(new { error = "Brand changed after the requested revision. Refresh before continuing." });
            }
            Guid? productionBriefId = null;
            string? recipeSha256 = null;
            string? recipeFamily = null, recipeHeadline = null, recipeCta = null;
            if (saveRecipe)
            {
                if (brand is null || brand.Status != "CONFIRMED" || brand.Id != request.BrandDnaId)
                    return Conflict(new { error = "Latest confirmed brand DNA required; no recipe created." });
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
                var current=await CampaignRecipeRevision.ForCampaign(db.AcademyProductionBriefs.AsNoTracking()
                    .Where(x=>x.WorkspaceId==workspace.WorkspaceId),draft.Id,request.BriefSha256).FirstOrDefaultAsync(ct);
                var key=CampaignRecipeRevision.OriginalKey(draft.Id,request.BriefSha256);
                AcademyProductionReview? revision=null;
                if(request.RevisionReviewId is Guid reviewId){
                    key=CampaignRecipeRevision.Key(draft.Id,request.BriefSha256,reviewId);
                    // Permit exact replays only; never let a stale review fork a newer recipe.
                    var source=await db.AcademyProductionBriefs.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==request.PreviousBriefId
                        && x.WorkspaceId==workspace.WorkspaceId && x.BrandDnaId==brand.Id,ct);
                    if(source is null || !CampaignRecipeRevision.Matches(source.IdempotencyKey,draft.Id,request.BriefSha256)
                        || current is null || (current.Id!=source.Id && current.IdempotencyKey!=key))
                        return Conflict(new{error="The selected review is no longer the current recipe."});
                    var candidate=await db.AcademyProductionCandidates.AsNoTracking().Where(x=>x.BriefId==source.Id)
                        .OrderByDescending(x=>x.Version).FirstOrDefaultAsync(ct);
                    if(candidate is null)return Conflict();
                    revision=await db.AcademyProductionReviews.AsNoTracking().Where(x=>x.CandidateId==candidate.Id)
                        .OrderByDescending(x=>x.Version).FirstOrDefaultAsync(ct);
                    if(revision?.Id!=reviewId || revision.Decision!="REVISE" || string.IsNullOrWhiteSpace(revision.Repair))
                        return Conflict(new{error="Latest saved REVISE decision required."});
                    var generated=await db.ControlledAiRuns.AsNoTracking().AnyAsync(x=>x.WorkspaceId==workspace.WorkspaceId && x.BriefId==source.Id
                        && x.Kind=="IMAGE" && x.Status=="SUCCEEDED_PENDING_REVIEW" && x.CostStatus=="PROVIDER_REPORTED"
                        && x.OutputSha256==candidate.ImageSha256 && x.IdempotencyKey==CampaignImageAuthorization.RunKey(source.Id),ct);
                    if(!generated)return Conflict(new{error="Verified generated candidate required."});
                    if(await db.WeddingPlannerAuditEvents.AnyAsync(x=>x.WorkspaceId==workspace.WorkspaceId
                        && x.Action==StarterArtworkDelivery.ReleaseAction && x.Detail!=null && x.Detail.Contains(":"+candidate.Id.ToString("N")+":"),ct))
                        return Conflict(new{error="Artwork already released; use a separate order."});
                }else if(current is not null && current.IdempotencyKey!=key)
                    return Conflict(new{error="Use the latest reviewed revision instead of overwriting the original recipe."});
                var result = await production.CreateAsync(workspace,
                    new CreateProductionBrief(brand.Id, request.Family!, request.Headline!, request.Cta!, key),
                    "OPERATOR", subject, RequestCorrelation.Resolve(HttpContext), ct,revision);
                productionBriefId = result.Row.Id;
                recipeSha256 = result.Row.RecipeSha256;
                recipeFamily = result.Row.Family; recipeHeadline = result.Row.Headline; recipeCta = result.Row.Cta;
                await tx.CommitAsync(ct);
            }
            if (!saveRecipe && brand?.Status == "CONFIRMED")
            {
                var existingRecipe = await CampaignRecipeRevision.ForCampaign(db.AcademyProductionBriefs.AsNoTracking()
                    .Where(x=>x.WorkspaceId==workspace.WorkspaceId),draft.Id,request.BriefSha256)
                    .Where(x=>x.BrandDnaId==brand.Id).Select(x => new { x.Id, x.RecipeSha256, x.Family, x.Headline, x.Cta }).FirstOrDefaultAsync(ct);
                productionBriefId = existingRecipe?.Id;
                recipeSha256 = existingRecipe?.RecipeSha256;
                recipeFamily = existingRecipe?.Family; recipeHeadline = existingRecipe?.Headline; recipeCta = existingRecipe?.Cta;
            }
            object? currentOrder = null;
            if (auth.StarterServiceOfferRequested)
            {
                var offerId = StarterServiceOffer.OfferId(draft.Id,request.BriefSha256);
                var offer = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==offerId
                    && x.WorkspaceId==workspace.WorkspaceId && x.Action==StarterServiceOffer.OfferAction
                    && x.ActorType=="OPERATOR" && x.Outcome=="OFFERED",ct);
                var terms=StarterServiceOffer.Parse(offer?.Detail);
                if (offer is not null && terms is not null && terms.CampaignId==draft.Id
                    && terms.CampaignDigest==request.BriefSha256)
                {
                    var accepted=await StarterServiceOffer.CurrentAccepted(db,draft.Id,offerId,ct);
                    var milestone=accepted is null
                        ? await db.WeddingPlannerAuditEvents.AnyAsync(x=>x.Id==StarterServiceOffer.AcceptanceId(offerId),ct)
                            ? "REVIEW_REQUIRED" : "AWAITING_ACCEPTANCE"
                        : await StarterServiceOffer.OrderProgress(db,accepted,draft.Id,ct);
                    currentOrder=new {offerId,offerSha256=ControlledAiService.Hash(offer.Detail!),scope=terms.Scope,
                        terms=terms.TermsText,amountCents=14900,currency="usd",milestone};
                }
            }
            return Ok(new { campaignId = draft.Id, briefSha256 = request.BriefSha256,
                workspaceId = workspace.WorkspaceId, brandDnaId = brand?.Id,currentOrder,
                brandStatus = brand?.Status ?? "NOT_PREPARED", productionBriefId, recipeSha256,
                recipeFields = productionBriefId.HasValue ? new { family = recipeFamily, headline = recipeHeadline, cta = recipeCta } : null,
                brandVersion = brand?.Version ?? 0,
                brandFields = brand is null ? null : new BrandDnaFields(brand.BrandName, brand.Niche, brand.Market,
                    brand.Language, brand.Colors, brand.Font, brand.Goals, brand.Restrictions),
                preparation = saveRecipe ? "RECIPE_SAVED" : request.BrandAction == "SAVE_DRAFT" ? "BRAND_DRAFT_SAVED"
                    : request.BrandAction == "CONFIRM_DRAFT" ? "BRAND_CONFIRMED" : "WORKSPACE_READY",
                customerProviderConsent = "NOT_COLLECTED", billingAuthorization = "NOT_GRANTED",
                providerCall = "NOT_REQUESTED", dispatchAuthorized = false, release = "HELD", delivery = "NOT_SENT" });
        }
        catch (WeddingPlannerNotFoundException) { return NotFound(); }
        catch (WeddingPlannerForbiddenException) { return StatusCode(403); }
        catch (WeddingPlannerConflictException) { return Conflict(new { error = "Preparation conflicts with existing state. Refresh before retrying." }); }
        catch (PostgresException pg) when(pg.SqlState==PostgresErrorCodes.SerializationFailure || pg.SqlState==PostgresErrorCodes.UniqueViolation)
        { return Conflict(new { error = "Preparation changed concurrently. Refresh before retrying." }); }
        catch (DbUpdateException) { return Conflict(new { error = "Preparation changed concurrently. Refresh before retrying." }); }
        catch (InvalidOperationException) { return BadRequest(new { error = "Production preparation requirements not met." }); }
    }
}

public sealed record StaffCampaignPreparationRequest(Guid CampaignId, string BriefSha256,
    bool OperatorReviewedCampaign, Guid? BrandDnaId, string? Family, string? Headline, string? Cta,
    string? BrandAction = null, BrandDnaFields? BrandFields = null, int? BrandExpectedVersion = null,
    string? BrandIdempotencyKey = null, bool CustomerBrandApproved = false,
    Guid? PreviousBriefId = null,Guid? RevisionReviewId = null);

public sealed record StaffStarterOfferRequest(Guid CampaignId,string BriefSha256,Guid WorkspaceId,string Scope,string Terms,bool ScopeAndTermsApproved,bool TaxTreatmentApproved);

public sealed record StaffStarterCheckoutRequest(Guid CampaignId,Guid OfferId,bool MerchantPriceAndTaxVerified,bool NoPriorOrOpenPaymentVerified);
