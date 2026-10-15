using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerBrandDnaConfiguration : IEntityTypeConfiguration<WeddingPlannerBrandDna>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerBrandDna> builder)
    {
        builder.ToTable("WeddingPlannerBrandDna");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.WorkspaceId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        builder.HasOne<WeddingPlannerWorkspace>().WithMany().HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.BrandName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Niche).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Market).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Language).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Colors).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Font).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Goals).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Restrictions).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
    }
}
