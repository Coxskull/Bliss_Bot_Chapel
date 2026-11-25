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
}
