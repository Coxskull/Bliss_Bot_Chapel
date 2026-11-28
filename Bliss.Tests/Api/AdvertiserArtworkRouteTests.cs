using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;
public sealed class AdvertiserArtworkRouteTests
{
    [Fact]
    public void Artwork_route_is_exact_post_only()
    {
        const string path = "/api/advertisers/my-campaign-drafts/creative-artwork";
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(path)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(path+"/other")));
    }
}
