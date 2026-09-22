using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        services.AddScoped<WeddingPlannerOrchestrationService>();
        services.AddScoped<WeddingPlannerColorIntelligenceService>();
        services.AddWeddingPlannerAiProvider(configuration);
        services.AddScoped<WeddingPlannerDataSeeder>();
        return services;
    }

    public static IServiceCollection AddWeddingPlannerAiProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<WeddingPlannerAiOptions>(
            configuration.GetSection(WeddingPlannerAiOptions.SectionName));

        var providerKind = configuration
            .GetSection(WeddingPlannerAiOptions.SectionName)
            .GetValue<string>(nameof(WeddingPlannerAiOptions.Provider));

        if (string.Equals(providerKind, WeddingPlannerAiProviderKinds.OpenAiCompatible, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<OpenAiCompatibleWeddingPlannerAiProvider>((sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<WeddingPlannerAiOptions>>().Value;
                    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                    {
                        var baseUrl = options.BaseUrl.Trim();
                        if (!baseUrl.EndsWith('/'))
                        {
                            baseUrl += "/";
                        }

                        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
                    }

                    var timeoutSeconds = options.TimeoutSeconds <= 0 ? 60 : options.TimeoutSeconds;
                    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 600));
                    // ApiKey is attached per request so DefaultRequestHeaders never hold secrets.
                });

            services.AddScoped<IWeddingPlannerAiProvider>(sp =>
                sp.GetRequiredService<OpenAiCompatibleWeddingPlannerAiProvider>());
        }
        else if (string.IsNullOrWhiteSpace(providerKind)
            || string.Equals(providerKind, WeddingPlannerAiProviderKinds.Local, StringComparison.OrdinalIgnoreCase))
        {
            // Default Local — safe for Development and CI when Provider is unset or Local.
            services.AddScoped<IWeddingPlannerAiProvider, LocalDeterministicWeddingPlannerAiProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported WeddingPlannerAi:Provider '{providerKind}'. "
                + $"Use {WeddingPlannerAiProviderKinds.Local} or {WeddingPlannerAiProviderKinds.OpenAiCompatible}.");
        }

        return services;
    }

    public static DbContextOptionsBuilder UseBlissPostgres(
        this DbContextOptionsBuilder options,
        string? connectionString)
    {
        return options.UseNpgsql(connectionString);
    }
}
