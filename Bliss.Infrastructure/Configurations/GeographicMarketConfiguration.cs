using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class GeographicMarketConfiguration : IEntityTypeConfiguration<GeographicMarket>
{
    public void Configure(EntityTypeBuilder<GeographicMarket> builder)
    {
        builder.ToTable("GeographicMarkets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(8);
        builder.Property(x => x.CityName).HasMaxLength(128);
        builder.Property(x => x.MetroName).HasMaxLength(128);
        builder.Property(x => x.MarketCode).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.HasIndex(x => x.MarketCode).IsUnique();
        builder.HasIndex(x => x.CountryCode);
    }
}
