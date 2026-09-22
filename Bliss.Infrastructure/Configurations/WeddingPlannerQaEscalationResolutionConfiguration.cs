using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerQaEscalationResolutionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerQaEscalationResolution>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerQaEscalationResolution> builder)
    {
        builder.ToTable("WeddingPlannerQaEscalationResolutions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Resolution).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.ExceptionRationale).HasMaxLength(2000);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.QaEscalationCaseId).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.QaEscalationResolutions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QaEscalationCase)
            .WithOne(x => x.Resolution)
            .HasForeignKey<WeddingPlannerQaEscalationResolution>(x => x.QaEscalationCaseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaResolutions_Case");

        builder.HasOne(x => x.QaReviewReportVersion)
            .WithMany()
            .HasForeignKey(x => x.QaReviewReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPQaResolutions_Report");
    }
}
