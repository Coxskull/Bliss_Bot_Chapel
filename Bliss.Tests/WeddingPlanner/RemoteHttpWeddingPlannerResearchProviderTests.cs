using System.Net;
using System.Text;
using System.Text.Json;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.WeddingPlanner;

public sealed class RemoteHttpWeddingPlannerResearchProviderTests
{
    private const string SecretApiKey = "rk-test-secret-do-not-leak-999";

    [Fact]
    public async Task Acquire_sends_bearer_json_request_and_maps_catalog()
    {
        string? capturedBody = null;
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHandler(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return JsonResponse("""
                {
                  "sourceCatalog": {
                    "schemaVersion": "research-source-catalog.v1",
                    "sources": [{
                      "id": "src_1",
                      "title": "Remote title",
                      "url": "https://docs.example.invalid/x",
                      "publisher": "Remote",
                      "retrievedAt": "2026-01-01T00:00:00Z",
                      "synthetic": false
                    }]
                  },
                  "providerRequestId": "remote-req-1",
                  "estimatedCostUsd": 0.012345
                }
                """);
        });

        var provider = CreateProvider(handler);
        var result = await provider.AcquireSourcesAsync(SampleRequest());

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.EndsWith("/research/acquire", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal(SecretApiKey, capturedRequest.Headers.Authorization.Parameter);
        Assert.Contains("\"topic\":\"Wedding ads\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("src_1", result.SourceCatalogJson, StringComparison.Ordinal);
        Assert.Equal("remote-req-1", result.ProviderRequestId);
        Assert.Equal(0.012345m, result.EstimatedCostUsd);
        Assert.Equal(RemoteHttpWeddingPlannerResearchProvider.ProviderKey, result.ProviderKey);
        Assert.DoesNotContain(SecretApiKey, result.SourceCatalogJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Oversized_and_unknown_field_responses_fail_closed_without_leaking_secret()
    {
        var oversized = new string('x', WeddingPlannerCuratorValidation.MaxRemoteResponseBytes + 10);
        var oversizedProvider = CreateProvider(new FakeHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sourceCatalog\":\"" + oversized + "\"}", Encoding.UTF8, "application/json")
            })));
        var oversizedEx = await Assert.ThrowsAsync<WeddingPlannerResearchProviderException>(() =>
            oversizedProvider.AcquireSourcesAsync(SampleRequest()));
        Assert.Equal("RESEARCH_PROVIDER_PAYLOAD_TOO_LARGE", oversizedEx.ErrorCode);
        Assert.DoesNotContain(SecretApiKey, oversizedEx.Message, StringComparison.Ordinal);

        var unknownProvider = CreateProvider(new FakeHandler((_, _) =>
            Task.FromResult(JsonResponse("""{"sourceCatalog":{"schemaVersion":"research-source-catalog.v1","sources":[]},"evil":true}"""))));
        var unknownEx = await Assert.ThrowsAsync<WeddingPlannerResearchProviderException>(() =>
            unknownProvider.AcquireSourcesAsync(SampleRequest()));
        Assert.Equal("RESEARCH_PROVIDER_UNKNOWN_FIELD", unknownEx.ErrorCode);
        Assert.DoesNotContain(SecretApiKey, unknownEx.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_error_and_invalid_json_fail_secret_safe()
    {
        var httpProvider = CreateProvider(new FakeHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("{\"error\":\"nope " + SecretApiKey + "\"}", Encoding.UTF8, "application/json")
            })));
        var httpEx = await Assert.ThrowsAsync<WeddingPlannerResearchProviderException>(() =>
            httpProvider.AcquireSourcesAsync(SampleRequest()));
        Assert.StartsWith("RESEARCH_PROVIDER_HTTP_", httpEx.ErrorCode, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretApiKey, httpEx.Message, StringComparison.Ordinal);

        var badJson = CreateProvider(new FakeHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
            })));
        var jsonEx = await Assert.ThrowsAsync<WeddingPlannerResearchProviderException>(() =>
            badJson.AcquireSourcesAsync(SampleRequest()));
        Assert.Equal("RESEARCH_PROVIDER_INVALID_PAYLOAD", jsonEx.ErrorCode);
    }

    [Fact]
    public async Task Local_catalog_is_conspicuously_synthetic()
    {
        var local = new LocalDeterministicWeddingPlannerResearchProvider();
        var result = await local.AcquireSourcesAsync(SampleRequest());
        Assert.Contains(".invalid", result.SourceCatalogJson, StringComparison.Ordinal);
        Assert.Contains("SYNTHETIC", result.SourceCatalogJson, StringComparison.Ordinal);
        Assert.Equal(1, local.InvokeCount);
    }

    private static WeddingPlannerResearchAcquisitionRequest SampleRequest() =>
        new(
            "Wedding ads",
            "Understand local demand",
            ["Who books?"],
            "Manila",
            "en",
            Array.Empty<string>(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            null,
            "Brand framing only");

    private static RemoteHttpWeddingPlannerResearchProvider CreateProvider(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://research.example.test/v1/")
        };
        var options = Options.Create(new WeddingPlannerResearchOptions
        {
            Provider = WeddingPlannerResearchProviderKinds.RemoteHttp,
            BaseUrl = "https://research.example.test/v1/",
            ApiKey = SecretApiKey,
            TimeoutSeconds = 5,
            RemoteEstimatedCostUsd = 0.01m
        });
        return new RemoteHttpWeddingPlannerResearchProvider(http, options);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }
}
