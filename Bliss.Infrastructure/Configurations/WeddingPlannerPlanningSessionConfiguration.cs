using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerPlanningSessionConfiguration : IEntityTypeConfiguration<WeddingPlannerPlanningSession>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerPlanningSession> builder)
    {
        builder.ToTable("WeddingPlannerPlanningSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Messages)
            .WithOne(x => x.Session)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
