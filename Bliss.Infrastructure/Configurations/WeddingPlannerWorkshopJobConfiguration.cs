using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerWorkshopJobConfiguration : IEntityTypeConfiguration<WeddingPlannerWorkshopJob>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerWorkshopJob> builder)
    {
        builder.ToTable("WeddingPlannerWorkshopJobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Objective).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.CampaignGoal).IsRequired().HasMaxLength(500);
        builder.Property(x => x.AudienceFocus).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.ChannelFormat).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DeliverablesJson).IsRequired();
        builder.Property(x => x.Cta).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ConstraintsJson).IsRequired();
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
        builder.HasIndex(x => x.ApprovedBrandDnaVersionId);
        builder.HasIndex(x => x.ApprovedColorProfileVersionId);
        builder.HasIndex(x => x.ApprovedResearchReportVersionId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.WorkshopJobs)
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

        builder.HasOne(x => x.ApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StrategyAgentRun)
            .WithMany()
            .HasForeignKey(x => x.StrategyAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreativeAgentRun)
            .WithMany()
            .HasForeignKey(x => x.CreativeAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductionAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ProductionAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        // OutputConceptPackageVersion FK is deferred to avoid cycles at insert time.
        builder.HasOne(x => x.OutputConceptPackageVersion)
            .WithMany()
            .HasForeignKey(x => x.OutputConceptPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
