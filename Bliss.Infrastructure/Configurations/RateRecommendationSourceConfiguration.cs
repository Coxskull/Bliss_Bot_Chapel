using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class RateRecommendationSourceConfiguration :
    IEntityTypeConfiguration<RateRecommendationSource>
{
    public void Configure(EntityTypeBuilder<RateRecommendationSource> builder)
    {
        builder.ToTable("RateRecommendationSources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.RateRecommendationId);
        builder.HasOne(x => x.RateRecommendation).WithMany(x => x.Sources)
            .HasForeignKey(x => x.RateRecommendationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResearchSource).WithMany()
            .HasForeignKey(x => x.ResearchSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.InventoryRateBenchmark).WithMany()
            .HasForeignKey(x => x.InventoryRateBenchmarkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatorAudienceSnapshot).WithMany()
            .HasForeignKey(x => x.CreatorAudienceSnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatorPerformanceSnapshot).WithMany()
            .HasForeignKey(x => x.CreatorPerformanceSnapshotId).OnDelete(DeleteBehavior.Restrict);
    }
}
