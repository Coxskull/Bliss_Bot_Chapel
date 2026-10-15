using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AdvertiserOpportunityConfiguration : IEntityTypeConfiguration<AdvertiserOpportunity>
{
    public void Configure(EntityTypeBuilder<AdvertiserOpportunity> builder)
    {
        builder.ToTable("AdvertiserOpportunities", table => table.HasCheckConstraint(
            "CK_AdvertiserOpportunities_AuthorityStatus",
            "\"AuthorityStatus\" IN ('UNVERIFIED', 'VERIFIED', 'SYNTHETIC_FIXTURE')"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ProductName).HasMaxLength(256);
        builder.Property(x => x.Category).HasMaxLength(128);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.MarketCountryCode).HasMaxLength(8);
        builder.Property(x => x.Language).HasMaxLength(64);
        builder.Property(x => x.CommissionPercentage).HasPrecision(7, 4);
        builder.Property(x => x.FixedFee).HasPrecision(18, 2);
        builder.Property(x => x.CommissionType).HasMaxLength(64);
        builder.Property(x => x.ExternalOpportunityId).HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("ACTIVE");
        builder.Property(x => x.AuthorityStatus).IsRequired().HasMaxLength(32).HasDefaultValue("UNVERIFIED");

        builder.HasIndex(x => x.AdvertiserProgramId);
        builder.HasIndex(x => x.ExternalOpportunityId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AuthorityStatus);

        builder.HasMany(x => x.BlissMatches)
            .WithOne(x => x.AdvertiserOpportunity)
            .HasForeignKey(x => x.AdvertiserOpportunityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
