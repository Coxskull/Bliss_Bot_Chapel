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
            if (protocol == "OpenAI") reply.Headers.TryAddWithoutValidation("x-request-id", "req_test_image");
            return reply;
        });
        var result = await new ConfiguredControlledAiProvider(new HttpClient(handler), runtime).ExecuteAsync(
            new AiProviderRequest(Guid.NewGuid(), "IMAGE", protocol == "OpenAI" ? "gpt-image-2.5-flare" : "fixture-image", "{\"recipe\":\"Original candidate\"}", 100), default);
        Assert.Equal(Convert.FromBase64String(AcademyProductionTests.Pixel), result.Image);
        Assert.Equal(protocol == "OpenAI" ? .00305m : null, result.ActualUsd);
    }
    private static BlissRuntimeOptions OpenAiPilot() => new()
    {
        ControlledAi = new ControlledAiOptions
        {
            Enabled = true, ImagePilotEnabled = true, Protocol = "OpenAI",
            Provider = "OpenAI", ApiToken = "sk-fixture-not-real",
            ImageEndpoint = "https://api.openai.com/v1/images/generations",
            ImageModel = "gpt-image-2.5-flare",
            MaxCallsPerDay = 11, DailyBudgetUsd = 20m, TotalBudgetUsd = 20m,
            ImageReservationUsd = 1.80m, MaxResponseBytes = 24_000_000,
        }
    };

    [Theory]
    [InlineData("insufficient_quota", "rate_limit_exceeded", "PROVIDER_HTTP_429_CODE_insufficient_quota")]
    [InlineData("credit_balance_exhausted", "", "PROVIDER_HTTP_429_CODE_credit_balance_exhausted")]
    [InlineData("rate_limit_exceeded", "", "PROVIDER_HTTP_429_CODE_rate_limit_exceeded")]
    [InlineData("", "rate_limit_exceeded", "PROVIDER_HTTP_429_TYPE_rate_limit_exceeded")]
    [InlineData("", "rate_limit_error", "PROVIDER_HTTP_429_TYPE_rate_limit_error")]
    [InlineData("sk-proj-FORGED-SENSITIVE", "", "PROVIDER_HTTP_429")]
    [InlineData("sk-proj-lowercase-secret", "", "PROVIDER_HTTP_429")]
    [InlineData("unrecognized_provider_code", "", "PROVIDER_HTTP_429")]
    [InlineData("", "sk-proj-lowercase-secret", "PROVIDER_HTTP_429")]
    public async Task Image_429_retains_only_safe_machine_reason_and_valid_request_id(
        string code, string type, string expected)
    {
        var calls = 0;
        var handler = new Handler((_, _) =>
        {
            calls++;
            var json = System.Text.Json.JsonSerializer.Serialize(
                new { error = new { code, type, message = "sk-proj-SECRET-NEVER-COPY" } });
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "req_fixture429valid");
            return response;
        });
        var adapter = new ConfiguredControlledAiProvider(new HttpClient(handler), OpenAiPilot());
        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            adapter.ExecuteAsync(new AiProviderRequest(Guid.NewGuid(), "IMAGE", "gpt-image-2.5-flare", "{}", 100), default));
        Assert.Equal(expected, exception.Code);
        Assert.Equal("req_fixture429valid", exception.ProviderRequestId);
        Assert.True(exception.Code.Length <= 128);
        Assert.DoesNotContain("SECRET", exception.Message);
        Assert.DoesNotContain("sk-proj-", exception.Message);
        Assert.Equal(1, calls); // never automatic paid retries or fallbacks
    }

    [Fact]
    public async Task Image_429_oversized_body_and_forged_request_id_fail_to_generic_status()
    {
        var handler = new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(new string('x', 8193))
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "Bearer sk-proj-LEAK");
            return response;
        });
        var provider = new ConfiguredControlledAiProvider(new HttpClient(handler), OpenAiPilot());
        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.ExecuteAsync(new AiProviderRequest(Guid.NewGuid(), "IMAGE", "gpt-image-2.5-flare", "{}", 100), default));
        Assert.Equal("PROVIDER_HTTP_429", exception.Code);
        Assert.Null(exception.ProviderRequestId);
    }


    [Fact]
    public async Task Paid_image_malformed_success_retains_validated_request_id_for_billing_reconciliation()
    {
        var handler = new Handler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[]}")
            };
            response.Headers.TryAddWithoutValidation("x-request-id", "req_fixture_success");
            return response;
        });
        var provider = new ConfiguredControlledAiProvider(new HttpClient(handler), OpenAiPilot());
        var failure = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.ExecuteAsync(new AiProviderRequest(Guid.NewGuid(), "IMAGE", "gpt-image-2.5-flare", "{}", 100), default));
        Assert.Equal("INVALID_PROVIDER_RESPONSE", failure.Code);
        Assert.Equal("req_fixture_success", failure.ProviderRequestId);
    }

    [Theory]
    [InlineData("req_fixture_success", true)]
    [InlineData("Bearer sk-fixture", false)]
    public async Task Paid_image_success_uses_header_not_untrusted_body_id(string header, bool valid)
    {
        var handler = new Handler((_, _) =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new {
                id = "untrusted-body-id",
                data = new[] { new { b64_json = AcademyProductionTests.Pixel } },
                usage = new { input_tokens = 10, input_tokens_details = new { text_tokens = 10, image_tokens = 0 }, output_tokens = 100 }
            });
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            response.Headers.TryAddWithoutValidation("x-request-id", header);
            return response;
        });
        var provider = new ConfiguredControlledAiProvider(new HttpClient(handler), OpenAiPilot());
        var request = new AiProviderRequest(Guid.NewGuid(), "IMAGE", "gpt-image-2.5-flare", "{}", 100);
        if (valid)
            Assert.Equal(header, (await provider.ExecuteAsync(request, default)).RequestId);
        else
        {
            var failure = await Assert.ThrowsAsync<AiProviderException>(() => provider.ExecuteAsync(request, default));
            Assert.Equal("IMAGE_REQUEST_ID_MISSING", failure.Code);
            Assert.Null(failure.ProviderRequestId);
        }
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
}
