using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ResearchSourceConfiguration : IEntityTypeConfiguration<ResearchSource>
{
    public void Configure(EntityTypeBuilder<ResearchSource> builder)
    {
        builder.ToTable("ResearchSources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.SourceUrl).HasMaxLength(2048);
        builder.Property(x => x.SourceType).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.Name);
    }
}
