using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ProspectMemoryConfiguration : IEntityTypeConfiguration<ProspectMemory>
{
    public void Configure(EntityTypeBuilder<ProspectMemory> builder)
    {
        builder.ToTable("ProspectMemories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(128);
        builder.Property(x => x.BusinessName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Market).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ProspectState).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

public sealed class SourceClipMemoryConfiguration : IEntityTypeConfiguration<SourceClipMemory>
{
    public void Configure(EntityTypeBuilder<SourceClipMemory> builder)
    {
        builder.ToTable("SourceClipMemories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Market).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Sha256).IsRequired().HasMaxLength(128);
        builder.Property(x => x.PayloadJson).IsRequired();
    }
}

public sealed class FactoryBatchMemoryConfiguration : IEntityTypeConfiguration<FactoryBatchMemory>
{
    public void Configure(EntityTypeBuilder<FactoryBatchMemory> builder)
    {
        builder.ToTable("FactoryBatchMemories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(64);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PayloadJson).IsRequired();
    }
}
