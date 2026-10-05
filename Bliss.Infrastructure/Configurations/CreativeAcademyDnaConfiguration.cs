using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreativeAcademyDnaConfiguration : IEntityTypeConfiguration<CreativeAcademyDnaRow>
{
    public void Configure(EntityTypeBuilder<CreativeAcademyDnaRow> builder)
    {
        builder.ToTable("CreativeAcademyDna");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DnaKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Family).IsRequired().HasMaxLength(40);
        builder.Property(x => x.BrandName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Hero).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Palette).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Cta).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Personality).IsRequired().HasMaxLength(120);
        builder.Property(x => x.TeacherKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.DnaKey).IsUnique();
    }
}
