using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerConceptRoleContributionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerConceptRoleContribution>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerConceptRoleContribution> builder)
    {
        builder.ToTable("WeddingPlannerConceptRoleContributions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LogicalRole).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ContributionJson).IsRequired();

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.WorkshopJobId);
        builder.HasIndex(x => x.ProducingAgentRunId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.ConceptPackageVersionId, x.LogicalRole }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.ConceptRoleContributions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WorkshopJob)
            .WithMany(x => x.RoleContributions)
            .HasForeignKey(x => x.WorkshopJobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProducingAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ProducingAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
