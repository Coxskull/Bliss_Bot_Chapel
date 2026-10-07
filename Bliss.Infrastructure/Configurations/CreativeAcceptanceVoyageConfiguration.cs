using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreativeAcceptanceVoyageConfiguration : IEntityTypeConfiguration<CreativeAcceptanceVoyageRow>
{
    public void Configure(EntityTypeBuilder<CreativeAcceptanceVoyageRow> builder)
    {
        builder.ToTable("CreativeAcceptanceVoyages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VoyageKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.AdvertiserName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Market).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Niche).IsRequired().HasMaxLength(80);
        builder.Property(x => x.InventoryProductId).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(40);
        builder.Property(x => x.ReportJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.VoyageKey).IsUnique();
        builder.HasIndex(x => x.RecordedAt);
    }
}
