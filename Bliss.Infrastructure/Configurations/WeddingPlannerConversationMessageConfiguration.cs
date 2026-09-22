using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerConversationMessageConfiguration : IEntityTypeConfiguration<WeddingPlannerConversationMessage>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerConversationMessage> builder)
    {
        builder.ToTable("WeddingPlannerConversationMessages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(8000);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => new { x.SessionId, x.SequenceNumber }).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Workspace)
            .WithMany()
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
