using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core CLI. Uses a local placeholder connection string only.
/// Runtime connections come from configuration / environment variables / user secrets.
/// </summary>
public sealed class BlissDbContextFactory : IDesignTimeDbContextFactory<BlissDbContext>
{
    public BlissDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BlissDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=bliss_phase1;Username=postgres;Password=postgres");

        return new BlissDbContext(optionsBuilder.Options);
    }
}
