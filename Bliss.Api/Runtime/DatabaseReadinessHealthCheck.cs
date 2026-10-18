using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bliss.Api.Runtime;

public sealed class DatabaseReadinessHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var connected = await db.Database.CanConnectAsync(cancellationToken);
            if (!connected)
                return HealthCheckResult.Unhealthy("Database connection failed.");
            if (!db.Database.IsRelational())
                return HealthCheckResult.Unhealthy("A relational application database is required.");
            if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                return HealthCheckResult.Unhealthy("Application database migrations are pending.");
            return HealthCheckResult.Healthy("Database connection succeeded and application migrations are current.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                $"Database readiness check failed ({exception.GetType().Name}).");
        }
    }
}
