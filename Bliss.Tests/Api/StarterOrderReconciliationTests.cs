using Bliss.Api.Controllers;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;
public sealed class StarterOrderReconciliationTests
{
    [Fact]
    public void Reference_must_identify_exact_immutable_quote_version()
    {
        var id = Guid.NewGuid();
        Assert.True(AdvertiserOperatorReviewController.ExactQuoteReference(id.ToString("D"),id));
        Assert.True(AdvertiserOperatorReviewController.ExactQuoteReference(id.ToString("N"),id));
        Assert.False(AdvertiserOperatorReviewController.ExactQuoteReference(null,id));
        Assert.False(AdvertiserOperatorReviewController.ExactQuoteReference(Guid.NewGuid().ToString("D"),id));
        Assert.False(AdvertiserOperatorReviewController.ExactQuoteReference("starter",id));
    }
    [Fact]
    public void Readback_requires_exact_durable_audit_and_keeps_unknown_evidence_held()
    {
        var version = Guid.NewGuid();
        var campaign = Guid.NewGuid();
        const string eventId = "evt_fixture";
        var digest = AdvertiserOperatorReviewController.StarterReceiptDigest("cs_live_fixture", "pi_fixture");
        var audit = new Bliss.Domain.Entities.WeddingPlannerAuditEvent
        {
            Id = AdvertiserOperatorReviewController.StarterReceiptKey(digest),
            Action = AdvertiserOperatorReviewController.StarterReconciliationAction,
            Outcome = "RECONCILED", RequestId = eventId,
            Detail = $"quote:{version:N}:{campaign:N}:{digest}"
        };
        string Status(string? reference, string evt, Bliss.Domain.Entities.WeddingPlannerAuditEvent? entry) =>
            AdvertiserOperatorReviewController.StarterReceiptStatus(reference, evt, digest, entry);
        Assert.Equal("SIGNED_NOTIFICATION_PENDING_ORDER_RECONCILIATION", Status(version.ToString("D"), eventId, null));
        Assert.Equal("RECONCILED_BY_OPERATOR", Status(version.ToString("D"), eventId, audit));
        Assert.Equal("RECONCILED_BY_OPERATOR", Status(version.ToString("N"), eventId, audit));
        Assert.Equal("RECONCILIATION_REVIEW_REQUIRED", Status(Guid.NewGuid().ToString(), eventId, audit));
        Assert.Equal("RECONCILIATION_REVIEW_REQUIRED", Status(version.ToString(), "evt_other", audit));
        audit.Detail = $"quote:{version:N}:{campaign:N}:wrong";
        Assert.Equal("RECONCILIATION_REVIEW_REQUIRED", Status(version.ToString(), eventId, audit));
    }

    [Fact]
    public void Reconciliation_is_exact_post_only()
    {
        const string path = "/api/advertisers/operator-review/starter-reconcile";
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(path)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path+"/other")));
    }
}
