namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Explicit host-environment signal for Phase 8 synthetic policy.
/// Prefer injecting a concrete value in tests; production wires Development vs non-Development.
/// </summary>
public interface IWeddingPlannerCampaignReadinessHostEnvironment
{
    bool IsDevelopmentHost { get; }
}
