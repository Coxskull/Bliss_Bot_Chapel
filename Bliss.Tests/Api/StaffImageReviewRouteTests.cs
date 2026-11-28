using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;
public sealed class StaffImageReviewRouteTests
{
    [Fact]
    public void Review_is_exact_staff_post_only()
    {
        const string route = "/api/internal/creative-image-review";
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route)));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(route),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route+"/other"),staffCreativePreparationEnabled:true));
    }
}
