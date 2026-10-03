using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class MarketplaceHandoffConfiguration : IEntityTypeConfiguration<MarketplaceHandoffRow>
{
    public void Configure(EntityTypeBuilder<MarketplaceHandoffRow> builder)
    {
        builder.ToTable("MarketplaceHandoffs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.BusinessName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CreatorName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RuleName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.MatchStatus).IsRequired().HasMaxLength(32);
        builder.Property(x => x.OverallScore).HasPrecision(9, 4);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => new { x.TenantKey, x.CreatorId, x.SourceUrl }).IsUnique();
    }
}
