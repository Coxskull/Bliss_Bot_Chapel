using System.Reflection;
using Bliss.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Tests.Architecture;

public sealed class EconomicsPhase8BoundaryTests
{
    [Fact]
    public void Wedding_planner_exposes_one_controlled_economics_write()
    {
        var methods = typeof(WeddingPlannerController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        var handshake = methods.Single(x =>
            x.Name == "RequestEconomicsRecommendation");

        Assert.Equal(
            "sessions/{sessionId:guid}/economics/recommendations",
            handshake.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(handshake.GetCustomAttribute<AuthorizeAttribute>());
        Assert.DoesNotContain(methods, x =>
            x.Name.Contains("Quote", StringComparison.OrdinalIgnoreCase)
            || x.Name.Contains("Placement", StringComparison.OrdinalIgnoreCase)
            || x.Name.Contains("Settlement", StringComparison.OrdinalIgnoreCase)
            || x.Name.Contains("Compensation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Handshake_delegates_pricing_and_has_no_downstream_authority()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Infrastructure",
            "Persistence",
            "WeddingPlannerEconomicsHandshakeService.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("RateRecommendationService", source);
        Assert.DoesNotContain("QuoteService", source);
        Assert.DoesNotContain("CampaignPlacementService", source);
        Assert.DoesNotContain("CompensationIllustrationService", source);
        Assert.DoesNotContain("OpenAI", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
    }
}
