using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class QuoteOutcomeConfiguration : IEntityTypeConfiguration<QuoteOutcome>
{
    public void Configure(EntityTypeBuilder<QuoteOutcome> builder)
    {
        builder.ToTable("QuoteOutcomes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Response).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.QuoteVersionId);
        builder.HasIndex(x => x.NewQuoteVersionId);
        builder.HasOne(x => x.Quote).WithMany(x => x.Outcomes)
            .HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteVersion).WithMany()
            .HasForeignKey(x => x.QuoteVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.NewQuoteVersion).WithMany()
            .HasForeignKey(x => x.NewQuoteVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
