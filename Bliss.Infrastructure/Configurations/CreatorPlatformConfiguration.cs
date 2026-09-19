using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreatorPlatformConfiguration : IEntityTypeConfiguration<CreatorPlatform>
{
    public void Configure(EntityTypeBuilder<CreatorPlatform> builder)
    {
        builder.ToTable("CreatorPlatforms");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Platform).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ExternalProfileId).HasMaxLength(256);
        builder.Property(x => x.ProfileUrl).HasMaxLength(1024);

        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.ExternalProfileId);
        builder.HasIndex(x => x.Platform);
    }
}
