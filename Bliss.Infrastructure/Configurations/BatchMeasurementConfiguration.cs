using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class BatchMeasurementConfiguration : IEntityTypeConfiguration<BatchMeasurementRow>
{
    public void Configure(EntityTypeBuilder<BatchMeasurementRow> builder)
    {
        builder.ToTable("BatchMeasurements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.ResourceLine).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CostLine).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Failures).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}
