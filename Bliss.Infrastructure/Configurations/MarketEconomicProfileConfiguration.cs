using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MarketEconomicProfileConfiguration : IEntityTypeConfiguration<MarketEconomicProfile>
{
    public void Configure(EntityTypeBuilder<MarketEconomicProfile> builder)
    {
        builder.ToTable("MarketEconomicProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PurchasingPowerIndex).HasPrecision(18, 6);
        builder.Property(x => x.CompetitionLevel).HasMaxLength(32);
        builder.Property(x => x.AudienceScarcityLevel).HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.GeographicMarketId, x.Version }).IsUnique();
        builder.HasIndex(x => x.ResearchSourceId);
        builder.HasOne(x => x.GeographicMarket).WithMany()
            .HasForeignKey(x => x.GeographicMarketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResearchSource).WithMany()
            .HasForeignKey(x => x.ResearchSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
