using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CampaignPerformanceEconomicsConfiguration
    : IEntityTypeConfiguration<CampaignPerformanceEconomics>
{
    public void Configure(EntityTypeBuilder<CampaignPerformanceEconomics> builder)
    {
        builder.ToTable("CampaignPerformanceEconomics");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EngagementRate).HasPrecision(18, 6);
        builder.Property(x => x.ConversionValue).HasPrecision(18, 2);
        builder.Property(x => x.ConversionValueCurrencyCode).HasMaxLength(8);
        builder.Property(x => x.AlphaContractedAmount).HasPrecision(18, 2);
        builder.Property(x => x.AlphaContractedCurrencyCode).HasMaxLength(8);
        builder.Property(x => x.EffectiveCpm).HasPrecision(18, 6);
        builder.Property(x => x.EffectiveCpv).HasPrecision(18, 6);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.InputSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.CampaignId, x.RecordedAt });
        builder.HasIndex(x => x.SupersedesCampaignPerformanceEconomicsId).IsUnique();

        builder.HasOne(x => x.Campaign)
            .WithMany(x => x.PerformanceEconomics)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supersedes).WithMany()
            .HasForeignKey(x => x.SupersedesCampaignPerformanceEconomicsId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
