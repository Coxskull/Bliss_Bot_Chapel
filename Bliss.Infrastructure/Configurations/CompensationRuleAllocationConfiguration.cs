using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CompensationRuleAllocationConfiguration :
    IEntityTypeConfiguration<CompensationRuleAllocation>
{
    public void Configure(EntityTypeBuilder<CompensationRuleAllocation> builder)
    {
        builder.ToTable("CompensationRuleAllocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ParticipantRole).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ParticipantLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Percentage).HasPrecision(9, 6);
        builder.HasIndex(x => new { x.CompensationRuleVersionId, x.SortOrder }).IsUnique();
        builder.HasOne(x => x.CompensationRuleVersion).WithMany(x => x.Allocations)
            .HasForeignKey(x => x.CompensationRuleVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
