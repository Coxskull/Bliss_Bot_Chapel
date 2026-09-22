using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerBrandDnaDecisionConfiguration : IEntityTypeConfiguration<WeddingPlannerBrandDnaDecision>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerBrandDnaDecision> builder)
    {
        builder.ToTable("WeddingPlannerBrandDnaDecisions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Decision).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.BrandDnaVersionId);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.BrandDnaDecisions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
