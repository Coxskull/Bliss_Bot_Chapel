using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CoverageWeekConfiguration : IEntityTypeConfiguration<CoverageWeekRow>
{
    public void Configure(EntityTypeBuilder<CoverageWeekRow> builder)
    {
        builder.ToTable("CoverageWeeks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WeekKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.MarketLine).IsRequired().HasMaxLength(300);
        builder.Property(x => x.FuelStatus).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.WeekKey).IsUnique();
    }
}
