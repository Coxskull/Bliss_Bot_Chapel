using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("DRAFT");

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AdvertiserOpportunityId);

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
