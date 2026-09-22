using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerQaRoleContributionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerQaRoleContribution>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerQaRoleContribution> builder)
    {
        builder.ToTable("WeddingPlannerQaRoleContributions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LogicalRole).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ContributionSource).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ContributionJson).IsRequired();

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.QaReviewReportVersionId);
        builder.HasIndex(x => new { x.QaReviewReportVersionId, x.LogicalRole }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.QaRoleContributions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QaReviewReportVersion)
            .WithMany(x => x.RoleContributions)
            .HasForeignKey(x => x.QaReviewReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QaReviewJob)
            .WithMany(x => x.RoleContributions)
            .HasForeignKey(x => x.QaReviewJobId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaContributions_Job");

        builder.HasOne(x => x.ProducingAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ProducingAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("FK_WPQaContributions_AgentRun");
    }
}
