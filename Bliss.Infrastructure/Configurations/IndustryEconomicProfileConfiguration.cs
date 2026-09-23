using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class IndustryEconomicProfileConfiguration : IEntityTypeConfiguration<IndustryEconomicProfile>
{
    public void Configure(EntityTypeBuilder<IndustryEconomicProfile> builder)
    {
        builder.ToTable("IndustryEconomicProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(128);
        builder.Property(x => x.AcquisitionCostLow).HasPrecision(18, 2);
        builder.Property(x => x.AcquisitionCostHigh).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).HasMaxLength(8);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.Category, x.GeographicMarketId, x.Version }).IsUnique();
        builder.HasIndex(x => x.ResearchSourceId);
        builder.HasOne(x => x.GeographicMarket).WithMany()
            .HasForeignKey(x => x.GeographicMarketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResearchSource).WithMany()
            .HasForeignKey(x => x.ResearchSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
