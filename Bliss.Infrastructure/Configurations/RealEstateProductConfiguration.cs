using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class RealEstateProductConfiguration : IEntityTypeConfiguration<RealEstateProductRow>
{
    public void Configure(EntityTypeBuilder<RealEstateProductRow> builder)
    {
        builder.ToTable("RealEstateProducts");
        builder.HasKey(x => x.ProductId);
        builder.Property(x => x.ProductId).HasMaxLength(40);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Tier).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Exclusivity).IsRequired().HasMaxLength(40);
        builder.Property(x => x.SlotIds).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ShowcaseId).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Lifecycle).IsRequired().HasMaxLength(40);
        builder.Property(x => x.OccupancyStatus).IsRequired().HasMaxLength(40);
        builder.Property(x => x.DeviceStatus).IsRequired().HasMaxLength(40);
        builder.Property(x => x.PlatformStatus).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
    }
}
