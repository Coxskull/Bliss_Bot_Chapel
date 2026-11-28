using Bliss.Api.Controllers;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;
namespace Bliss.Tests.Api;
public sealed class StarterCheckoutInstructionsTests
{
    [Fact]
    public void Exact_offer_reference_is_added_to_existing_link()
    {
        var id=Guid.NewGuid();var url=new Uri(StaffCreativePreparationController.CheckoutInstructions(id));
        Assert.Equal("https",url.Scheme);Assert.Equal("buy.stripe.com",url.Host);
        Assert.Equal("?client_reference_id="+id.ToString("D"),url.Query);
    }
    [Fact]
    public void Checkout_instructions_are_exact_staff_post_only()
    {
        const string route="/api/internal/creative-preparation/starter-checkout";
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route)));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(route),staffCreativePreparationEnabled:true));
    }
}