using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class HostedAcceptanceConfiguration : IEntityTypeConfiguration<HostedAcceptanceRow>
{
    public void Configure(EntityTypeBuilder<HostedAcceptanceRow> builder)
    {
        builder.ToTable("HostedAcceptanceReadings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReadingKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.EnvironmentName).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.ReadingKey).IsUnique();
    }
}
