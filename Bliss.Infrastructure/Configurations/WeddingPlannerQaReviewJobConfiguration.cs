using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerQaReviewJobConfiguration
    : IEntityTypeConfiguration<WeddingPlannerQaReviewJob>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerQaReviewJob> builder)
    {
        builder.ToTable("WeddingPlannerQaReviewJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReviewObjective).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.FocusAreasJson).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.InputJson).IsRequired();
        builder.Property(x => x.InputSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreativePackageDocumentSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedVariantId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SelectedCreativeAssetSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedCreativeAssetContentType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedConceptId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ErrorCode).HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => x.ApprovedCreativePackageVersionId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.QaReviewJobs)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedCreativePackageVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedCreativePackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_CreativePackage");

        builder.HasOne(x => x.CreativePackageDecision)
            .WithMany()
            .HasForeignKey(x => x.CreativePackageDecisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_CreativeDecision");

        builder.HasOne(x => x.SelectedCreativeAsset)
            .WithMany()
            .HasForeignKey(x => x.SelectedCreativeAssetId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_CreativeAsset");

        builder.HasOne(x => x.ApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_BrandDna");

        builder.HasOne(x => x.ApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_ColorProfile");

        builder.HasOne(x => x.ApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_ResearchReport");

        builder.HasOne(x => x.ChaperoneReviewAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ChaperoneReviewAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_ChaperoneRun");

        builder.HasOne(x => x.QaInspectionAgentRun)
            .WithMany()
            .HasForeignKey(x => x.QaInspectionAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_QaInspectionRun");

        // OutputQaReviewReportVersion FK deferred to avoid cycles at insert time (Phase 6 style).
        builder.HasOne(x => x.OutputQaReviewReportVersion)
            .WithMany()
            .HasForeignKey(x => x.OutputQaReviewReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaJobs_OutputReport");
    }
}
