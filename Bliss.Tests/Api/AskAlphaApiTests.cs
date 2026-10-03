using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class AskAlphaApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public AskAlphaApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ask_Alpha_answers_then_advances_and_repeats_a_studied_answer()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var firstResponse = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/messages", new
        {
            text = "How does this work?"
        });
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("Ask Alpha", first!.GetProperty("voice").GetString());
        Assert.Equal("teaching", first.GetProperty("gear").GetString());
        var firstReply = first.GetProperty("reply").GetString()!;
        Assert.StartsWith("Thank you.", firstReply);
        Assert.Contains("has not commissioned", firstReply);
        Assert.Contains("ask what a human handoff requires", firstReply);
        Assert.DoesNotContain("$", firstReply);

        var secondResponse = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/messages", new
        {
            text = "How does this work?"
        });
        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondReply = second!.GetProperty("reply").GetString()!;
        Assert.Contains("already answered", secondReply);
        Assert.Contains("has not commissioned", secondReply);
        Assert.DoesNotContain("ask what a human handoff requires", secondReply);
        Assert.Equal("teaching", second.GetProperty("gear").GetString());

        var priceResponse = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/messages", new
        {
            text = "How much does this cost?"
        });
        var price = await priceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var priceReply = price!.GetProperty("reply").GetString()!;
        Assert.Contains("cannot invent a price", priceReply);
        Assert.Equal("integrity", price.GetProperty("gear").GetString());
        Assert.Equal("Ask Alpha", price.GetProperty("voice").GetString());
        Assert.DoesNotContain("$", priceReply);

        var page = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/mesa-norte");
        Assert.Equal("NOT_SENT", page!.GetProperty("delivery").GetString());
    }
}
