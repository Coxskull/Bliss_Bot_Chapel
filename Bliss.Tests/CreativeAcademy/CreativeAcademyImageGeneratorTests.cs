using System.Net;
using System.Text;
using System.Text.Json;
using Bliss.Api.Operations;
using Bliss.Api.Runtime;

namespace Bliss.Tests.Academy;

public sealed class CreativeAcademyImageGeneratorTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    [Fact]
    public async Task Configured_adapter_sends_one_authenticated_recipe_and_accepts_a_real_image_type()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"imageBase64":"{{Convert.ToBase64String(OnePixelPng)}}","mediaType":"image/png","providerRequestId":"provider-42"}""",
                Encoding.UTF8,
                "application/json")
        });
        var runtime = Runtime();
        var generator = new ConfiguredCreativeImageGenerator(new HttpClient(handler), runtime);

        var result = await generator.GenerateAsync(
            new CreativeImageRequest("request-123", "Create Ruta Libre Motos", 1536, 864),
            CancellationToken.None);

        Assert.True(generator.Configured);
        Assert.Equal("image/png", result.MediaType);
        Assert.Equal("provider-42", result.ProviderRequestId);
        Assert.Equal(OnePixelPng, result.Content);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("Create Ruta Libre Motos", handler.Body);
        Assert.Contains("\"width\":1536", handler.Body);
        Assert.Contains("\"responseFormat\":\"base64\"", handler.Body);
    }

    [Fact]
    public async Task OpenAi_adapter_sends_the_images_payload_and_reads_b64_json()
    {
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal(
                CreativeGenerationOptions.OpenAiImagesEndpoint,
                request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"created":1,"data":[{"b64_json":"{{Convert.ToBase64String(OnePixelPng)}}"}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var runtime = new BlissRuntimeOptions
        {
            CreativeGeneration = new CreativeGenerationOptions
            {
                Provider = "OpenAI",
                ApiToken = "secret-token",
                Model = "gpt-image-1",
                TimeoutSeconds = 30,
                MaxImageBytes = 1024 * 1024
            }
        };
        var generator = new ConfiguredCreativeImageGenerator(new HttpClient(handler), runtime);

        var result = await generator.GenerateAsync(
            new CreativeImageRequest("request-123", "Create Ruta Libre Motos", 1536, 864),
            CancellationToken.None);

        Assert.True(generator.Configured);
        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(OnePixelPng, result.Content);
        Assert.Contains("\"model\":\"gpt-image-1\"", handler.Body);
        Assert.Contains("\"size\":\"1536x1024\"", handler.Body);
        Assert.Contains("Create Ruta Libre Motos", handler.Body);
        Assert.DoesNotContain("response_format", handler.Body);
        Assert.DoesNotContain("responseFormat", handler.Body);
    }

    [Fact]
    public async Task Configured_adapter_rejects_non_image_content()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"imageBase64":"{{Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg></svg>"))}}","mediaType":"image/svg+xml"}""",
                Encoding.UTF8,
                "application/json")
        });
        var generator = new ConfiguredCreativeImageGenerator(
            new HttpClient(handler),
            Runtime());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.GenerateAsync(
                new CreativeImageRequest("request-123", "prompt", 1536, 864),
                CancellationToken.None));

        Assert.Contains("unsupported image type", error.Message);
    }

    [Fact]
    public void OpenAi_size_mapping_uses_supported_landscape_sizes()
    {
        Assert.Equal("1536x1024", ConfiguredCreativeImageGenerator.MapOpenAiSize(1536, 864, "gpt-image-1"));
        Assert.Equal("1792x1024", ConfiguredCreativeImageGenerator.MapOpenAiSize(1536, 864, "dall-e-3"));
        var body = ConfiguredCreativeImageGenerator.OpenAiBody(
            new CreativeImageRequest("request-123", "prompt", 1536, 864),
            "dall-e-3");
        var json = JsonSerializer.Serialize(body);
        Assert.Contains("\"response_format\":\"b64_json\"", json);
    }

    [Fact]
    public async Task OpenAi_credit_refusal_is_reported_without_storing_an_image()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(
                """{"error":{"message":"You have no credits remaining.","type":"insufficient_quota","code":"credit_balance_exhausted"}}""",
                Encoding.UTF8,
                "application/json")
        });
        var runtime = new BlissRuntimeOptions
        {
            CreativeGeneration = new CreativeGenerationOptions
            {
                Provider = "OpenAI",
                ApiToken = "secret-token",
                Model = "gpt-image-1",
                TimeoutSeconds = 30,
                MaxImageBytes = 1024 * 1024
            }
        };
        var generator = new ConfiguredCreativeImageGenerator(new HttpClient(handler), runtime);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.GenerateAsync(
                new CreativeImageRequest("request-123", "prompt", 1536, 864),
                CancellationToken.None));

        Assert.Contains("no remaining API credits", error.Message);
        Assert.Contains("No advertisement was stored", error.Message);
    }

    private static BlissRuntimeOptions Runtime() => new()
    {
        CreativeGeneration = new CreativeGenerationOptions
        {
            Endpoint = "https://image-provider.invalid/generate",
            ApiToken = "secret-token",
            TimeoutSeconds = 30,
            MaxImageBytes = 1024 * 1024
        }
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return response(request);
        }
    }
}
