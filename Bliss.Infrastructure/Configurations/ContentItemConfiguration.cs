using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ContentItemConfiguration : IEntityTypeConfiguration<ContentItem>
{
    public void Configure(EntityTypeBuilder<ContentItem> builder)
    {
        builder.ToTable("ContentItems");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(512);
        builder.Property(x => x.ExternalContentId).HasMaxLength(256);
        builder.Property(x => x.Url).HasMaxLength(1024);

        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.ExternalContentId);

        builder.HasMany(x => x.AdInventorySlots)
            .WithOne(x => x.ContentItem)
            .HasForeignKey(x => x.ContentItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.CampaignPlacements)
            .WithOne(x => x.ContentItem)
            .HasForeignKey(x => x.ContentItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
