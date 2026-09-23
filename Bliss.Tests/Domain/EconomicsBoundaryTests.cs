using System.Reflection;
using Bliss.Api.Controllers;
using Bliss.Domain.Economics;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Tests.Domain;

public sealed class EconomicsBoundaryTests
{
    [Fact]
    public void Pricing_vocabulary_includes_exposure_models_not_minutes_only()
    {
        Assert.Equal("CPM", PricingModelCodes.Cpm);
        Assert.Equal("CPV", PricingModelCodes.Cpv);
        Assert.Equal("FLAT_PLACEMENT", PricingModelCodes.FlatPlacement);
        Assert.Equal("FIXED_CAMPAIGN", PricingModelCodes.FixedCampaign);
        Assert.Equal("SPONSORSHIP", PricingModelCodes.Sponsorship);
        Assert.Equal("HOST_READ", PricingModelCodes.HostRead);
        Assert.Equal("CPA", PricingModelCodes.Cpa);
        Assert.Equal("CPL", PricingModelCodes.Cpl);
        Assert.Equal("CPS", PricingModelCodes.Cps);
        Assert.Equal("HYBRID", PricingModelCodes.Hybrid);
    }

    [Fact]
    public void Observation_status_distinguishes_verified_from_invented()
    {
        Assert.Equal("VERIFIED", ObservationVerificationStatuses.Verified);
        Assert.Equal("ESTIMATED", ObservationVerificationStatuses.Estimated);
        Assert.Equal("INFERRED", ObservationVerificationStatuses.Inferred);
        Assert.Equal("UNKNOWN", ObservationVerificationStatuses.Unknown);
    }

    [Fact]
    public void Economics_vocabulary_does_not_compile_a_default_compensation_split()
    {
        var fields = typeof(CompensationParticipantRoles)
            .Assembly
            .GetTypes()
            .Where(t => t.Namespace == "Bliss.Domain.Economics")
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(f => f.IsLiteral && !f.IsInitOnly);

        Assert.DoesNotContain(fields, f => f.FieldType == typeof(decimal));
        Assert.DoesNotContain(fields, f => f.FieldType == typeof(double));
        Assert.DoesNotContain(fields, f => f.FieldType == typeof(int) && (int)f.GetRawConstantValue()! is 20 or 80);
        Assert.Contains("ALPHA", fields.Select(f => f.GetRawConstantValue()?.ToString()));
        Assert.Contains("CREATOR", fields.Select(f => f.GetRawConstantValue()?.ToString()));
    }

    [Fact]
    public void Economics_http_api_is_not_implemented_in_the_current_phase()
    {
        var prefixes = typeof(CreatorsController).Assembly.GetTypes()
            .Where(t => t.IsDefined(typeof(ApiControllerAttribute)))
            .Select(t => t.GetCustomAttribute<RouteAttribute>()?.Template)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("api/economics", prefixes);
        Assert.DoesNotContain(prefixes, p => p is not null && p.StartsWith("api/economics", StringComparison.OrdinalIgnoreCase));
    }
}
