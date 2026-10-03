using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MarketplaceMetricConfiguration : IEntityTypeConfiguration<MarketplaceMetricRow>
{
    public void Configure(EntityTypeBuilder<MarketplaceMetricRow> builder)
    {
        builder.ToTable("MarketplaceMetricReadings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MetricKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Pressure).IsRequired().HasMaxLength(160);
        builder.Property(x => x.RevenueLine).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.MetricKey).IsUnique();
    }
}
