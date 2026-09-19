using System.Reflection;
using Bliss.Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Tests.Architecture;

public sealed class ApiSurfaceTests
{
    [Fact]
    public void Phase1_read_endpoints_are_present()
    {
        var routes = typeof(CreatorsController).Assembly.GetTypes()
            .Where(t => t.IsDefined(typeof(ApiControllerAttribute)))
            .Select(t => (
                Route: t.GetCustomAttribute<RouteAttribute>()?.Template,
                Methods: t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(m => m.GetCustomAttribute<HttpGetAttribute>()?.Template ?? "")
                    .ToArray()))
            .ToList();

        var prefixes = routes.Select(r => r.Route).ToHashSet();
        Assert.Contains("api/creators", prefixes);
        Assert.Contains("api/content-items", prefixes);
        Assert.Contains("api/advertisers", prefixes);
        Assert.Contains("api/advertiser-programs", prefixes);
        Assert.Contains("api/advertiser-opportunities", prefixes);
        Assert.Contains("api/bliss/matches", prefixes);
        Assert.Contains("api/rule-versions", prefixes);
    }
}
