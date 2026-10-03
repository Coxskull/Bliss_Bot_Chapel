using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class LaneTempoStateConfiguration : IEntityTypeConfiguration<LaneTempoState>
{
    public void Configure(EntityTypeBuilder<LaneTempoState> builder)
    {
        builder.ToTable("LaneTempoStates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Lane).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Tempo).IsRequired().HasMaxLength(16);
        builder.Property(x => x.CeilingAmount).HasPrecision(18, 2);
        builder.Property(x => x.CeilingCurrency).HasMaxLength(3);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.HasIndex(x => x.Lane).IsUnique();
    }
}

public sealed class LaneTempoAuditConfiguration : IEntityTypeConfiguration<LaneTempoAudit>
{
    public void Configure(EntityTypeBuilder<LaneTempoAudit> builder)
    {
        builder.ToTable("LaneTempoAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Lane).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Tempo).IsRequired().HasMaxLength(16);
        builder.Property(x => x.CeilingAmount).HasPrecision(18, 2);
        builder.Property(x => x.CeilingCurrency).HasMaxLength(3);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.HasIndex(x => x.RecordedAt);
    }
}
