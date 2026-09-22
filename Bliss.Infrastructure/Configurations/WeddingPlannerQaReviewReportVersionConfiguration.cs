using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerQaReviewReportVersionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerQaReviewReportVersion>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerQaReviewReportVersion> builder)
    {
        builder.ToTable("WeddingPlannerQaReviewReportVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DocumentJson).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.CreativePackageDocumentSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedVariantId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SelectedCreativeAssetSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedConceptId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => new { x.WorkspaceId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.QaReviewReportVersions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProducingQaReviewJob)
            .WithMany()
            .HasForeignKey(x => x.ProducingQaReviewJobId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_Job");

        builder.HasOne(x => x.ProducingAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ProducingAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_AgentRun");

        builder.HasOne(x => x.ApprovedCreativePackageVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedCreativePackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_CreativePackage");

        builder.HasOne(x => x.CreativePackageDecision)
            .WithMany()
            .HasForeignKey(x => x.CreativePackageDecisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_CreativeDecision");

        builder.HasOne(x => x.SelectedCreativeAsset)
            .WithMany()
            .HasForeignKey(x => x.SelectedCreativeAssetId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_CreativeAsset");

        builder.HasOne(x => x.ApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_BrandDna");

        builder.HasOne(x => x.ApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_ColorProfile");

        builder.HasOne(x => x.ApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaReports_ResearchReport");
    }
}
