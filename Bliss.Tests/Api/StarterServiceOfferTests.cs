using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;
namespace Bliss.Tests.Api;
public sealed class StarterServiceOfferTests
{
    [Fact]
    public void Offer_is_campaign_bound_and_fixed_price()
    {
        var id=Guid.NewGuid();var sha=new string('a',64);
        var terms=new StarterServiceOffer.Terms(id,sha,"Scope","Terms",14900,"usd");
        Assert.Equal(terms,StarterServiceOffer.Parse(StarterServiceOffer.Serialize(terms)));
        Assert.Null(StarterServiceOffer.Parse(StarterServiceOffer.Serialize(terms with {AmountCents=1})));
        Assert.NotEqual(StarterServiceOffer.OfferId(id,sha),StarterServiceOffer.OfferId(id,new string('b',64)));
    }
    [Fact]
    public void Staff_offer_is_exact_post_only()
    {
        const string path="/api/internal/creative-preparation/starter-offer";
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path)));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(path),staffCreativePreparationEnabled:true));
    }
    [Fact]
    public void Order_progress_requires_exact_tenant_offer_and_campaign_evidence()
    {
        var accepted = new StarterServiceOffer.Accepted(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var campaign = Guid.NewGuid();
        var action = Bliss.Api.Controllers.StaffCreativePreparationController.StarterCheckoutIssued;
        var instructions = new Bliss.Domain.Entities.WeddingPlannerAuditEvent
        {
            Id = StarterArtworkDelivery.StableId(action, accepted.VersionId.ToString("N")),
            AdvertiserId = accepted.AdvertiserId, WorkspaceId = accepted.WorkspaceId,
            ActorType = "OPERATOR", Action = action, Outcome = "ISSUED",
            Detail = $"{accepted.VersionId:N}:{campaign:N}:{StarterStripeReceipt.PaymentLinkId}"
        };
        var digest = new string('a',64);
        var reconciliation = new Bliss.Domain.Entities.WeddingPlannerAuditEvent
        {
            Id = new Guid(Convert.FromHexString(digest[..32])),
            AdvertiserId = accepted.AdvertiserId, WorkspaceId = accepted.WorkspaceId,
            ActorType = "OPERATOR", Action = Bliss.Api.Controllers.AdvertiserOperatorReviewController.StarterReconciliationAction,
            Outcome = "RECONCILED", RequestId = "evt_fixture",
            Detail = $"quote:{accepted.VersionId:N}:{campaign:N}:{digest}"
        };
        Assert.Equal("AWAITING_PAYMENT_INSTRUCTIONS", StarterServiceOffer.RecordedProgress(accepted,campaign,null,[]));
        Assert.Equal("PAYMENT_INSTRUCTIONS_RECORDED", StarterServiceOffer.RecordedProgress(accepted,campaign,instructions,[]));
        Assert.Equal("RECONCILIATION_RECORDED", StarterServiceOffer.RecordedProgress(accepted,campaign,instructions,[reconciliation]));
        Assert.Equal("REVIEW_REQUIRED", StarterServiceOffer.RecordedProgress(accepted,Guid.NewGuid(),instructions,[]));
        Assert.Equal("REVIEW_REQUIRED", StarterServiceOffer.RecordedProgress(accepted,campaign,instructions,[reconciliation,reconciliation]));
        reconciliation.AdvertiserId = Guid.NewGuid();
        Assert.Equal("REVIEW_REQUIRED", StarterServiceOffer.RecordedProgress(accepted,campaign,instructions,[reconciliation]));
        instructions.WorkspaceId = Guid.NewGuid();
        Assert.Equal("REVIEW_REQUIRED", StarterServiceOffer.RecordedProgress(accepted,campaign,instructions,[]));
    }
}