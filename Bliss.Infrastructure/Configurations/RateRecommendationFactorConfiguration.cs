using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class RateRecommendationFactorConfiguration :
    IEntityTypeConfiguration<RateRecommendationFactor>
{
    public void Configure(EntityTypeBuilder<RateRecommendationFactor> builder)
    {
        builder.ToTable("RateRecommendationFactors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FactorCode).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Label).IsRequired().HasMaxLength(256);
        builder.Property(x => x.NumericValue).HasPrecision(18, 6);
        builder.Property(x => x.AdjustmentMultiplier).HasPrecision(9, 6);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.HasIndex(x => new { x.RateRecommendationId, x.SortOrder });
        builder.HasOne(x => x.RateRecommendation).WithMany(x => x.Factors)
            .HasForeignKey(x => x.RateRecommendationId).OnDelete(DeleteBehavior.Restrict);
    }
}
