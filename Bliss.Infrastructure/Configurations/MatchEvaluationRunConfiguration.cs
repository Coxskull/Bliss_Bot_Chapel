using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MatchEvaluationRunConfiguration : IEntityTypeConfiguration<MatchEvaluationRun>
{
    public void Configure(EntityTypeBuilder<MatchEvaluationRun> builder)
    {
        builder.ToTable("MatchEvaluationRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AlgorithmVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.InputSnapshot).HasColumnType("text");
        builder.Property(x => x.OutputSnapshot).HasColumnType("text");
        builder.Property(x => x.MatchStatus).HasMaxLength(64);
        builder.Property(x => x.OverallScore).HasPrecision(7, 4);
        builder.Property(x => x.ConfidenceScore).HasPrecision(7, 4);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("CREATED");

        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.RuleVersionId);
        // Non-unique: a match may have many historical evaluation runs.
        builder.HasIndex(x => x.BlissMatchId);

        builder.HasOne(x => x.BlissMatch)
            .WithMany()
            .HasForeignKey(x => x.BlissMatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RuleVersion)
            .WithMany()
            .HasForeignKey(x => x.RuleVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
