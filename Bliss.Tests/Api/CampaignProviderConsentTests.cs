using Bliss.Api.Controllers;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;
public sealed class CampaignProviderConsentTests
{
    [Fact]
    public void Scope_is_bound_to_campaign_and_exact_recipe()
    {
        var campaign = Guid.NewGuid(); var recipe = Guid.NewGuid();
        var digest = new string('a',64); var recipeDigest = new string('b',64);
        var scope = AdvertiserCampaignDraftsController.ProviderConsentScope(campaign,digest,recipe,recipeDigest);
        Assert.Equal(64,scope.Length);
        Assert.NotEqual(scope,AdvertiserCampaignDraftsController.ProviderConsentScope(Guid.NewGuid(),digest,recipe,recipeDigest));
        Assert.NotEqual(scope,AdvertiserCampaignDraftsController.ProviderConsentScope(campaign,digest,Guid.NewGuid(),recipeDigest));
        Assert.NotEqual(scope,AdvertiserCampaignDraftsController.ProviderConsentScope(campaign,digest,recipe,new string('c',64)));
    }
    [Fact]
    public void Restricted_route_is_exact_post_only()
    {
        const string route = "/api/advertisers/my-campaign-drafts/creative-provider-consent";
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(route)));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route+"/other")));
    }
}
