using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("Quotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.RequestedBy).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.AdvertiserOpportunityId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.AdvertiserOpportunity).WithMany()
            .HasForeignKey(x => x.AdvertiserOpportunityId).OnDelete(DeleteBehavior.Restrict);
    }
}
