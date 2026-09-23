using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class PricingModelConfiguration : IEntityTypeConfiguration<PricingModel>
{
    public void Configure(EntityTypeBuilder<PricingModel> builder)
    {
        builder.ToTable("PricingModels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
