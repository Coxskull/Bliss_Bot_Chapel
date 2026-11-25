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
    public void Reconciliation_is_exact_post_only()
    {
        const string path = "/api/advertisers/operator-review/starter-reconcile";
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(path)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path+"/other")));
    }
}
