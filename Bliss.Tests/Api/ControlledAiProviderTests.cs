using System.Net;
using System.Text;
using Bliss.Api.Operations;
using Bliss.Api.Runtime;

namespace Bliss.Tests.Api;

public sealed class ControlledAiProviderTests
{
    public static BlissRuntimeOptions Runtime() => new() { ControlledAi = new ControlledAiOptions {
        Enabled = true, Protocol = "Gateway", Provider = "LOCAL_TEST_DOUBLE", ApiToken = "test-not-a-live-key",
        TextEndpoint = "https://fixture.example/text", ImageEndpoint = "https://fixture.example/image", TextModel = "fixture-text", ImageModel = "fixture-image",
        MaxCallsPerDay = 10, DailyBudgetUsd = 1, TotalBudgetUsd = 1, TextReservationUsd = .1m, ImageReservationUsd = .1m, TimeoutSeconds = 1 } };
    private sealed class Handler(Func<HttpRequestMessage, string, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            reply(request, await request.Content!.ReadAsStringAsync(ct));
    }
    [Theory]
    [InlineData("Gateway")]
    [InlineData("OpenAI")]
    public async Task Text_adapter_sends_one_bounded_tool_free_request_and_records_usage(string protocol)
    {
        var runtime = Runtime(); runtime.ControlledAi.Protocol = protocol;
        var calls = 0;
        var handler = new Handler((request, body) =>
        {
            calls++; Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Contains("bliss-workers-v1", body); Assert.Contains("max", body);
            Assert.DoesNotContain("\"tools\"", body);
            var json = protocol == "OpenAI" ? "{\"id\":\"fixture-id\",\"choices\":[{\"message\":{\"content\":\"Draft suggestion\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}"
                : "{\"text\":\"Draft suggestion\",\"providerRequestId\":\"fixture-id\",\"inputTokens\":10,\"outputTokens\":5,\"actualUsd\":0.01}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });
        var result = await new ConfiguredControlledAiProvider(new HttpClient(handler), runtime).ExecuteAsync(
            new AiProviderRequest(Guid.NewGuid(), "TEXT", "fixture-text", "{\"prompt\":\"Plan a campaign\"}", 100), default);
        Assert.Equal(1, calls); Assert.Equal("Draft suggestion", result.Text);
        Assert.Equal(10, result.InputTokens); Assert.Equal(5, result.OutputTokens);
        Assert.Equal(protocol == "OpenAI" ? null : .01m, result.ActualUsd);
    }
    [Theory]
    [InlineData("Gateway")]
    [InlineData("OpenAI")]
    public async Task Image_adapter_returns_png_bytes_for_private_review(string protocol)
    {
        var runtime = Runtime(); runtime.ControlledAi.Protocol = protocol;
        if (protocol == "OpenAI")
        {
            runtime.ControlledAi.Provider = "OpenAI";
            runtime.ControlledAi.ImagePilotEnabled = true;
            runtime.ControlledAi.ImageModel = "gpt-image-2.5-flare";
            runtime.ControlledAi.ImageEndpoint = "https://api.openai.com/v1/images/generations";
            runtime.ControlledAi.TotalBudgetUsd = 20m;
            runtime.ControlledAi.DailyBudgetUsd = 20m;
            runtime.ControlledAi.ImageReservationUsd = 1.8m;
            runtime.ControlledAi.MaxResponseBytes = 24_000_000;
        }
        var handler = new Handler((_, body) =>
        {
            Assert.Contains("recipe", body);
            if (protocol == "OpenAI") { using var payload = System.Text.Json.JsonDocument.Parse(body); Assert.Equal("low", payload.RootElement.GetProperty("quality").GetString()); }
            var json = protocol == "OpenAI" ? System.Text.Json.JsonSerializer.Serialize(new { data = new[] { new { b64_json = AcademyProductionTests.Pixel } }, usage = new { input_tokens = 10, input_tokens_details = new { text_tokens = 10, image_tokens = 0 }, output_tokens = 100 } })
                : $$"""{"imageBase64":"{{AcademyProductionTests.Pixel}}","providerRequestId":"fixture-image"}""";
            var reply = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            if (protocol == "OpenAI") reply.Headers.TryAddWithoutValidation("x-request-id", "req-test-image");
            return reply;
        });
        var result = await new ConfiguredControlledAiProvider(new HttpClient(handler), runtime).ExecuteAsync(
            new AiProviderRequest(Guid.NewGuid(), "IMAGE", protocol == "OpenAI" ? "gpt-image-2.5-flare" : "fixture-image", "{\"recipe\":\"Original candidate\"}", 100), default);
        Assert.Equal(Convert.FromBase64String(AcademyProductionTests.Pixel), result.Image);
        Assert.Equal(protocol == "OpenAI" ? .00305m : null, result.ActualUsd);
    }
    [Fact]
    public async Task Provider_errors_and_tool_requests_are_rejected_without_exposing_response_text()
    {
        var runtime = Runtime();
        var request = new AiProviderRequest(Guid.NewGuid(), "TEXT", "fixture", "{}", 100);
        var refused = new ConfiguredControlledAiProvider(new HttpClient(new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("sensitive-provider-error") })), runtime);
        var failure = await Assert.ThrowsAsync<AiProviderException>(() => refused.ExecuteAsync(request, default));
        Assert.Equal("PROVIDER_HTTP_401", failure.Code); Assert.DoesNotContain("sensitive", failure.Message);
        runtime.ControlledAi.Protocol = "OpenAI";
        var tools = new ConfiguredControlledAiProvider(new HttpClient(new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"Do something\",\"tool_calls\":[{}]}}]}") })), runtime);
        Assert.Equal("TOOLS_NOT_ALLOWED", (await Assert.ThrowsAsync<AiProviderException>(() => tools.ExecuteAsync(request, default))).Code);
    }

    [Theory]
    [InlineData("credit_balance_exhausted", "insufficient_quota", "PROVIDER_HTTP_429_QUOTA_OR_CREDITS_UNAVAILABLE")]
    [InlineData("rate_limit_exceeded", "rate_limit_error", "PROVIDER_HTTP_429_RATE_LIMIT")]
    [InlineData("unknown", "other", "PROVIDER_HTTP_429")]
    public async Task Future_429_provider_reason_is_sanitized_and_request_id_is_preserved(
        string code, string type, string expected)
    {
        var calls = 0;
        const string sensitive = "sk-proj-SHOULD-NEVER-BE-SHOWN";
        var handler = new Handler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = new { code, type, message = sensitive }
                }))
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "req_synthetic_429");
            return response;
        });
        var provider = new ConfiguredControlledAiProvider(new HttpClient(handler), Runtime());
        var request = new AiProviderRequest(Guid.NewGuid(), "TEXT", "fixture-text", "{}", 100);
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.ExecuteAsync(request, CancellationToken.None));
        Assert.Equal(expected, ex.Code);
        Assert.Equal("req_synthetic_429", ex.ProviderRequestId);
        Assert.Equal(1, calls); // No retry / fallback / second paid request.
        Assert.DoesNotContain(sensitive, ex.Message);
        Assert.DoesNotContain(code, ex.Message);
        Assert.DoesNotContain(type, ex.Message);
    }

    [Fact]
    public async Task Malicious_429_metadata_is_not_echoed_to_persisted_failure()
    {
        var calls = 0;
        var handler = new Handler((_, _) =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""
                    {"error":{"code":"sk-proj-EXFILTRATE","type":"../../sensitive","message":"SECRET CUSTOMER PROMPT"}}
                    """)
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "req_<secret>/../../");
            return response;
        });
        var provider = new ConfiguredControlledAiProvider(new HttpClient(handler), Runtime());
        var ex = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.ExecuteAsync(new AiProviderRequest(Guid.NewGuid(), "TEXT", "fixture-text", "{}", 100), default));
        Assert.Equal("PROVIDER_HTTP_429", ex.Code);
        Assert.Empty(ex.ProviderRequestId);
        Assert.Equal(1, calls);
        Assert.DoesNotContain("sk-proj", ex.Message);
        Assert.DoesNotContain("CUSTOMER", ex.Message);
    }

    [Fact]
    public async Task Oversized_or_malformed_429_body_remains_unknown_without_retry()
    {
        foreach (var body in new[] { new string('x', 10000), "{not-json" })
        {
            var calls = 0;
            var handler = new Handler((_, _) =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Content = new StringContent(body)
                };
            });
            var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
                new ConfiguredControlledAiProvider(new HttpClient(handler), Runtime())
                    .ExecuteAsync(new AiProviderRequest(Guid.NewGuid(), "TEXT", "fixture-text", "{}", 100), default));
            Assert.Equal("PROVIDER_HTTP_429", ex.Code);
            Assert.Empty(ex.ProviderRequestId);
            Assert.Equal(1, calls);
        }
    }

    [Fact]
    public async Task Safe_provider_error_parser_does_not_reclassify_non_429_billing_errors()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error":{"code":"credit_balance_exhausted","type":"insufficient_quota"}}""")
        };
        var code = await ConfiguredControlledAiProvider.ClassifiedProviderRejectionAsync(
            response, CancellationToken.None);
        Assert.Equal("PROVIDER_HTTP_401", code);
    }
}
