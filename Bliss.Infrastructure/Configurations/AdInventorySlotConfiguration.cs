using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AdInventorySlotConfiguration : IEntityTypeConfiguration<AdInventorySlot>
{
    public void Configure(EntityTypeBuilder<AdInventorySlot> builder)
    {
        builder.ToTable("AdInventorySlots");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SlotType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IsAvailable).HasDefaultValue(true);

        builder.HasIndex(x => x.ContentItemId);
        builder.HasIndex(x => x.SlotType);

        builder.HasMany(x => x.CampaignPlacements)
            .WithOne(x => x.AdInventorySlot)
            .HasForeignKey(x => x.AdInventorySlotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
