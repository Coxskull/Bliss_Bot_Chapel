using Bliss.Domain.WeddingPlanner;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Explicit/testable Phase 8 host environment value.
/// </summary>
public sealed class ExplicitWeddingPlannerCampaignReadinessHostEnvironment(bool isDevelopmentHost)
    : IWeddingPlannerCampaignReadinessHostEnvironment
{
    public bool IsDevelopmentHost { get; } = isDevelopmentHost;
}
