using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class EvidencePackageConfiguration : IEntityTypeConfiguration<EvidencePackageRow>
{
    public void Configure(EntityTypeBuilder<EvidencePackageRow> builder)
    {
        builder.ToTable("EvidencePackages");
        builder.HasKey(x => x.EvidenceId);
        builder.Property(x => x.EvidenceId).HasMaxLength(24);
        builder.Property(x => x.ManifestJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.ParentEvidenceId).HasMaxLength(24);
        builder.Property(x => x.ReviewStatus).IsRequired().HasMaxLength(40);
        builder.Property(x => x.DriveStatus).IsRequired().HasMaxLength(40);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ParentEvidenceId);
    }
}
