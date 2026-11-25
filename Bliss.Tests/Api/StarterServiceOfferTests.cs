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
}