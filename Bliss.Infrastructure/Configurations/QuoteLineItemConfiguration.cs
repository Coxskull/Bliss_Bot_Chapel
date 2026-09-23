using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class QuoteLineItemConfiguration : IEntityTypeConfiguration<QuoteLineItem>
{
    public void Configure(EntityTypeBuilder<QuoteLineItem> builder)
    {
        builder.ToTable("QuoteLineItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.UnitAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineAmount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.RecommendationLow).HasPrecision(18, 2);
        builder.Property(x => x.RecommendationTarget).HasPrecision(18, 2);
        builder.Property(x => x.RecommendationHigh).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.QuoteVersionId, x.SortOrder }).IsUnique();
        builder.HasIndex(x => x.RateRecommendationId);
        builder.HasOne(x => x.QuoteVersion).WithMany(x => x.LineItems)
            .HasForeignKey(x => x.QuoteVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RateRecommendation).WithMany()
            .HasForeignKey(x => x.RateRecommendationId).OnDelete(DeleteBehavior.Restrict);
    }
}
