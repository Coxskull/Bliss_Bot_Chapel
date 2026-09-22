using System.Net;
using System.Text;
using System.Text.Json;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.WeddingPlanner;

public sealed class OpenAiCompatibleWeddingPlannerAiProviderTests
{
    private const string SecretApiKey = "sk-test-secret-do-not-leak-12345";

    [Fact]
    public async Task Concierge_request_sends_system_instruction_and_chat_shape()
    {
        string? capturedBody = null;
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHandler(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();
            return JsonResponse("""
                {
                  "id": "chatcmpl-concierge-1",
                  "model": "gpt-test",
                  "choices": [{ "message": { "role": "assistant", "content": "Happy to help refine Brand DNA." } }],
                  "usage": { "prompt_tokens": 40, "completion_tokens": 12, "total_tokens": 52 }
                }
                """);
        });

        var provider = CreateProvider(handler, inputPrice: 1.0m, outputPrice: 2.0m);
        var result = await provider.CompleteAsync(new WeddingPlannerAiCompletionRequest(
            WeddingPlannerAgentRoles.Concierge,
            WeddingPlannerPromptPacks.ConciergeV1,
            new[]
            {
                new WeddingPlannerAiMessage(WeddingPlannerActorTypes.Advertiser, "We serve local families.")
            },
            WeddingPlannerResponseFormats.Text,
            512));

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.EndsWith("/chat/completions", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal(SecretApiKey, capturedRequest.Headers.Authorization.Parameter);

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("gpt-test", root.GetProperty("model").GetString());
        Assert.Equal(512, root.GetProperty("max_tokens").GetInt32());
        Assert.False(root.TryGetProperty("response_format", out _));

        var messages = root.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Contains("Concierge", messages[0].GetProperty("content").GetString(), StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerPromptPacks.ConciergeV1, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("We serve local families.", messages[1].GetProperty("content").GetString());

        Assert.Equal("Happy to help refine Brand DNA.", result.Content);
        Assert.Equal(OpenAiCompatibleWeddingPlannerAiProvider.ProviderKey, result.ProviderKey);
        Assert.Equal("gpt-test", result.ModelId);
        Assert.Equal(OpenAiCompatibleWeddingPlannerAiProvider.AdapterVersion, result.AdapterVersion);
        Assert.Equal("chatcmpl-concierge-1", result.ProviderRequestId);
        Assert.Equal(WeddingPlannerWorkers.OpenAiCompatibleV1, result.WorkerKey);
        Assert.Equal(40, result.PromptTokens);
        Assert.Equal(12, result.CompletionTokens);
        Assert.Equal(52, result.TotalTokens);
        Assert.Equal(0.000064m, result.EstimatedCostUsd);
    }

    [Fact]
    public async Task Interpreter_request_requires_json_response_format()
    {
        string? capturedBody = null;
        var handler = new FakeHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""
                {
                  "id": "chatcmpl-dna-1",
                  "model": "gpt-test",
                  "choices": [{ "message": { "content": "{\"schemaVersion\":\"brand-dna.v1\",\"brandVoice\":\"warm\"}" } }],
                  "usage": { "prompt_tokens": 100, "completion_tokens": 50, "total_tokens": 150 }
                }
                """);
        });

        var provider = CreateProvider(handler, workerKey: "wp-custom-worker.v1", inputPrice: 3m, outputPrice: 6m);
        var result = await provider.CompleteAsync(new WeddingPlannerAiCompletionRequest(
            WeddingPlannerAgentRoles.BrandDnaInterpreter,
            WeddingPlannerPromptPacks.BrandDnaV1,
            new[]
            {
                new WeddingPlannerAiMessage(WeddingPlannerActorTypes.Operator, "voice: calm. audience: couples.")
            },
            WeddingPlannerResponseFormats.Json,
            2048));

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal(2048, root.GetProperty("max_tokens").GetInt32());

        var system = root.GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.Contains("Brand DNA Interpreter", system, StringComparison.Ordinal);
        Assert.Contains("brand-dna.v1", system, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerPromptPacks.BrandDnaV1, system);

        Assert.Contains("brand-dna.v1", result.Content);
        Assert.Equal("wp-custom-worker.v1", result.WorkerKey);
        Assert.Equal(provider.WorkerKey, result.WorkerKey);
        Assert.Equal(100, result.PromptTokens);
        Assert.Equal(50, result.CompletionTokens);
        Assert.Equal(150, result.TotalTokens);
        Assert.Equal(0.0006m, result.EstimatedCostUsd);
    }

    [Fact]
    public async Task Curator_stage_request_includes_profile_roles_and_schema_rules()
    {
        string? capturedBody = null;
        var handler = new FakeHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""
                {
                  "id": "chatcmpl-curator-1",
                  "model": "gpt-test",
                  "choices": [{ "message": { "content": "{\"schemaVersion\":\"curator-worker-output.v1\"}" } }],
                  "usage": { "prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15 }
                }
                """);
        });

        var provider = CreateProvider(handler);
        var roles = WeddingPlannerCuratorWorkerProfiles.AssignedRoles(WeddingPlannerCuratorWorkerProfiles.EvidenceV1);
        await provider.CompleteAsync(new WeddingPlannerAiCompletionRequest(
            WeddingPlannerAgentRoles.CuratorEvidence,
            WeddingPlannerPromptPacks.CuratorEvidenceV1,
            new[] { new WeddingPlannerAiMessage(WeddingPlannerActorTypes.System, "{\"sourceCatalog\":{}}") },
            WeddingPlannerResponseFormats.Json,
            1024,
            WeddingPlannerCuratorWorkerProfiles.EvidenceV1,
            roles));

        using var doc = JsonDocument.Parse(capturedBody!);
        var system = doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.Contains("curator-worker-output.v1", system, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerCuratorWorkerProfiles.EvidenceV1, system, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerCuratorLogicalRoles.EvidenceAnalyst, system, StringComparison.Ordinal);
        Assert.Contains("Never fetch live URL content", system, StringComparison.Ordinal);
        Assert.Equal("json_object", doc.RootElement.GetProperty("response_format").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Non_success_errors_are_bounded_and_secret_safe()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(
                "{\"error\":{\"message\":\"Incorrect API key " + SecretApiKey + "\",\"type\":\"invalid_request_error\"}}",
                Encoding.UTF8,
                "application/json")
        }));

        var provider = CreateProvider(handler);
        var ex = await Assert.ThrowsAsync<WeddingPlannerAiProviderException>(() =>
            provider.CompleteAsync(new WeddingPlannerAiCompletionRequest(
                WeddingPlannerAgentRoles.Concierge,
                WeddingPlannerPromptPacks.ConciergeV1,
                new[] { new WeddingPlannerAiMessage(WeddingPlannerActorTypes.Advertiser, "hello") },
                WeddingPlannerResponseFormats.Text,
                128)));

        Assert.Equal("PROVIDER_HTTP_401", ex.ErrorCode);
        Assert.Contains("HTTP 401", ex.Message);
        Assert.DoesNotContain(SecretApiKey, ex.Message);
        Assert.DoesNotContain(SecretApiKey, ex.ToString());
    }

    [Fact]
    public async Task Invalid_payload_errors_are_secret_safe()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse("{not-json")));
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<WeddingPlannerAiProviderException>(() =>
            provider.CompleteAsync(new WeddingPlannerAiCompletionRequest(
                WeddingPlannerAgentRoles.Concierge,
                WeddingPlannerPromptPacks.ConciergeV1,
                Array.Empty<WeddingPlannerAiMessage>(),
                WeddingPlannerResponseFormats.Text,
                64)));

        Assert.Equal("PROVIDER_INVALID_PAYLOAD", ex.ErrorCode);
        Assert.DoesNotContain(SecretApiKey, ex.Message);
        Assert.DoesNotContain(SecretApiKey, ex.ToString());
    }

    [Fact]
    public async Task Empty_completion_content_is_rejected()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(JsonResponse("""
            {
              "id": "chatcmpl-empty",
              "choices": [{ "message": { "content": "   " } }],
              "usage": { "prompt_tokens": 1, "completion_tokens": 0, "total_tokens": 1 }
            }
            """)));
        var provider = CreateProvider(handler);

        var ex = await Assert.ThrowsAsync<WeddingPlannerAiProviderException>(() =>
            provider.CompleteAsync(new WeddingPlannerAiCompletionRequest(
                WeddingPlannerAgentRoles.Concierge,
                WeddingPlannerPromptPacks.ConciergeV1,
                Array.Empty<WeddingPlannerAiMessage>(),
                WeddingPlannerResponseFormats.Text,
                64)));

        Assert.Equal("PROVIDER_EMPTY_CONTENT", ex.ErrorCode);
    }

    private static OpenAiCompatibleWeddingPlannerAiProvider CreateProvider(
        HttpMessageHandler handler,
        string? workerKey = null,
        decimal inputPrice = 0m,
        decimal outputPrice = 0m)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/v1/")
        };
        var options = Options.Create(new WeddingPlannerAiOptions
        {
            Provider = WeddingPlannerAiProviderKinds.OpenAiCompatible,
            BaseUrl = "https://example.test/v1/",
            ApiKey = SecretApiKey,
            ModelId = "gpt-test",
            WorkerKey = workerKey,
            TimeoutSeconds = 30,
            InputPricePerMillionTokens = inputPrice,
            OutputPricePerMillionTokens = outputPrice
        });
        return new OpenAiCompatibleWeddingPlannerAiProvider(client, options);
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
