using Bliss.Api.Operations;
namespace Bliss.Tests.Api;
public sealed class StarterArtworkDeliveryTests
{
    [Fact]
    public void Binding_changes_with_approval_and_artwork()
    {
        var campaign=Guid.NewGuid();var candidate=Guid.NewGuid();var review=Guid.NewGuid();var approval=Guid.NewGuid();var hash=new string('a',64);
        var exact=StarterArtworkDelivery.Suffix(campaign,candidate,review,approval,hash);
        Assert.NotEqual(exact,StarterArtworkDelivery.Suffix(campaign,candidate,review,Guid.NewGuid(),hash));
        Assert.NotEqual(exact,StarterArtworkDelivery.Suffix(campaign,candidate,review,approval,new string('b',64)));
        Assert.NotEqual(StarterArtworkDelivery.StableId("RELEASE",exact),StarterArtworkDelivery.StableId("DOWNLOAD",exact));
    }
    [Fact]
    public void Original_signed_receipt_must_match_reconciled_order_exactly()
    {
        var version = Guid.NewGuid();
        var campaign = Guid.NewGuid();
        var receipt = new StarterStripeReceipt("evt_fixture", "cs_live_fixture", "pi_fixture",
            version.ToString("D"), 14900, "usd", "checkout.session.completed");
        var digest = ControlledAiService.Hash(receipt.SessionId + "\n" + receipt.PaymentIntentId);
        var audit = new Bliss.Domain.Entities.WeddingPlannerAuditEvent
        {
            Id = new Guid(Convert.FromHexString(digest[..32])),
            RequestId = receipt.EventId,
            Action = Bliss.Api.Controllers.AdvertiserOperatorReviewController.StarterReconciliationAction,
            ActorType = "OPERATOR", Outcome = "RECONCILED",
            Detail = $"quote:{version:N}:{campaign:N}:{digest}"
        };
        bool Matches(StarterStripeReceipt input) =>
            StarterArtworkDelivery.MatchesSignedReceipt(audit, version, campaign, input);
        Assert.True(Matches(receipt));
        Assert.True(Matches(receipt with { QuoteReference = version.ToString("N") }));
        Assert.True(Matches(receipt with { EventType = "checkout.session.async_payment_succeeded" }));
        Assert.False(Matches(receipt with { EventId = "evt_other" }));
        Assert.False(Matches(receipt with { SessionId = "cs_live_other" }));
        Assert.False(Matches(receipt with { SessionId = "cs_test_fixture" }));
        Assert.False(Matches(receipt with { PaymentIntentId = "pi_other" }));
        Assert.False(Matches(receipt with { QuoteReference = null }));
        Assert.False(Matches(receipt with { QuoteReference = Guid.NewGuid().ToString("D") }));
        Assert.False(Matches(receipt with { AmountCents = 14899 }));
        Assert.False(Matches(receipt with { Currency = "eur" }));
        Assert.False(Matches(receipt with { EventType = "charge.refunded" }));
        Assert.False(StarterArtworkDelivery.MatchesSignedReceipt(audit, version, Guid.NewGuid(), receipt));
        audit.Id = Guid.NewGuid();
        Assert.False(Matches(receipt));
    }
    [Fact]
    public void Download_history_requires_exact_release_and_customer_identity()
    {
        var advertiser = Guid.NewGuid();
        var workspace = Guid.NewGuid();
        var release = Guid.NewGuid();
        const string subject = "customer-fixture";
        var row = new Bliss.Domain.Entities.WeddingPlannerAuditEvent
        {
            Id = StarterArtworkDelivery.DownloadRequestId(release, subject),
            AdvertiserId = advertiser, WorkspaceId = workspace,
            Action = StarterArtworkDelivery.DownloadAction,
            ActorType = "ADVERTISER", ActorLabel = subject,
            Outcome = "REQUESTED", Detail = release.ToString("N")
        };
        Assert.True(StarterArtworkDelivery.MatchesDownloadRequest(row,advertiser,workspace,release,subject));
        Assert.False(StarterArtworkDelivery.MatchesDownloadRequest(row,Guid.NewGuid(),workspace,release,subject));
        Assert.False(StarterArtworkDelivery.MatchesDownloadRequest(row,advertiser,Guid.NewGuid(),release,subject));
        Assert.False(StarterArtworkDelivery.MatchesDownloadRequest(row,advertiser,workspace,Guid.NewGuid(),subject));
        Assert.False(StarterArtworkDelivery.MatchesDownloadRequest(row,advertiser,workspace,release,"other"));
        row.Outcome = "DELIVERED";
        Assert.False(StarterArtworkDelivery.MatchesDownloadRequest(row,advertiser,workspace,release,subject));
    }
}
