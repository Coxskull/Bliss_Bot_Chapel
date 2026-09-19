using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreatorConfiguration : IEntityTypeConfiguration<Creator>
{
    public void Configure(EntityTypeBuilder<Creator> builder)
    {
        builder.ToTable("Creators");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.CountryCode).HasMaxLength(8);
        builder.Property(x => x.PrimaryLanguage).HasMaxLength(128);
        builder.Property(x => x.FemalePercentage).HasPrecision(5, 2);
        builder.Property(x => x.MalePercentage).HasPrecision(5, 2);
        builder.Property(x => x.PrimaryAgeRange).HasMaxLength(64);
        builder.Property(x => x.PrimaryGeography).HasMaxLength(256);
        builder.Property(x => x.EngagementLevel).HasMaxLength(64);

        builder.HasIndex(x => x.Name);

        builder.HasMany(x => x.Platforms)
            .WithOne(x => x.Creator)
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ContentItems)
            .WithOne(x => x.Creator)
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.BlissMatches)
            .WithOne(x => x.Creator)
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
