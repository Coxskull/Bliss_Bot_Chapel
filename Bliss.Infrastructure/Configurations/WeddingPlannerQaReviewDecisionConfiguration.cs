using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerQaReviewDecisionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerQaReviewDecision>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerQaReviewDecision> builder)
    {
        builder.ToTable("WeddingPlannerQaReviewDecisions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Decision).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SelectedVariantId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.EscalationCategory).HasMaxLength(64);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.QaReviewReportVersionId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.QaReviewDecisions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QaReviewReportVersion)
            .WithMany(x => x.Decisions)
            .HasForeignKey(x => x.QaReviewReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
