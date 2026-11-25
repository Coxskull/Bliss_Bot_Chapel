using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;
namespace Bliss.Tests.Api;
public sealed class StaffCreativeQueueTests
{
    [Fact]
    public void Queue_is_exact_staff_enabled_get_only()
    {
        const string route="/api/internal/creative-preparation/queue";
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(route)));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(route),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(route+"/other"),staffCreativePreparationEnabled:true));
    }
}
