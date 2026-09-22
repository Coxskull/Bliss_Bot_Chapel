using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerCreativeAssetConfiguration
    : IEntityTypeConfiguration<WeddingPlannerCreativeAsset>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerCreativeAsset> builder)
    {
        builder.ToTable("WeddingPlannerCreativeAssets");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VariantId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Format).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Bytes).IsRequired();
        builder.Property(x => x.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ProviderKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.AdapterVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ProviderRequestId).HasMaxLength(128);
        builder.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.CreativeProductionJobId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.CreativePackageVersionId, x.VariantId }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.CreativeAssets)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreativeProductionJob)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.CreativeProductionJobId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
