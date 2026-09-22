using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerCreativeProductionJobConfiguration
    : IEntityTypeConfiguration<WeddingPlannerCreativeProductionJob>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerCreativeProductionJob> builder)
    {
        builder.ToTable("WeddingPlannerCreativeProductionJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobKind).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Objective).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.FormatsJson).IsRequired();
        builder.Property(x => x.RevisionNotes).HasMaxLength(4000);
        builder.Property(x => x.InputJson).IsRequired();
        builder.Property(x => x.InputSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedConceptId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.AssetProviderKey).HasMaxLength(64);
        builder.Property(x => x.AssetProviderAdapterVersion).HasMaxLength(64);
        builder.Property(x => x.AssetProviderRequestId).HasMaxLength(128);
        builder.Property(x => x.AssetProviderEstimatedCostUsd).HasPrecision(18, 6);
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
        builder.HasIndex(x => x.ApprovedConceptPackageVersionId);
        builder.HasIndex(x => x.ApprovedBrandDnaVersionId);
        builder.HasIndex(x => x.ApprovedColorProfileVersionId);
        builder.HasIndex(x => x.ApprovedResearchReportVersionId);
        builder.HasIndex(x => x.RevisionParentCreativePackageVersionId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.CreativeProductionJobs)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedConceptPackageVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedConceptPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_ConceptPackage");

        builder.HasOne(x => x.ApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_BrandDna");

        builder.HasOne(x => x.ApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_ColorProfile");

        builder.HasOne(x => x.ApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_ResearchReport");

        builder.HasOne(x => x.RevisionParentCreativePackageVersion)
            .WithMany()
            .HasForeignKey(x => x.RevisionParentCreativePackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_RevisionParent");

        builder.HasOne(x => x.CreativeDirectionAgentRun)
            .WithMany()
            .HasForeignKey(x => x.CreativeDirectionAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_DirectionRun");

        builder.HasOne(x => x.StrategyAdaptationAgentRun)
            .WithMany()
            .HasForeignKey(x => x.StrategyAdaptationAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_StrategyRun");

        builder.HasOne(x => x.VisualSystemAgentRun)
            .WithMany()
            .HasForeignKey(x => x.VisualSystemAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_VisualRun");

        builder.HasOne(x => x.ImageDirectionAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ImageDirectionAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_ImageRun");

        builder.HasOne(x => x.CopySystemAgentRun)
            .WithMany()
            .HasForeignKey(x => x.CopySystemAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_CopyRun");

        builder.HasOne(x => x.VariantProductionAgentRun)
            .WithMany()
            .HasForeignKey(x => x.VariantProductionAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_VariantRun");

        // OutputCreativePackageVersion FK is deferred to avoid cycles at insert time.
        builder.HasOne(x => x.OutputCreativePackageVersion)
            .WithMany()
            .HasForeignKey(x => x.OutputCreativePackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCreativeJobs_OutputPackage");
    }
}
