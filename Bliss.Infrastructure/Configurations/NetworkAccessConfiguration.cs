using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class NetworkAccessConfiguration : IEntityTypeConfiguration<NetworkAccess>
{
    public void Configure(EntityTypeBuilder<NetworkAccess> builder)
    {
        builder.ToTable("NetworkAccesses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("UNKNOWN");
        builder.Property(x => x.ExternalAccountId).HasMaxLength(256);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.AffiliateNetworkId);
        builder.HasIndex(x => x.ExternalAccountId);
        builder.HasIndex(x => x.Status);
    }
}
