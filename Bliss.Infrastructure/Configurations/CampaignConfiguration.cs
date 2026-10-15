using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns", table => table.HasCheckConstraint(
            "CK_Campaigns_AuthorityStatus",
            "\"AuthorityStatus\" IN ('UNVERIFIED', 'VERIFIED', 'SYNTHETIC_FIXTURE')"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("DRAFT");
        builder.Property(x => x.AuthorityStatus).IsRequired().HasMaxLength(32).HasDefaultValue("UNVERIFIED");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AdvertiserOpportunityId);
        builder.HasIndex(x => x.AuthorityStatus);

        builder.HasOne(x => x.AdvertiserOpportunity)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserOpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Placements)
            .WithOne(x => x.Campaign)
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
