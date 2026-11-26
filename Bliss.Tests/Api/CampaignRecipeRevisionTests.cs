using Bliss.Api.Operations;
namespace Bliss.Tests.Api;
public sealed class CampaignRecipeRevisionTests
{
    [Fact]
    public void Key_retains_exact_campaign_digest_and_review_within_existing_column()
    {
        var c=Guid.NewGuid();var r=Guid.NewGuid();var digest=new string('a',64);
        var key=CampaignRecipeRevision.Key(c,digest,r);
        Assert.True(key.Length<=128);
        Assert.True(CampaignRecipeRevision.TryCampaign(key,out var parsed));Assert.Equal(c,parsed);
        Assert.True(CampaignRecipeRevision.Matches(key,c,digest));
        Assert.False(CampaignRecipeRevision.Matches(key,c,new string('b',64)));
        Assert.False(CampaignRecipeRevision.Matches(key,Guid.NewGuid(),digest));
        Assert.False(CampaignRecipeRevision.Matches(key+"x",c,digest));
        Assert.NotEqual(key,CampaignRecipeRevision.Key(c,digest,Guid.NewGuid()));
        Assert.True(CampaignRecipeRevision.Matches(CampaignRecipeRevision.OriginalKey(c,digest),c,digest));
    }
}
