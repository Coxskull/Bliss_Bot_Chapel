using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBlissInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<Phase1DataSeeder>();
        services.AddScoped<Phase2DataSeeder>();
        services.AddScoped<Phase3DataSeeder>();
        services.AddScoped<MatchRuleEvaluationService>();
        services.AddScoped<CreatorIngestionService>();
        services.AddScoped<MatchFormationService>();
        services.AddScoped<MatchReviewService>();
        services.AddScoped<CampaignPlacementService>();
        services.AddScoped<WeddingPlannerService>();
        services.AddScoped<WeddingPlannerDataSeeder>();
        return services;
    }

    public static DbContextOptionsBuilder UseBlissPostgres(
        this DbContextOptionsBuilder options,
        string? connectionString)
    {
        return options.UseNpgsql(connectionString);
    }
}
