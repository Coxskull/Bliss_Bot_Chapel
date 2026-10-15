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
        var handler = new Handler((_, body) =>
        {
            Assert.Contains("recipe", body);
            var json = protocol == "OpenAI" ? $$"""{"data":[{"b64_json":"{{AcademyProductionTests.Pixel}}"}]}"""
                : $$"""{"imageBase64":"{{AcademyProductionTests.Pixel}}","providerRequestId":"fixture-image"}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });
        var result = await new ConfiguredControlledAiProvider(new HttpClient(handler), runtime).ExecuteAsync(
            new AiProviderRequest(Guid.NewGuid(), "IMAGE", "fixture-image", "{\"recipe\":\"Original candidate\"}", 100), default);
        Assert.Equal(Convert.FromBase64String(AcademyProductionTests.Pixel), result.Image);
        Assert.Null(result.ActualUsd);
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
