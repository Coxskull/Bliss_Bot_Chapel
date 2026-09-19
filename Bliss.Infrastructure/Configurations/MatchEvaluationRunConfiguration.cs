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
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("CREATED");

        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.RuleVersionId);

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
