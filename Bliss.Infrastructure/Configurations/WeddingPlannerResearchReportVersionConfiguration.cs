using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerResearchReportVersionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerResearchReportVersion>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerResearchReportVersion> builder)
    {
        builder.ToTable("WeddingPlannerResearchReportVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DocumentJson).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ProducingResearchJobId);
        builder.HasIndex(x => x.ProducingAgentRunId);
        builder.HasIndex(x => x.ApprovedBrandDnaVersionId);
        builder.HasIndex(x => new { x.WorkspaceId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.ResearchReportVersions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProducingResearchJob)
            .WithMany()
            .HasForeignKey(x => x.ProducingResearchJobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProducingAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ProducingAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.RoleContributions)
            .WithOne(x => x.ResearchReportVersion)
            .HasForeignKey(x => x.ResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Decisions)
            .WithOne(x => x.ResearchReportVersion)
            .HasForeignKey(x => x.ResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
