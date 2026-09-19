using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AffiliateNetworkConfiguration : IEntityTypeConfiguration<AffiliateNetwork>
{
    public void Configure(EntityTypeBuilder<AffiliateNetwork> builder)
    {
        builder.ToTable("AffiliateNetworks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Website).HasMaxLength(1024);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("ACTIVE");

        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.NetworkAccesses)
            .WithOne(x => x.AffiliateNetwork)
            .HasForeignKey(x => x.AffiliateNetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
