using System.Net;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Api;

public sealed class RailwayIngressTrustTests
{
    private const string Project = "fb33c1b7-03fb-42c8-89b1-081951a738fd";
    private const string EnvironmentId = "b67a9836-36bf-4083-93f7-00c867b13f39";
    private static IConfiguration Deployment(string environment = EnvironmentId) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["RAILWAY_PROJECT_ID"] = Project, ["RAILWAY_ENVIRONMENT_ID"] = environment }).Build();
    private static BlissRuntimeOptions Approved() => new() {
        ForwardedForHeaderName = "X-Real-IP",
        RailwayIngress = new() { Enabled = true, AllEnvironmentServicesTrusted = true,
            ExpectedProjectId = Project, ExpectedEnvironmentId = EnvironmentId } };

    [Fact]
    public void Disabled_policy_preserves_explicit_peer_validation() =>
        Assert.False(RailwayIngressTrust.IsConfigured(new(), null));

    [Theory]
    [InlineData("missing-approval")]
    [InlineData("missing-project")]
    [InlineData("missing-environment")]
    [InlineData("missing-server-identity")]
    [InlineData("different-environment")]
    [InlineData("mixed-peer-policy")]
    [InlineData("wrong-client-header")]
    public void Policy_fails_closed_without_approved_exact_deployment_boundary(string problem)
    {
        var runtime = Approved(); IConfiguration? deployment = Deployment();
        switch (problem)
        {
            case "missing-approval": runtime.RailwayIngress.AllEnvironmentServicesTrusted = false; break;
            case "missing-project": runtime.RailwayIngress.ExpectedProjectId = ""; break;
            case "missing-environment": runtime.RailwayIngress.ExpectedEnvironmentId = ""; break;
            case "missing-server-identity": deployment = null; break;
            case "different-environment": deployment = Deployment(Guid.NewGuid().ToString()); break;
            case "mixed-peer-policy": runtime.KnownProxies = ["192.0.2.10"]; break;
            case "wrong-client-header": runtime.ForwardedForHeaderName = "X-Forwarded-For"; break;
        }
        Assert.Throws<InvalidOperationException>(() => TrustedForwarding.Configure(new(), runtime, deployment));
    }

    [Fact]
    public void Environment_trust_does_not_satisfy_authentication_database_or_backup_gates()
    {
        var report = ProductionPostureEvaluator.Evaluate(new() { RailwayEnvironmentTrustConfigured = true });
        Assert.False(report.Document.ProductionGatesApplied);
        Assert.False(report.Document.KnownProxiesConfigured);
        Assert.True(report.Document.RailwayEnvironmentTrustConfigured);
        Assert.Contains(report.Failures, x => x.Contains("OIDC authentication must be enabled"));
        Assert.Contains(report.Failures, x => x.Contains("ConnectionStrings:DefaultConnection"));
        Assert.Contains(report.Failures, x => x.Contains("Runtime:Backup:Provider"));
    }

    [Fact]
    public async Task Approved_environment_policy_handles_rotating_peer_and_uses_edge_client_header()
    {
        var options = new ForwardedHeadersOptions();
        TrustedForwarding.Configure(options, Approved(), Deployment());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("100.64.0.5");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Real-IP"] = "198.51.100.12";
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        await new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            NullLoggerFactory.Instance, Options.Create(options)).Invoke(context);
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal("198.51.100.12", context.Connection.RemoteIpAddress?.ToString());
        Assert.Equal(1, options.ForwardLimit);
        Assert.True(options.RequireHeaderSymmetry);
    }
}
