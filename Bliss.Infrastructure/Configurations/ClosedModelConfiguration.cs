using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ClosedModelConfiguration : IEntityTypeConfiguration<ClosedModelRow>
{
    public void Configure(EntityTypeBuilder<ClosedModelRow> builder)
    {
        builder.ToTable("ClosedModelReadings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReadingKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.ReadingKey).IsUnique();
    }
}
