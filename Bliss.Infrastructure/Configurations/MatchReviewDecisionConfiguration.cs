using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MatchReviewDecisionConfiguration : IEntityTypeConfiguration<MatchReviewDecision>
{
    public void Configure(EntityTypeBuilder<MatchReviewDecision> builder)
    {
        builder.ToTable("MatchReviewDecisions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ReviewerLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Decision).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ResultingMatchStatus).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.InputSnapshot).IsRequired().HasColumnType("text");

        builder.HasIndex(x => x.BlissMatchId);
        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.MatchEvaluationRunId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.CompletedAt);

        builder.HasOne(x => x.BlissMatch)
            .WithMany()
            .HasForeignKey(x => x.BlissMatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MatchEvaluationRun)
            .WithMany()
            .HasForeignKey(x => x.MatchEvaluationRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
