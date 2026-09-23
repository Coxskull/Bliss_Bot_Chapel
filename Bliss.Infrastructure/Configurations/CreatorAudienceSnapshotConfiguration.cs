using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreatorAudienceSnapshotConfiguration :
    IEntityTypeConfiguration<CreatorAudienceSnapshot>
{
    public void Configure(EntityTypeBuilder<CreatorAudienceSnapshot> builder)
    {
        builder.ToTable("CreatorAudienceSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FemalePercentage).HasPrecision(7, 4);
        builder.Property(x => x.MalePercentage).HasPrecision(7, 4);
        builder.Property(x => x.PrimaryAgeRange).HasMaxLength(64);
        builder.Property(x => x.PrimaryGeography).HasMaxLength(128);
        builder.Property(x => x.Language).HasMaxLength(64);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => new { x.CreatorId, x.CapturedAt });
        builder.HasIndex(x => x.GeographicMarketId);
        builder.HasIndex(x => x.ResearchSourceId);

        builder.HasOne(x => x.Creator).WithMany()
            .HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeographicMarket).WithMany()
            .HasForeignKey(x => x.GeographicMarketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResearchSource).WithMany()
            .HasForeignKey(x => x.ResearchSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
