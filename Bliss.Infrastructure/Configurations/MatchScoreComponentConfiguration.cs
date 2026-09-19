using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MatchScoreComponentConfiguration : IEntityTypeConfiguration<MatchScoreComponent>
{
    public void Configure(EntityTypeBuilder<MatchScoreComponent> builder)
    {
        builder.ToTable("MatchScoreComponents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ComponentName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Score).HasPrecision(7, 4);
        builder.Property(x => x.Weight).HasPrecision(7, 4);
        builder.Property(x => x.Explanation).HasMaxLength(4000);

        builder.HasIndex(x => x.BlissMatchId);
        builder.HasIndex(x => x.ComponentName);
    }
}
