using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreativeAcademyGenerationConfiguration : IEntityTypeConfiguration<CreativeAcademyGenerationRow>
{
    public void Configure(EntityTypeBuilder<CreativeAcademyGenerationRow> builder)
    {
        builder.ToTable("CreativeAcademyGenerations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Family).IsRequired().HasMaxLength(40);
        builder.Property(x => x.BrandName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.TeacherKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(40);
        builder.Property(x => x.ImagePath).IsRequired().HasMaxLength(240);
        builder.Property(x => x.MediaType).IsRequired().HasMaxLength(40);
        builder.Property(x => x.ProviderRequestId).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RecipeSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.HasIndex(x => x.RecordedAt);
    }
}
