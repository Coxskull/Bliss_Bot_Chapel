using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class EconomicsPhaseAcceptanceConfiguration : IEntityTypeConfiguration<EconomicsPhaseAcceptanceRow>
{
    public void Configure(EntityTypeBuilder<EconomicsPhaseAcceptanceRow> builder)
    {
        builder.ToTable("EconomicsPhaseAcceptances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PhaseKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.HistoryLine).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.PhaseKey).IsUnique();
    }
}
