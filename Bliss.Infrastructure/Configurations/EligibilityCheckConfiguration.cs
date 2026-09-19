using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class EligibilityCheckConfiguration : IEntityTypeConfiguration<EligibilityCheck>
{
    public void Configure(EntityTypeBuilder<EligibilityCheck> builder)
    {
        builder.ToTable("EligibilityChecks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CheckType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Result).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ReasonCode).HasMaxLength(128);
        builder.Property(x => x.Explanation).HasMaxLength(4000);

        builder.HasIndex(x => x.BlissMatchId);
        builder.HasIndex(x => x.CheckType);
    }
}
