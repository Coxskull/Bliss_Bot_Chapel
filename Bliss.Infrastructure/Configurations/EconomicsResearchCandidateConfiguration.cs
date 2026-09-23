using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class EconomicsResearchCandidateConfiguration :
    IEntityTypeConfiguration<EconomicsResearchCandidate>
{
    public void Configure(EntityTypeBuilder<EconomicsResearchCandidate> builder)
    {
        builder.ToTable("EconomicsResearchCandidates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IndustryCategory).HasMaxLength(128);
        builder.Property(x => x.Platform).HasMaxLength(64);
        builder.Property(x => x.InventorySlotType).HasMaxLength(64);
        builder.Property(x => x.Metric).IsRequired().HasMaxLength(64);
        builder.Property(x => x.NumericValue).HasPrecision(18, 6);
        builder.Property(x => x.RangeLow).HasPrecision(18, 6);
        builder.Property(x => x.RangeHigh).HasPrecision(18, 6);
        builder.Property(x => x.CurrencyCode).HasMaxLength(8);
        builder.Property(x => x.SourceName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.SourceType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ExtractionModel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.RawPayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasOne(x => x.EconomicsResearchRun).WithMany(x => x.Candidates)
            .HasForeignKey(x => x.EconomicsResearchRunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeographicMarket).WithMany()
            .HasForeignKey(x => x.GeographicMarketId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PromotedObservation).WithMany()
            .HasForeignKey(x => x.PromotedObservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
