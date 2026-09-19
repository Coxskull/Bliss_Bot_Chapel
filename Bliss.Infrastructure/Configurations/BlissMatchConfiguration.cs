using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class BlissMatchConfiguration : IEntityTypeConfiguration<BlissMatch>
{
    public void Configure(EntityTypeBuilder<BlissMatch> builder)
    {
        builder.ToTable("BlissMatches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("CREATED");
        builder.Property(x => x.OverallScore).HasPrecision(7, 4);
        builder.Property(x => x.ConfidenceScore).HasPrecision(7, 4);

        // Non-unique: a Creator may have many simultaneous BlissMatches.
        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.AdvertiserOpportunityId);
        builder.HasIndex(x => x.RuleVersionId);
        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.ScoreComponents)
            .WithOne(x => x.BlissMatch)
            .HasForeignKey(x => x.BlissMatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.EligibilityChecks)
            .WithOne(x => x.BlissMatch)
            .HasForeignKey(x => x.BlissMatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
