using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Fictional Wedding Planner advertisers for tenant-isolation proofs.
/// Does not seed AI content, creative assets, or Bliss matches.
/// </summary>
public sealed class WeddingPlannerDataSeeder
{
    public static readonly Guid DentalManilaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid RestaurantSantoDomingoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");

    private readonly BlissDbContext _db;

    public WeddingPlannerDataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);
        await EnsureAdvertiserAsync(
            DentalManilaId,
            "TEST Dental Manila",
            "https://example.test/dental-manila",
            "PH",
            "Fictional dental advertiser for Wedding Planner Phase 1 isolation tests.",
            now,
            cancellationToken);
        await EnsureAdvertiserAsync(
            RestaurantSantoDomingoId,
            "TEST Restaurant Santo Domingo",
            "https://example.test/restaurant-santo-domingo",
            "DO",
            "Fictional restaurant advertiser for Wedding Planner Phase 1 isolation tests.",
            now,
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAdvertiserAsync(
        Guid id,
        string name,
        string website,
        string country,
        string description,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        if (await _db.Advertisers.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return;
        }

        _db.Advertisers.Add(new Advertiser
        {
            Id = id,
            Name = name,
            Website = website,
            CountryCode = country,
            Description = description,
            CreatedAt = createdAt
        });
    }
}
