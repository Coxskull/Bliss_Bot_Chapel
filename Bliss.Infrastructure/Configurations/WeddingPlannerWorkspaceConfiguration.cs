using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerWorkspaceConfiguration : IEntityTypeConfiguration<WeddingPlannerWorkspace>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerWorkspace> builder)
    {
        builder.ToTable("WeddingPlannerWorkspaces");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Advertiser)
            .WithMany(x => x.WeddingPlannerWorkspaces)
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.CurrentApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.CurrentApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.CurrentApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentApprovedConceptPackageVersion)
            .WithMany()
            .HasForeignKey(x => x.CurrentApprovedConceptPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentApprovedCreativePackageVersion)
            .WithMany()
            .HasForeignKey(x => x.CurrentApprovedCreativePackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentAcceptedQaReviewReportVersion)
            .WithMany()
            .HasForeignKey(x => x.CurrentAcceptedQaReviewReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentCampaignReadinessHandshakeVersion)
            .WithMany()
            .HasForeignKey(x => x.CurrentCampaignReadinessHandshakeVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Sessions)
            .WithOne(x => x.Workspace)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.AuditEvents)
            .WithOne(x => x.Workspace)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
