using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class DataProvenanceConfiguration : IEntityTypeConfiguration<DataProvenance>
{
    public void Configure(EntityTypeBuilder<DataProvenance> builder)
    {
        builder.ToTable("DataProvenances");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.FieldName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceName).HasMaxLength(256);
        builder.Property(x => x.SourceUrl).HasMaxLength(1024);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(64).HasDefaultValue("UNKNOWN");
        builder.Property(x => x.Notes).HasMaxLength(4000);

        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.FieldName);
        builder.HasIndex(x => x.CollectedAt);
    }
}
