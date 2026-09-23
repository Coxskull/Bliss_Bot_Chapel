using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class EconomicsResearchReviewDecisionConfiguration :
    IEntityTypeConfiguration<EconomicsResearchReviewDecision>
{
    public void Configure(EntityTypeBuilder<EconomicsResearchReviewDecision> builder)
    {
        builder.ToTable("EconomicsResearchReviewDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Decision).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ReviewerLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.EconomicsResearchCandidateId).IsUnique();
        builder.HasIndex(x => x.MarketBenchmarkObservationId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.EconomicsResearchCandidate)
            .WithMany(x => x.ReviewDecisions)
            .HasForeignKey(x => x.EconomicsResearchCandidateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MarketBenchmarkObservation).WithMany()
            .HasForeignKey(x => x.MarketBenchmarkObservationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
