using System.Net;
using System.Text.Json;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;

public sealed class HostedDiagnosticTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Host=not-contacted.invalid;Password=synthetic-secret;SSL Mode=Require")]
    [InlineData("Host=not-contacted.invalid;Password=synthetic-secret;SSL Mode=VerifyFull;Trust Server Certificate=true")]
    [InlineData("this is not a connection string;synthetic-secret")]
    public async Task Invalid_or_unverified_database_configuration_is_refused_without_leaking_input(string configuration)
    {
        var result = await HostedDatabaseDiagnostic.RunAsync(configuration);
        Assert.False(result.Connected);
        Assert.False(result.Success);
        var evidence = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("synthetic-secret", evidence);
        Assert.DoesNotContain("not-contacted.invalid", evidence);
        Assert.DoesNotContain(configuration.Length > 0 ? configuration : "Password", evidence);
    }

    [Theory]
    [InlineData(true, false, 0, "schema_not_initialized", true, false, false)]
    [InlineData(true, false, 3, "migration_history_missing", true, false, false)]
    [InlineData(true, true, 0, "public_tables_missing", true, false, false)]
    [InlineData(true, true, 3, "", true, true, true)]
    [InlineData(false, true, 3, "tls_not_active", false, true, false)]
    public void Catalog_result_distinguishes_verified_transport_from_minimum_schema_metadata(
        bool tls, bool history, long publicTables, string expectedCode,
        bool expectedTransport, bool expectedCatalog, bool expectedSuccess)
    {
        var result = HostedDatabaseDiagnosticResult.FromReadOnlyCatalog(tls, history, publicTables);
        Assert.True(result.Connected);
        Assert.Equal(tls, result.TlsVerified);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(expectedTransport, result.ConnectionVerified);
        Assert.Equal(expectedCatalog, result.CatalogMetadataPresent);
        Assert.Equal(expectedSuccess, result.Success);

        // An empty database must fail closed even if a caller accidentally
        // supplies the historical blank ErrorCode with verified TLS.
        if (tls && !history && publicTables == 0)
            Assert.False(new HostedDatabaseDiagnosticResult(true, true, false, 0, "").Success);
    }

    [Fact]
    public void Ingress_observation_uses_transport_peer_and_ignores_forged_identity_headers()
    {
        var context = Context();
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.12";
        context.Request.Headers["X-Real-IP"] = "198.51.100.13";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        var result = IngressDiagnostic.Observe(context, new() { RestrictedPreview = true, IngressDiagnostics = true });
        Assert.NotNull(result);
        Assert.Equal("192.0.2.10", result.PeerAddress);
        Assert.False(result.TransportHttps);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Ingress_observation_is_disabled_without_both_preview_and_explicit_opt_in(bool preview, bool enabled) =>
        Assert.Null(IngressDiagnostic.Observe(Context(), new() { RestrictedPreview = preview, IngressDiagnostics = enabled }));

    [Fact]
    public void Ingress_observation_rejects_unrelated_paths_mutations_and_unstructured_probe_values()
    {
        var options = new BlissRuntimeOptions { RestrictedPreview = true, IngressDiagnostics = true };
        var context = Context(); context.Request.Path = "/api/orders";
        Assert.Null(IngressDiagnostic.Observe(context, options));
        context = Context(); context.Request.Method = "POST";
        Assert.Null(IngressDiagnostic.Observe(context, options));
        context = Context(); context.Request.QueryString = new("?bliss-ingress-probe=untrusted-log-message");
        Assert.Null(IngressDiagnostic.Observe(context, options));
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/health/live";
        context.Request.QueryString = new("?bliss-ingress-probe=5ac6d328-3277-49ab-a02d-0a626c053899");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        return context;
    }
}
