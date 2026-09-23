using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class HistoricalPlacementEconomicsConfiguration
    : IEntityTypeConfiguration<HistoricalPlacementEconomics>
{
    public void Configure(EntityTypeBuilder<HistoricalPlacementEconomics> builder)
    {
        builder.ToTable("HistoricalPlacementEconomics");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.QuotedAmount).HasPrecision(18, 2);
        builder.Property(x => x.ContractedAmount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.RecommendationLow).HasPrecision(18, 2);
        builder.Property(x => x.RecommendationTarget).HasPrecision(18, 2);
        builder.Property(x => x.RecommendationHigh).HasPrecision(18, 2);
        builder.Property(x => x.ExternalBenchmarkLow).HasPrecision(18, 2);
        builder.Property(x => x.ExternalBenchmarkHigh).HasPrecision(18, 2);
        builder.Property(x => x.EffectiveCpm).HasPrecision(18, 6);
        builder.Property(x => x.EffectiveCpv).HasPrecision(18, 6);
        builder.Property(x => x.ContractedVsRecommendationTargetPercentage)
            .HasPrecision(18, 6);
        builder.Property(x => x.ContractedVsExternalMidpointPercentage)
            .HasPrecision(18, 6);
        builder.Property(x => x.AlphaCompensationAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatorCompensationAmount).HasPrecision(18, 2);
        builder.Property(x => x.OtherCompensationAmount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.InputSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.CampaignPlacementId, x.RecordedAt });
        builder.HasIndex(x => x.QuoteVersionId);
        builder.HasIndex(x => x.QuoteOutcomeId);
        builder.HasIndex(x => x.QuoteLineItemId);
        builder.HasIndex(x => x.RateRecommendationId);
        builder.HasIndex(x => x.CompensationIllustrationId);
        builder.HasIndex(x => x.SupersedesHistoricalPlacementEconomicsId).IsUnique();

        builder.HasOne(x => x.CampaignPlacement)
            .WithMany(x => x.EconomicsHistory)
            .HasForeignKey(x => x.CampaignPlacementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteVersion).WithMany()
            .HasForeignKey(x => x.QuoteVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteOutcome).WithMany()
            .HasForeignKey(x => x.QuoteOutcomeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteLineItem).WithMany()
            .HasForeignKey(x => x.QuoteLineItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RateRecommendation).WithMany()
            .HasForeignKey(x => x.RateRecommendationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CompensationIllustration).WithMany()
            .HasForeignKey(x => x.CompensationIllustrationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supersedes).WithMany()
            .HasForeignKey(x => x.SupersedesHistoricalPlacementEconomicsId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
