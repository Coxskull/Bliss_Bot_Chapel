using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class ContactRoadTests
{
    [Fact]
    public void A_public_marketing_road_is_discovered_and_not_eligible_to_send()
    {
        var result = ContactRoads.Accept(
            "company_marketing",
            "marketing@example.com",
            "https://example.com/puerto-azul/contact",
            prospectSuppressed: false);

        Assert.True(result.Accepted);
        Assert.Equal("DISCOVERED", result.State);
        Assert.False(result.OutreachEligible);
        Assert.Contains(result.Reasons, reason => reason.Contains("not permission to send"));
    }

    [Fact]
    public void A_road_without_a_public_source_is_rejected()
    {
        var result = ContactRoads.Accept("published_messaging", "+50760000000", "", prospectSuppressed: false);

        Assert.False(result.Accepted);
        Assert.False(result.OutreachEligible);
        Assert.Contains(result.Reasons, reason => reason.Contains("will not store a road"));
    }

    [Fact]
    public void Suppression_keeps_a_new_road_ineligible()
    {
        var result = ContactRoads.Accept(
            "general_company",
            "hello@example.com",
            "https://example.com/puerto-azul/contact",
            prospectSuppressed: true);

        Assert.True(result.Accepted);
        Assert.Equal("SUPPRESSED", result.State);
        Assert.False(result.OutreachEligible);
        Assert.Equal("A suppression reason is required. Alpha will not invent one.", ContactRoads.SuppressionError(" "));
        Assert.Null(ContactRoads.SuppressionError("The business asked Alpha to stop"));
    }
}
