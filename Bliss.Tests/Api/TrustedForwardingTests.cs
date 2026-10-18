using System.Net;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Api;

public sealed class TrustedForwardingTests
{
    [Theory]
    [InlineData("192.0.2.10", "198.51.100.10", "https")]
    [InlineData("192.0.2.11", "192.0.2.11", "http")]
    public async Task Railway_client_header_is_used_only_from_an_explicit_trusted_peer(string peer, string expectedAddress, string expectedScheme)
    {
        var options = new ForwardedHeadersOptions();
        TrustedForwarding.Configure(options, new() { KnownProxies = ["192.0.2.10"], ForwardedForHeaderName = "X-Real-IP" });
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Headers["X-Real-IP"] = "198.51.100.10";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        Assert.Equal(expectedAddress, context.Connection.RemoteIpAddress?.ToString());
        Assert.Equal(expectedScheme, context.Request.Scheme);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("X-Caller-Identity")]
    [InlineData("")]
    public void Unreviewed_identity_headers_are_rejected(string header) =>
        Assert.Throws<InvalidOperationException>(() => TrustedForwarding.Configure(new(), new() { ForwardedForHeaderName = header }));
}
