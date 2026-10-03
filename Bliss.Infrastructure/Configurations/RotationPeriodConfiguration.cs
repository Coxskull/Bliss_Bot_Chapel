using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class RotationPeriodConfiguration : IEntityTypeConfiguration<RotationPeriodRow>
{
    public void Configure(EntityTypeBuilder<RotationPeriodRow> builder)
    {
        builder.ToTable("RotationPeriods");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PeriodKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.RevenueLine).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.PeriodKey).IsUnique();
    }
}
