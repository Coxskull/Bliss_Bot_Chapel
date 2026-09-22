using Bliss.Domain.WeddingPlanner;
using Microsoft.Extensions.Hosting;

namespace Bliss.Api.Security;

/// <summary>
/// Production wiring for Phase 8 synthetic host determination via IHostEnvironment.
/// </summary>
public sealed class AspNetWeddingPlannerCampaignReadinessHostEnvironment(IHostEnvironment environment)
    : IWeddingPlannerCampaignReadinessHostEnvironment
{
    public bool IsDevelopmentHost => environment.IsDevelopment();
}
