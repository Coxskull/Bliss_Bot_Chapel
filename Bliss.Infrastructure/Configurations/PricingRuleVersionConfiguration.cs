using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class PricingRuleVersionConfiguration : IEntityTypeConfiguration<PricingRuleVersion>
{
    public void Configure(EntityTypeBuilder<PricingRuleVersion> builder)
    {
        builder.ToTable("PricingRuleVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.DocumentJson).IsRequired().HasColumnType("jsonb");
        builder.HasIndex(x => x.Version).IsUnique();
    }
}
