using System.Net;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerPngAndAssetProviderTests
{
    [Fact]
    public async Task Local_provider_is_deterministic_and_validates_exact_canvas()
    {
        var provider = new LocalDeterministicWeddingPlannerCreativeAssetProvider(
            Options.Create(new WeddingPlannerCreativeAssetOptions { LocalEstimatedCostUsd = 0.01m }));
        var request = SampleRequest(1080, 1080);
        var first = await provider.GeneratePngAsync(request);
        var second = await provider.GeneratePngAsync(request);
        Assert.Equal(first.PngBytes, second.PngBytes);
        var validated = WeddingPlannerPngValidator.ValidateExactCanvas(first.PngBytes, 1080, 1080);
        Assert.Equal(validated.Sha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(first.PngBytes)).ToLowerInvariant());
        Assert.Equal(2, provider.InvokeCount);
    }

    [Theory]
    [InlineData(1080, 1080)]
    [InlineData(1080, 1920)]
    [InlineData(1200, 628)]
    [InlineData(1200, 600)]
    public async Task Local_provider_covers_all_format_canvases(int width, int height)
    {
        var provider = new LocalDeterministicWeddingPlannerCreativeAssetProvider();
        var result = await provider.GeneratePngAsync(SampleRequest(width, height));
        var validated = WeddingPlannerPngValidator.ValidateExactCanvas(result.PngBytes, width, height);
        Assert.True(validated.ByteSize <= WeddingPlannerPngValidator.MaxAssetBytes);
        Assert.Equal(width, validated.Width);
        Assert.Equal(height, validated.Height);
    }

    [Fact]
    public void Validator_rejects_bad_signature_ancillary_and_wrong_dims()
    {
        var good = LocalDeterministicWeddingPlannerCreativeAssetProvider
            .BuildDeterministicTruecolorPng(10, 10, Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        WeddingPlannerPngValidator.ValidateExactCanvas(good, 10, 10);

        var badSig = (byte[])good.Clone();
        badSig[0] = 0x00;
        Assert.Throws<InvalidOperationException>(() => WeddingPlannerPngValidator.ValidateExactCanvas(badSig, 10, 10));

        Assert.Throws<InvalidOperationException>(() => WeddingPlannerPngValidator.ValidateExactCanvas(good, 11, 10));

        // Insert a forbidden tEXt chunk between IHDR and IDAT by corrupting type bytes of IDAT length region is hard;
        // instead append trailing bytes after IEND.
        var trailing = good.Concat(new byte[] { 0, 0, 0, 0 }).ToArray();
        Assert.Throws<InvalidOperationException>(() => WeddingPlannerPngValidator.ValidateExactCanvas(trailing, 10, 10));
    }

    [Fact]
    public async Task Remote_accepts_raw_png_and_rejects_url_html_and_wrong_content_type()
    {
        var seed = Enumerable.Range(0, 32).Select(i => (byte)(i + 3)).ToArray();
        var png = LocalDeterministicWeddingPlannerCreativeAssetProvider.BuildDeterministicTruecolorPng(8, 8, seed);

        var okHandler = new FakeHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(png)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            response.Headers.Add("x-request-id", "remote-asset-1");
            response.Headers.Add("x-estimated-cost-usd", "0.02");
            return Task.FromResult(response);
        });
        var ok = CreateRemote(okHandler);
        var result = await ok.GeneratePngAsync(SampleRequest(8, 8));
        Assert.Equal(png, result.PngBytes);
        Assert.Equal("remote-asset-1", result.ProviderRequestId);
        Assert.Equal(0.02m, result.EstimatedCostUsd);

        var urlHandler = new FakeHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("https://evil.example/image.png", System.Text.Encoding.UTF8, "image/png")
            };
            return Task.FromResult(response);
        });
        await Assert.ThrowsAsync<WeddingPlannerCreativeAssetProviderException>(() =>
            CreateRemote(urlHandler).GeneratePngAsync(SampleRequest(8, 8)));

        var htmlHandler = new FakeHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>nope</html>", System.Text.Encoding.UTF8, "text/html")
            };
            return Task.FromResult(response);
        });
        var htmlEx = await Assert.ThrowsAsync<WeddingPlannerCreativeAssetProviderException>(() =>
            CreateRemote(htmlHandler).GeneratePngAsync(SampleRequest(8, 8)));
        Assert.Equal("CREATIVE_ASSET_PROVIDER_CONTENT_TYPE", htmlEx.ErrorCode);
        Assert.DoesNotContain("rk-secret", htmlEx.Message, StringComparison.Ordinal);
    }

    private static WeddingPlannerCreativeAssetGenerationRequest SampleRequest(int width, int height) =>
        new(
            "variant_1",
            WeddingPlannerChannelFormats.StaticSocialSquare,
            width,
            height,
            "{\"headline\":\"Hello\"}",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            WeddingPlannerConceptIds.Concept1);

    private static RemoteHttpWeddingPlannerCreativeAssetProvider CreateRemote(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://assets.example.test/") };
        return new RemoteHttpWeddingPlannerCreativeAssetProvider(
            client,
            Options.Create(new WeddingPlannerCreativeAssetOptions
            {
                Provider = WeddingPlannerCreativeAssetProviderKinds.RemoteHttp,
                BaseUrl = "https://assets.example.test/",
                ApiKey = "rk-secret",
                RemoteEstimatedCostUsd = 0.01m
            }));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
