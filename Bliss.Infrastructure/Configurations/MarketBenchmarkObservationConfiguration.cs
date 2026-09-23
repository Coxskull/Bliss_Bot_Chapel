using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MarketBenchmarkObservationConfiguration :
    IEntityTypeConfiguration<MarketBenchmarkObservation>
{
    public void Configure(EntityTypeBuilder<MarketBenchmarkObservation> builder)
    {
        builder.ToTable("MarketBenchmarkObservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IndustryCategory).HasMaxLength(128);
        builder.Property(x => x.Platform).HasMaxLength(64);
        builder.Property(x => x.InventorySlotType).HasMaxLength(64);
        builder.Property(x => x.Metric).IsRequired().HasMaxLength(64);
        builder.Property(x => x.NumericValue).HasPrecision(18, 6);
        builder.Property(x => x.RangeLow).HasPrecision(18, 6);
        builder.Property(x => x.RangeHigh).HasPrecision(18, 6);
        builder.Property(x => x.CurrencyCode).HasMaxLength(8);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasIndex(x => new { x.GeographicMarketId, x.Metric, x.RetrievedAt });

        builder.HasOne(x => x.ResearchSource)
            .WithMany(x => x.BenchmarkObservations)
            .HasForeignKey(x => x.ResearchSourceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeographicMarket)
            .WithMany(x => x.BenchmarkObservations)
            .HasForeignKey(x => x.GeographicMarketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
