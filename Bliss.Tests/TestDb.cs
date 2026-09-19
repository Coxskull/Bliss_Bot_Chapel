using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests;

internal static class TestDb
{
    public static BlissDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BlissDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BlissDbContext(options);
    }
}
