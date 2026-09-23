using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class InventoryRateBenchmarkConfiguration : IEntityTypeConfiguration<InventoryRateBenchmark>
{
    public void Configure(EntityTypeBuilder<InventoryRateBenchmark> builder)
    {
        builder.ToTable("InventoryRateBenchmarks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InventorySlotType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Platform).HasMaxLength(64);
        builder.Property(x => x.ContentFormat).HasMaxLength(64);
        builder.Property(x => x.RangeLow).HasPrecision(18, 6);
        builder.Property(x => x.RangeHigh).HasPrecision(18, 6);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.GeographicMarketId, x.InventorySlotType, x.EffectiveAt });
        builder.HasIndex(x => x.PricingModelId);
        builder.HasIndex(x => x.ResearchSourceId);
        builder.HasOne(x => x.GeographicMarket).WithMany()
            .HasForeignKey(x => x.GeographicMarketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricingModel).WithMany()
            .HasForeignKey(x => x.PricingModelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResearchSource).WithMany()
            .HasForeignKey(x => x.ResearchSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
