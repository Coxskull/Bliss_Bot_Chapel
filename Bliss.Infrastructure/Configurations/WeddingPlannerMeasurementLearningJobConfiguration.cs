using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerMeasurementLearningJobConfiguration
    : IEntityTypeConfiguration<WeddingPlannerMeasurementLearningJob>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerMeasurementLearningJob> builder)
    {
        builder.ToTable("WeddingPlannerMeasurementLearningJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HandshakeStatusSnapshot).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedVariantId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SelectedConceptId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SourceLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Spend).HasPrecision(18, 6);
        builder.Property(x => x.Revenue).HasPrecision(18, 6);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.InputJson).IsRequired();
        builder.Property(x => x.InputSha256).IsRequired().HasMaxLength(64);
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
        builder.HasIndex(x => x.CampaignReadinessHandshakeVersionId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.MeasurementLearningJobs)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CampaignReadinessHandshakeVersion)
            .WithMany()
            .HasForeignKey(x => x.CampaignReadinessHandshakeVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_Handshake");

        builder.HasOne(x => x.CampaignPlacement)
            .WithMany()
            .HasForeignKey(x => x.CampaignPlacementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_Placement");

        builder.HasOne(x => x.CampaignPlacementRun)
            .WithMany()
            .HasForeignKey(x => x.CampaignPlacementRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_PlacementRun");

        builder.HasOne(x => x.BlissMatch)
            .WithMany()
            .HasForeignKey(x => x.BlissMatchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_Match");

        builder.HasOne(x => x.Campaign)
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_Campaign");

        builder.HasOne(x => x.ContentItem)
            .WithMany()
            .HasForeignKey(x => x.ContentItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_Content");

        builder.HasOne(x => x.AdInventorySlot)
            .WithMany()
            .HasForeignKey(x => x.AdInventorySlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_Slot");

        builder.HasOne(x => x.QaReviewReportVersion)
            .WithMany()
            .HasForeignKey(x => x.QaReviewReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_QaReport");

        builder.HasOne(x => x.ApprovedCreativePackageVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedCreativePackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_CreativePackage");

        builder.HasOne(x => x.SelectedCreativeAsset)
            .WithMany()
            .HasForeignKey(x => x.SelectedCreativeAssetId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_CreativeAsset");

        builder.HasOne(x => x.ApprovedConceptPackageVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedConceptPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_ConceptPackage");

        builder.HasOne(x => x.ApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_BrandDna");

        builder.HasOne(x => x.ApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_ColorProfile");

        builder.HasOne(x => x.ApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_ResearchReport");

        builder.HasOne(x => x.PerformanceAnalysisAgentRun)
            .WithMany()
            .HasForeignKey(x => x.PerformanceAnalysisAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_PerfRun");

        builder.HasOne(x => x.LearningSynthesisAgentRun)
            .WithMany()
            .HasForeignKey(x => x.LearningSynthesisAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_LearnRun");

        builder.HasOne(x => x.OutputMeasurementLearningReportVersion)
            .WithMany()
            .HasForeignKey(x => x.OutputMeasurementLearningReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlJobs_OutputReport");
    }
}
