using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Bliss.Tests.Api;
public sealed class CampaignImageReservationTests
{
    [Fact]
    public void Provider_input_requires_exact_receipt_and_recipe_and_excludes_local_metadata()
    {
        var grant = new CampaignImageAuthorization.Grant(Guid.NewGuid(), new string('a',64),Guid.NewGuid(),new string('b',64),"Original creative recipe");
        string Snapshot(Guid receipt, string recipe) => JsonSerializer.Serialize(new { consentReceiptId = receipt,
            recipeSha256 = grant.RecipeDigest, productionRecipe = recipe, briefId = Guid.NewGuid() });
        Assert.Null(CampaignImageAuthorization.ProviderInput(Snapshot(Guid.NewGuid(),grant.Recipe),grant));
        Assert.Null(CampaignImageAuthorization.ProviderInput(Snapshot(grant.ReceiptId,"changed"),grant));
        var input = CampaignImageAuthorization.ProviderInput(Snapshot(grant.ReceiptId,grant.Recipe),grant);
        Assert.NotNull(input);
        Assert.DoesNotContain(grant.ReceiptId.ToString(),input!);
        Assert.DoesNotContain("briefId",input!);
        Assert.Contains(grant.Recipe,input!);
    }
    [Fact]
    public void Reservation_is_exact_post_only_and_dispatch_defaults_off()
    {
        const string route = "/api/internal/creative-image-reservation";
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route)));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET",new PathString(route),staffCreativePreparationEnabled:true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST",new PathString(route+"/other"),staffCreativePreparationEnabled:true));
        var pin = new RestrictedImageRunOptions();
        Assert.False(pin.DispatchRequested);
        Assert.False(pin.ExternalBillingReconciled);
    }
}
