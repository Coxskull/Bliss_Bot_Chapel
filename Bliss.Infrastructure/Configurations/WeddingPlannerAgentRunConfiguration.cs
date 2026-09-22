using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerAgentRunConfiguration : IEntityTypeConfiguration<WeddingPlannerAgentRun>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerAgentRun> builder)
    {
        builder.ToTable("WeddingPlannerAgentRuns");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LogicalRole).IsRequired().HasMaxLength(64);
        builder.Property(x => x.WorkerKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PromptPackVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.ProviderKey).HasMaxLength(64);
        builder.Property(x => x.ModelId).HasMaxLength(128);
        builder.Property(x => x.AdapterVersion).HasMaxLength(64);
        builder.Property(x => x.RequestId).HasMaxLength(64);
        builder.Property(x => x.ProviderRequestId).HasMaxLength(128);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Outcome).HasMaxLength(64);
        builder.Property(x => x.ErrorCode).HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.EstimatedCostUsd).HasPrecision(18, 6);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.AgentRuns)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Session)
            .WithMany(x => x.AgentRuns)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TriggerMessage)
            .WithMany()
            .HasForeignKey(x => x.TriggerMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutputMessage)
            .WithMany()
            .HasForeignKey(x => x.OutputMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OutputBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.OutputBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
