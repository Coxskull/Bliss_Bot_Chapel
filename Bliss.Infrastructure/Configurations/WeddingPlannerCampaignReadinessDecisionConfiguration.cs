using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerCampaignReadinessDecisionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerCampaignReadinessDecision>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerCampaignReadinessDecision> builder)
    {
        builder.ToTable("WeddingPlannerCampaignReadinessDecisions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Decision).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.CampaignReadinessHandshakeVersionId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.CampaignReadinessDecisions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CampaignReadinessHandshakeVersion)
            .WithMany(x => x.Decisions)
            .HasForeignKey(x => x.CampaignReadinessHandshakeVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
