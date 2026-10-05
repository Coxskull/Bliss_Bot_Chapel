using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreativeAcademyLessonConfiguration : IEntityTypeConfiguration<CreativeAcademyLessonRow>
{
    public void Configure(EntityTypeBuilder<CreativeAcademyLessonRow> builder)
    {
        builder.ToTable("CreativeAcademyLessons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LessonKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Family).IsRequired().HasMaxLength(40);
        builder.Property(x => x.BrandName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Headline).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.ProperNouns).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ImagePath).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Defects).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.PreserveList).IsRequired().HasMaxLength(500);
        builder.Property(x => x.RepairList).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.LessonKey).IsUnique();
    }
}
