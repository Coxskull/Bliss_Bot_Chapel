using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Api;

public sealed class LoopbackIngressTests
{
    private static BlissRuntimeOptions Policy(int publicPort = 18080, int backendPort = 18081) => new() {
        LoopbackIngress = new() { Enabled = true, PublicOrigin = "https://bliss.example.test/",
            PublicPort = publicPort, BackendPort = backendPort } };
    private static IConfiguration Deployment(int publicPort = 18080) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["RAILWAY_PUBLIC_DOMAIN"] = "bliss.example.test",
            ["PORT"] = publicPort.ToString() }).Build();

    [Fact]
    public void Only_actual_local_proxy_is_trusted_and_no_network_is_trusted()
    {
        var options = new ForwardedHeadersOptions();
        TrustedForwarding.Configure(options, Policy(), Deployment());
        Assert.Equal(IPAddress.Loopback, Assert.Single(options.KnownProxies));
        Assert.Empty(options.KnownNetworks);
        Assert.Equal(1, options.ForwardLimit);
        Assert.True(options.RequireHeaderSymmetry);
    }

    [Theory]
    [InlineData("http-origin")]
    [InlineData("foreign-origin")]
    [InlineData("origin-credentials")]
    [InlineData("origin-path")]
    [InlineData("same-ports")]
    [InlineData("privileged-port")]
    [InlineData("mismatched-port")]
    [InlineData("mixed-policy")]
    [InlineData("wrong-header")]
    public void Unsafe_or_ambiguous_listener_configuration_refuses_startup(string problem)
    {
        var runtime = Policy();
        switch (problem)
        {
            case "http-origin": runtime.LoopbackIngress.PublicOrigin = "http://bliss.example.test"; break;
            case "foreign-origin": runtime.LoopbackIngress.PublicOrigin = "https://unapproved.example.test"; break;
            case "origin-credentials": runtime.LoopbackIngress.PublicOrigin = "https://user:synthetic@bliss.example.test"; break;
            case "origin-path": runtime.LoopbackIngress.PublicOrigin = "https://bliss.example.test/path"; break;
            case "same-ports": runtime.LoopbackIngress.BackendPort = runtime.LoopbackIngress.PublicPort; break;
            case "privileged-port": runtime.LoopbackIngress.BackendPort = 80; break;
            case "mismatched-port": runtime.LoopbackIngress.PublicPort = 19090; break;
            case "mixed-policy": runtime.KnownProxies = ["192.0.2.5"]; break;
            case "wrong-header": runtime.ForwardedForHeaderName = "X-Real-IP"; break;
        }
        Assert.Throws<InvalidOperationException>(() => LoopbackIngress.IsConfigured(runtime, Deployment()));
    }

    [Fact]
    public void Retired_environment_wide_trust_is_rejected_even_if_local_profile_is_disabled()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Runtime:RailwayIngress:Enabled"] = "true" }).Build();
        Assert.Throws<InvalidOperationException>(() => LoopbackIngress.IsConfigured(new(), config));
    }

    [Theory]
    [InlineData("100.64.0.5")]
    [InlineData("192.0.2.12")]
    [InlineData("fd12::1234")]
    public async Task Nonloopback_network_peers_cannot_supply_trusted_forwarding_headers(string peer)
    {
        var options = new ForwardedHeadersOptions();
        TrustedForwarding.Configure(options, Policy(), Deployment());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.99";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance,
            Options.Create(options)).Invoke(context);
        Assert.Equal(IPAddress.Parse(peer), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task Real_proxy_discards_spoofs_preserves_body_and_rejects_unauthorized_access()
    {
        var (publicPort, backendPort) = FreePorts();
        var runtime = Policy(publicPort, backendPort); var config = Deployment(publicPort);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => LoopbackIngress.ConfigureBackend(o, runtime.LoopbackIngress));
        builder.Services.Configure<ForwardedHeadersOptions>(o => TrustedForwarding.Configure(o, runtime, config));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => {
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; }; });
        builder.Services.AddAuthorization();
        await using var backend = builder.Build();
        backend.UseForwardedHeaders(); backend.UseAuthentication(); backend.UseAuthorization();
        backend.MapPost("/inspect", async (HttpContext c) => Results.Json(new {
            scheme = c.Request.Scheme, host = c.Request.Host.Value,
            peer = c.Connection.RemoteIpAddress?.ToString(),
            forwardedHost = c.Request.Headers["X-Forwarded-Host"].ToString(),
            realIp = c.Request.Headers["X-Real-IP"].ToString(),
            originalHost = c.Request.Headers["X-Original-Host"].ToString(),
            csrf = c.Request.Headers["X-CSRF-TOKEN"].ToString(),
            body = await new StreamReader(c.Request.Body).ReadToEndAsync() })).AllowAnonymous();
        backend.MapGet("/protected", () => Results.Ok()).RequireAuthorization();
        backend.MapGet("/health/live", () => Results.Ok()).AllowAnonymous();
        await backend.StartAsync();
        await using var proxy = LoopbackIngress.BuildProxy(runtime, config);
        await proxy.StartAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                { BaseAddress = new Uri($"http://127.0.0.1:{publicPort}"), Timeout = TimeSpan.FromSeconds(5) };
            using var request = new HttpRequestMessage(HttpMethod.Post, "/inspect") { Content = new StringContent("synthetic body") };
            request.Headers.Host = "bliss.example.test";
            foreach (var name in new[] { "X-Forwarded-For", "X-Real-IP", "X-Original-For" })
                request.Headers.TryAddWithoutValidation(name, "198.51.100.99");
            foreach (var name in new[] { "X-Forwarded-Host", "X-Original-Host" })
                request.Headers.TryAddWithoutValidation(name, "attacker.example.test");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "http");
            request.Headers.TryAddWithoutValidation("Forwarded", "for=198.51.100.99;host=attacker.example.test;proto=http");
            request.Headers.Add("X-CSRF-TOKEN", "synthetic-csrf");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("https", json.GetProperty("scheme").GetString());
            Assert.Equal("bliss.example.test", json.GetProperty("host").GetString());
            Assert.DoesNotContain("198.51.100.99", json.GetRawText());
            Assert.DoesNotContain("attacker.example.test", json.GetRawText());
            Assert.Equal("", json.GetProperty("forwardedHost").GetString());
            Assert.Equal("", json.GetProperty("realIp").GetString());
            Assert.Equal("synthetic body", json.GetProperty("body").GetString());
            Assert.Equal("synthetic-csrf", json.GetProperty("csrf").GetString());
            using var denied = new HttpRequestMessage(HttpMethod.Get, "/protected");
            denied.Headers.Host = "bliss.example.test";
            denied.Headers.Add("X-Forwarded-User", "admin");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(denied)).StatusCode);
            using var wrongHost = new HttpRequestMessage(HttpMethod.Get, "/protected"); wrongHost.Headers.Host = "attacker.example.test";
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(wrongHost)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
            // The authenticated backend must not be reachable through a container's network address.
            var address = NetworkInterface.GetAllNetworkInterfaces().SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
            Assert.NotNull(address);
            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://{address}:{backendPort}/protected"));
        }
        finally { await proxy.StopAsync(); await backend.StopAsync(); }
    }

    private static (int, int) FreePorts()
    {
        var one = new TcpListener(IPAddress.Loopback, 0); var two = new TcpListener(IPAddress.Loopback, 0);
        one.Start(); two.Start();
        try { return (((IPEndPoint)one.LocalEndpoint).Port, ((IPEndPoint)two.LocalEndpoint).Port); }
        finally { one.Stop(); two.Stop(); }
    }
}
