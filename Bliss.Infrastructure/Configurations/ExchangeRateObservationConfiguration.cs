using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ExchangeRateObservationConfiguration : IEntityTypeConfiguration<ExchangeRateObservation>
{
    public void Configure(EntityTypeBuilder<ExchangeRateObservation> builder)
    {
        builder.ToTable("ExchangeRateObservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BaseCurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.QuoteCurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.Rate).HasPrecision(18, 8);
        builder.Property(x => x.ConfidenceLevel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.VerificationStatus).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasIndex(x => new { x.BaseCurrencyCode, x.QuoteCurrencyCode, x.ObservedAt });
        builder.HasIndex(x => x.ResearchSourceId);
        builder.HasOne(x => x.ResearchSource).WithMany()
            .HasForeignKey(x => x.ResearchSourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
