using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerConceptPackageVersionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerConceptPackageVersion>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerConceptPackageVersion> builder)
    {
        builder.ToTable("WeddingPlannerConceptPackageVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DocumentJson).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.ChannelFormat).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.ProducingWorkshopJobId);
        builder.HasIndex(x => x.ProducingAgentRunId);
        builder.HasIndex(x => x.ApprovedBrandDnaVersionId);
        builder.HasIndex(x => x.ApprovedColorProfileVersionId);
        builder.HasIndex(x => x.ApprovedResearchReportVersionId);
        builder.HasIndex(x => new { x.WorkspaceId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.ConceptPackageVersions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProducingWorkshopJob)
            .WithMany()
            .HasForeignKey(x => x.ProducingWorkshopJobId)
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

        builder.HasOne(x => x.ApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.RoleContributions)
            .WithOne(x => x.ConceptPackageVersion)
            .HasForeignKey(x => x.ConceptPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Decisions)
            .WithOne(x => x.ConceptPackageVersion)
            .HasForeignKey(x => x.ConceptPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
