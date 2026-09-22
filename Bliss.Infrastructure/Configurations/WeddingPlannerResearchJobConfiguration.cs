using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerResearchJobConfiguration : IEntityTypeConfiguration<WeddingPlannerResearchJob>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerResearchJob> builder)
    {
        builder.ToTable("WeddingPlannerResearchJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Topic).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Objective).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.QuestionsJson).IsRequired();
        builder.Property(x => x.Geography).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Language).IsRequired().HasMaxLength(32);
        builder.Property(x => x.AllowedDomainsJson).IsRequired();
        builder.Property(x => x.InputJson).IsRequired();
        builder.Property(x => x.InputSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ResearchProviderKey).HasMaxLength(64);
        builder.Property(x => x.ResearchAdapterVersion).HasMaxLength(64);
        builder.Property(x => x.ResearchProviderRequestId).HasMaxLength(128);
        builder.Property(x => x.ResearchWorkerKey).HasMaxLength(64);
        builder.Property(x => x.ResearchEstimatedCostUsd).HasPrecision(18, 6);
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
        builder.HasIndex(x => x.ApprovedBrandDnaVersionId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.ResearchJobs)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResearchAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ResearchAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EvidenceAgentRun)
            .WithMany()
            .HasForeignKey(x => x.EvidenceAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SynthesisRiskAgentRun)
            .WithMany()
            .HasForeignKey(x => x.SynthesisRiskAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        // OutputResearchReportVersion FK is configured from the report side / deferred to avoid cycles at insert time.
        builder.HasOne(x => x.OutputResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.OutputResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
