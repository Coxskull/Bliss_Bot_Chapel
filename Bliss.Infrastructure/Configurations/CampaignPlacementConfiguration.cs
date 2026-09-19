using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CampaignPlacementConfiguration : IEntityTypeConfiguration<CampaignPlacement>
{
    public void Configure(EntityTypeBuilder<CampaignPlacement> builder)
    {
        builder.ToTable("CampaignPlacements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("CREATED");

        // Non-unique: a ContentItem may have many independent CampaignPlacements.
        builder.HasIndex(x => x.CampaignId);
        builder.HasIndex(x => x.ContentItemId);
        builder.HasIndex(x => x.AdInventorySlotId);
        builder.HasIndex(x => x.Status);
    }
}
