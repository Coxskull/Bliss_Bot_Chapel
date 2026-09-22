using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerMeasurementLearningRoleContributionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerMeasurementLearningRoleContribution>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerMeasurementLearningRoleContribution> builder)
    {
        builder.ToTable("WeddingPlannerMeasurementLearningRoleContributions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LogicalRole).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ContributionSource).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ContributionJson).IsRequired();

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.MeasurementLearningReportVersionId);
        builder.HasIndex(x => new { x.MeasurementLearningReportVersionId, x.LogicalRole }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.MeasurementLearningRoleContributions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MeasurementLearningReportVersion)
            .WithMany(x => x.RoleContributions)
            .HasForeignKey(x => x.MeasurementLearningReportVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MeasurementLearningJob)
            .WithMany(x => x.RoleContributions)
            .HasForeignKey(x => x.MeasurementLearningJobId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlContributions_Job");

        builder.HasOne(x => x.ProducingAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ProducingAgentRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPMlContributions_AgentRun");
    }
}
