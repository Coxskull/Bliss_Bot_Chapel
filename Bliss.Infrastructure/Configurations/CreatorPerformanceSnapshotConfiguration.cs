using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreatorPerformanceSnapshotConfiguration :
    IEntityTypeConfiguration<CreatorPerformanceSnapshot>
{
    public void Configure(EntityTypeBuilder<CreatorPerformanceSnapshot> builder)
    {
        builder.ToTable("CreatorPerformanceSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EngagementRate).HasPrecision(9, 6);
        builder.Property(x => x.RetentionRate).HasPrecision(9, 6);
        builder.Property(x => x.PublishingFrequencyPerWeek).HasPrecision(9, 4);
        builder.Property(x => x.Platform).HasMaxLength(64);
        builder.Property(x => x.ContentFormat).HasMaxLength(64);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.CreatorId, x.CapturedAt });
        builder.HasIndex(x => x.ContentItemId);
        builder.HasIndex(x => x.ResearchSourceId);

        builder.HasOne(x => x.Creator).WithMany()
            .HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ContentItem).WithMany()
            .HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResearchSource).WithMany()
            .HasForeignKey(x => x.ResearchSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
