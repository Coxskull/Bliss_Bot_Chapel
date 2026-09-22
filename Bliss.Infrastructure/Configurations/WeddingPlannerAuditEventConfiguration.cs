using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerAuditEventConfiguration : IEntityTypeConfiguration<WeddingPlannerAuditEvent>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerAuditEvent> builder)
    {
        builder.ToTable("WeddingPlannerAuditEvents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Outcome).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RequestId).HasMaxLength(64);
        builder.Property(x => x.Detail).HasMaxLength(2000);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.OccurredAt);

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Session)
            .WithMany()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Message)
            .WithMany()
            .HasForeignKey(x => x.MessageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
