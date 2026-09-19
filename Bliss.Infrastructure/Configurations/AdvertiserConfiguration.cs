using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AdvertiserConfiguration : IEntityTypeConfiguration<Advertiser>
{
    public void Configure(EntityTypeBuilder<Advertiser> builder)
    {
        builder.ToTable("Advertisers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Website).HasMaxLength(1024);
        builder.Property(x => x.CountryCode).HasMaxLength(8);
        builder.Property(x => x.Description).HasMaxLength(4000);

        builder.HasIndex(x => x.Name);

        builder.HasMany(x => x.Programs)
            .WithOne(x => x.Advertiser)
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.NetworkAccesses)
            .WithOne(x => x.Advertiser)
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
