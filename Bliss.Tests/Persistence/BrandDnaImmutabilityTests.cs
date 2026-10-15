using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class BrandDnaImmutabilityTests
{
    [Fact]
    public async Task Stored_revisions_cannot_be_updated_or_deleted()
    {
        await using var db = TestDb.CreateContext();
        var row = new WeddingPlannerBrandDna { Id = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), Version = 1, BrandName = "Original" };
        db.WeddingPlannerBrandDna.Add(row);
        await db.SaveChangesAsync();
        row.BrandName = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var original = await db.WeddingPlannerBrandDna.SingleAsync();
        Assert.Equal("Original", original.BrandName);
        db.Remove(original);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        Assert.Single(await db.WeddingPlannerBrandDna.ToListAsync());
    }
}
