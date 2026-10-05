using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI. A set ConnectionStrings__DefaultConnection
/// is used. Otherwise the local placeholder remains. Runtime connections come from
/// configuration, environment variables, or mounted secret files.
/// </summary>
public sealed class BlissDbContextFactory : IDesignTimeDbContextFactory<BlissDbContext>
{
    public const string LocalDesignTimePlaceholder =
        "Host=localhost;Port=5432;Database=bliss_phase1;Username=postgres;Password=postgres";

    public BlissDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BlissDbContext>();
        optionsBuilder.UseNpgsql(ResolveConnection(
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")));

        return new BlissDbContext(optionsBuilder.Options);
    }

    public static string ResolveConnection(string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? LocalDesignTimePlaceholder : configured.Trim();
}
