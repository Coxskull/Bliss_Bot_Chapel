using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ControlledAiRunConfiguration : IEntityTypeConfiguration<ControlledAiRun>
{
    public void Configure(EntityTypeBuilder<ControlledAiRun> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.Status, x.CreatedAt });
        b.Property(x => x.Status).IsConcurrencyToken().IsRequired().HasMaxLength(32);
        foreach (var field in new[] { "Kind", "Provider", "Model", "PromptVersion", "IdempotencyKey", "ActorLabel", "FailureCode" }) b.Property<string>(field).IsRequired().HasMaxLength(128);
        b.Property(x => x.InputSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.ExecutionConfigSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.OutputSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.InputSnapshotJson).IsRequired();
        b.Property(x => x.OutputText).IsRequired().HasMaxLength(8000);
        b.Property(x => x.ProviderRequestId).IsRequired().HasMaxLength(200);
        b.Property(x => x.CostStatus).IsRequired().HasMaxLength(32);
        b.Property(x => x.ReservedUsd).HasPrecision(18, 6);
        b.Property(x => x.ActualUsd).HasPrecision(18, 6);
        b.HasOne<WeddingPlannerWorkspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WeddingPlannerPlanningSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademyProductionBrief>().WithMany().HasForeignKey(x => x.BriefId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademyProductionCandidate>().WithMany().HasForeignKey(x => x.AttachedCandidateId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ControlledAiDailyUsageConfiguration : IEntityTypeConfiguration<ControlledAiDailyUsage>
{
    public void Configure(EntityTypeBuilder<ControlledAiDailyUsage> b)
    {
        b.HasKey(x => x.Day); b.Property(x => x.ReservedUsd).HasPrecision(18, 6); b.Property(x => x.Revision).IsConcurrencyToken();
    }
}
public sealed class ControlledAiEventConfiguration : IEntityTypeConfiguration<ControlledAiEvent>
{
    public void Configure(EntityTypeBuilder<ControlledAiEvent> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<ControlledAiRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.RunId, x.Action }).IsUnique();
        b.Property(x => x.Action).IsRequired().HasMaxLength(64);
        b.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
    }
}
public sealed class ControlledAiBudgetLedgerConfiguration : IEntityTypeConfiguration<ControlledAiBudgetLedger>
{
    public void Configure(EntityTypeBuilder<ControlledAiBudgetLedger> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ReservedUsd).HasPrecision(18, 6); b.Property(x => x.Revision).IsConcurrencyToken();
    }
}
