using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CompensationRuleVersionConfiguration :
    IEntityTypeConfiguration<CompensationRuleVersion>
{
    public void Configure(EntityTypeBuilder<CompensationRuleVersion> builder)
    {
        builder.ToTable("CompensationRuleVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.DocumentJson).IsRequired().HasColumnType("jsonb");
        builder.HasIndex(x => x.Version).IsUnique();
        builder.HasIndex(x => x.EffectiveAt);
    }
}
