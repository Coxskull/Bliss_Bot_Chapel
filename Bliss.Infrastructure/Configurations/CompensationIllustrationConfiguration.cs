using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CompensationIllustrationConfiguration :
    IEntityTypeConfiguration<CompensationIllustration>
{
    public void Configure(EntityTypeBuilder<CompensationIllustration> builder)
    {
        builder.ToTable("CompensationIllustrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.InputSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.Quote).WithMany()
            .HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteVersion).WithMany()
            .HasForeignKey(x => x.QuoteVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteOutcome).WithMany()
            .HasForeignKey(x => x.QuoteOutcomeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CompensationRuleVersion).WithMany(x => x.Illustrations)
            .HasForeignKey(x => x.CompensationRuleVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
