using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class QuoteVersionConfiguration : IEntityTypeConfiguration<QuoteVersion>
{
    public void Configure(EntityTypeBuilder<QuoteVersion> builder)
    {
        builder.ToTable("QuoteVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.SubtotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.RevisionReason).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.QuoteId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasOne(x => x.Quote).WithMany(x => x.Versions)
            .HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ParentVersion).WithMany(x => x.Revisions)
            .HasForeignKey(x => x.ParentVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
