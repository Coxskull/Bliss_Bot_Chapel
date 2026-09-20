using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreatorIngestionRunConfiguration : IEntityTypeConfiguration<CreatorIngestionRun>
{
    public void Configure(EntityTypeBuilder<CreatorIngestionRun> builder)
    {
        builder.ToTable("CreatorIngestionRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.IdentityKey).IsRequired().HasMaxLength(384);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(64);
        builder.Property(x => x.InputSnapshot).IsRequired().HasColumnType("text");

        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.CreatorPlatformId);
        builder.HasIndex(x => x.IdentityKey);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.CompletedAt);

        builder.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatorPlatform)
            .WithMany()
            .HasForeignKey(x => x.CreatorPlatformId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
