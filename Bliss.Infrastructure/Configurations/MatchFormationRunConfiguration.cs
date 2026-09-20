using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MatchFormationRunConfiguration : IEntityTypeConfiguration<MatchFormationRun>
{
    public void Configure(EntityTypeBuilder<MatchFormationRun> builder)
    {
        builder.ToTable("MatchFormationRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(64);
        builder.Property(x => x.InputSnapshot).IsRequired().HasColumnType("text");

        builder.HasIndex(x => x.BlissMatchId);
        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.AdvertiserOpportunityId);
        builder.HasIndex(x => x.RuleVersionId);
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

        builder.HasOne(x => x.AdvertiserOpportunity)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserOpportunityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RuleVersion)
            .WithMany()
            .HasForeignKey(x => x.RuleVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
