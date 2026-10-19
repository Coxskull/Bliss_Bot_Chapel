using System.Net;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

namespace Bliss.Api.Runtime;

public sealed class LoopbackIngressOptions
{
    public bool Enabled { get; set; }
    public string PublicOrigin { get; set; } = string.Empty;
    public int PublicPort { get; set; } = 8080;
    public int BackendPort { get; set; } = 8081;
}

// The public listener is a sanitizing proxy in the SAME container/process.
// The authenticated API binds only 127.0.0.1. No Railway network peer is trusted
// to supply forwarding headers, including PostgreSQL and future private services.
public static class LoopbackIngress
{
    public static bool IsConfigured(BlissRuntimeOptions runtime, IConfiguration? configuration)
    {
        if (configuration?.GetValue<bool>("Runtime:RailwayIngress:Enabled") == true)
            throw new InvalidOperationException("The retired environment-wide Railway ingress trust policy must be disabled.");
        var policy = runtime.LoopbackIngress;
        if (!policy.Enabled) return false;
        if (!Uri.TryCreate(policy.PublicOrigin, UriKind.Absolute, out var origin)
            || origin.Scheme != "https" || !origin.IsDefaultPort || origin.IsLoopback
            || origin.UserInfo.Length != 0 || origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0
            || configuration is null
            || configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED")
            || configuration.GetSection("Kestrel:Endpoints").GetChildren().Any()
            || !string.Equals(origin.Host, configuration["RAILWAY_PUBLIC_DOMAIN"], StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(configuration["PORT"], out var port) || port != policy.PublicPort
            || policy.PublicPort is < 1024 or > 65535 || policy.BackendPort is < 1024 or > 65535
            || policy.BackendPort == policy.PublicPort || runtime.KnownProxies.Length != 0
            || runtime.ForwardedForHeaderName != "X-Forwarded-For")
            throw new InvalidOperationException("Loopback ingress requires an exact HTTPS deployment origin, matching public port, separate unprivileged API port and no mixed proxy policy.");
        return true;
    }

    public static void ConfigureBackend(Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions options,
        LoopbackIngressOptions policy) => options.Listen(IPAddress.Loopback, policy.BackendPort);

    public static WebApplication BuildProxy(BlissRuntimeOptions runtime, IConfiguration configuration)
    {
        if (!IsConfigured(runtime, configuration))
            throw new InvalidOperationException("Loopback ingress is not enabled.");
        var policy = runtime.LoopbackIngress;
        var origin = new Uri(policy.PublicOrigin);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(policy.PublicPort));
        // Never emit full callback URLs, query strings, session codes or headers.
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Yarp.ReverseProxy", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Services.AddReverseProxy().LoadFromMemory(
            [new RouteConfig { RouteId = "local-api", ClusterId = "local-api",
                Match = new RouteMatch { Path = "/{**path}" },
                Transforms = [new Dictionary<string, string> { ["X-Forwarded"] = "Off" },
                    new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }] }],
            [new ClusterConfig { ClusterId = "local-api", Destinations = new Dictionary<string, DestinationConfig> {
                ["loopback"] = new() { Address = $"http://127.0.0.1:{policy.BackendPort}/" } } }])
            .AddTransforms(context => context.AddRequestTransform(request =>
            {
                var peer = request.HttpContext.Connection.RemoteIpAddress
                    ?? throw new InvalidOperationException("Ingress transport peer is unavailable.");
                foreach (var name in request.ProxyRequest.Headers.Select(h => h.Key).Where(name =>
                    name.Equals("Forwarded", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("X-Real-IP", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase)).ToArray())
                    request.ProxyRequest.Headers.Remove(name);
                // Scheme comes from the approved HTTPS deployment, never a caller header.
                // Client identity remains the actual transport peer, NOT unprovable X-Real-IP.
                request.ProxyRequest.Headers.Host = origin.Authority;
                request.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
                request.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", peer.ToString());
                return ValueTask.CompletedTask;
            }));
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var healthProbe = (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && context.Request.Path == "/health/live";
            if (!healthProbe && (!string.Equals(context.Request.Host.Host, origin.Host, StringComparison.OrdinalIgnoreCase)
                || context.Request.Host.Port is not (null or 443)))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            await next();
        });
        app.MapReverseProxy();
        return app;
    }
}
