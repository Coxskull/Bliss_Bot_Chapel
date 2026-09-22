using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerBrandDnaVersionConfiguration : IEntityTypeConfiguration<WeddingPlannerBrandDnaVersion>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerBrandDnaVersion> builder)
    {
        builder.ToTable("WeddingPlannerBrandDnaVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DocumentJson).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.WorkspaceId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.BrandDnaVersions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProducingAgentRun)
            .WithMany()
            .HasForeignKey(x => x.ProducingAgentRunId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Decisions)
            .WithOne(x => x.BrandDnaVersion)
            .HasForeignKey(x => x.BrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
