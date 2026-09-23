using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class EconomicsResearchRunConfiguration :
    IEntityTypeConfiguration<EconomicsResearchRun>
{
    public void Configure(EntityTypeBuilder<EconomicsResearchRun> builder)
    {
        builder.ToTable("EconomicsResearchRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Metric).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IndustryCategory).HasMaxLength(128);
        builder.Property(x => x.Platform).HasMaxLength(64);
        builder.Property(x => x.InventorySlotType).HasMaxLength(64);
        builder.Property(x => x.ResearchQuestion).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.RequestedBy).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasOne(x => x.GeographicMarket).WithMany()
            .HasForeignKey(x => x.GeographicMarketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
