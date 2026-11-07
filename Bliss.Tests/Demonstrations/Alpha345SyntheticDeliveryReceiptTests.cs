using System.Text.Json;

namespace Bliss.Tests.Alpha345;

public sealed class SyntheticDeliveryReceiptTests
{
    [Fact]
    public void Synthetic_receipt_cannot_be_misreported_as_real_display()
    {
        var parent = new DirectoryInfo(AppContext.BaseDirectory);
        string? source = null;
        while (parent is not null)
        {
            var path = Path.Combine(parent.FullName, "docs", "architecture",
                "ALPHA-345-SYNTHETIC-DELIVERY-RECEIPT.json");
            if (File.Exists(path)) { source = path; break; }
            parent = parent.Parent;
        }
        Assert.NotNull(source);
        using var json = JsonDocument.Parse(File.ReadAllText(source!));
        var doc = json.RootElement;
        Assert.Equal("SYNTHETIC", doc.GetProperty("evidenceKind").GetString());
        Assert.False(doc.GetProperty("realCampaign").GetBoolean());
        Assert.Equal("ENGAGED", doc.GetProperty("globalStop").GetString());
        Assert.Equal("NOT_SENT", doc.GetProperty("campaignStatus").GetString());
        Assert.Equal("NO_DISPLAY", doc.GetProperty("deliveryState").GetString());
        Assert.Equal("NONE", doc.GetProperty("proofOfDelivery").GetString());
        foreach (var field in new[] {
            "advertiserId", "creatorId", "contentItemId", "adInventorySlotId",
            "economicsQuoteId", "scheduledOccurrences", "observedOccurrences",
            "scheduledDurationSeconds", "observedDurationSeconds",
            "scheduledStartAt", "actualDisplayStartedAt", "verifiedDisplayAt",
            "impressions", "clicks", "conversions" })
        {
            Assert.Equal(JsonValueKind.Null, doc.GetProperty(field).ValueKind);
        }
        Assert.Equal("CREATOR_AUTHORIZATION_AND_PRICING_UNBOUND",
            doc.GetProperty("exception").GetString());
        Assert.Equal("V1", doc.GetProperty("productVersion").GetString());
    }
}
