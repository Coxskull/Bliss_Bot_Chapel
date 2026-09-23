using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class RateRecommendationConfiguration : IEntityTypeConfiguration<RateRecommendation>
{
    public void Configure(EntityTypeBuilder<RateRecommendation> builder)
    {
        builder.ToTable("RateRecommendations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IndustryCategory).HasMaxLength(128);
        builder.Property(x => x.CampaignObjective).HasMaxLength(256);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.RangeLow).HasPrecision(18, 2);
        builder.Property(x => x.RangeTarget).HasPrecision(18, 2);
        builder.Property(x => x.RangeHigh).HasPrecision(18, 2);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.InputSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Creator).WithMany()
            .HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ContentItem).WithMany()
            .HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AdInventorySlot).WithMany()
            .HasForeignKey(x => x.AdInventorySlotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AdvertiserOpportunity).WithMany()
            .HasForeignKey(x => x.AdvertiserOpportunityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BlissMatch).WithMany()
            .HasForeignKey(x => x.BlissMatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeographicMarket).WithMany()
            .HasForeignKey(x => x.GeographicMarketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricingModel).WithMany()
            .HasForeignKey(x => x.PricingModelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricingRuleVersion).WithMany(x => x.Recommendations)
            .HasForeignKey(x => x.PricingRuleVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
