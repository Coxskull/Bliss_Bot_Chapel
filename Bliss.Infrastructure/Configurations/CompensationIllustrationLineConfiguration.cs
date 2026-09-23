using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CompensationIllustrationLineConfiguration :
    IEntityTypeConfiguration<CompensationIllustrationLine>
{
    public void Configure(EntityTypeBuilder<CompensationIllustrationLine> builder)
    {
        builder.ToTable("CompensationIllustrationLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ParticipantRole).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ParticipantLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Percentage).HasPrecision(9, 6);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.CompensationIllustrationId, x.SortOrder }).IsUnique();
        builder.HasIndex(x => x.CompensationRuleAllocationId);
        builder.HasOne(x => x.CompensationIllustration).WithMany(x => x.Lines)
            .HasForeignKey(x => x.CompensationIllustrationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CompensationRuleAllocation).WithMany()
            .HasForeignKey(x => x.CompensationRuleAllocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
