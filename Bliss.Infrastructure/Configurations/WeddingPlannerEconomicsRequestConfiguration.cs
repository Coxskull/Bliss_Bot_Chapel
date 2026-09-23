using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerEconomicsRequestConfiguration
    : IEntityTypeConfiguration<WeddingPlannerEconomicsRequest>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerEconomicsRequest> builder)
    {
        builder.ToTable("WeddingPlannerEconomicsRequests");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IndustryCategory).HasMaxLength(128);
        builder.Property(x => x.CampaignObjective).HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.RequestedBy).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.SessionId, x.CreatedAt });
        builder.HasIndex(x => x.RateRecommendationId).IsUnique();

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.EconomicsRequests)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Session)
            .WithMany(x => x.EconomicsRequests)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BlissMatch)
            .WithMany()
            .HasForeignKey(x => x.BlissMatchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AdInventorySlot)
            .WithMany()
            .HasForeignKey(x => x.AdInventorySlotId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GeographicMarket)
            .WithMany()
            .HasForeignKey(x => x.GeographicMarketId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricingModel)
            .WithMany()
            .HasForeignKey(x => x.PricingModelId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RateRecommendation)
            .WithMany()
            .HasForeignKey(x => x.RateRecommendationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
