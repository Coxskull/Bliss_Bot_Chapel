using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CandidateApprovalDecisionConfiguration : IEntityTypeConfiguration<CandidateApprovalDecision>
{
    public void Configure(EntityTypeBuilder<CandidateApprovalDecision> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.CandidateId, x.ScopeKey, x.Version }).IsUnique();
        b.HasIndex(x => new { x.CandidateId, x.IdempotencyKey }).IsUnique();
        b.HasOne<AcademyProductionCandidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademyProductionReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AdInventorySlot>().WithMany().HasForeignKey(x => x.AdInventorySlotId).OnDelete(DeleteBehavior.Restrict);
        foreach (var name in new[] { "Stage", "ScopeKey", "Decision", "Status", "ActorLabel", "IdempotencyKey" }) b.Property<string>(name).IsRequired().HasMaxLength(128);
        b.Property(x => x.ActorSubject).IsRequired().HasMaxLength(256);
        b.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
    }
}
public sealed class CreativeCampaignHandoffConfiguration : IEntityTypeConfiguration<CreativeCampaignHandoff>
{
    public void Configure(EntityTypeBuilder<CreativeCampaignHandoff> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => x.QuoteLineItemId).IsUnique();
        b.HasOne<WeddingPlannerWorkspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademyProductionCandidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AcademyProductionReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CandidateApprovalDecision>().WithMany().HasForeignKey(x => x.AdvertiserApprovalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CandidateApprovalDecision>().WithMany().HasForeignKey(x => x.AlphaQaApprovalId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CandidateApprovalDecision>().WithMany().HasForeignKey(x => x.InventoryFitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<QuoteLineItem>().WithMany().HasForeignKey(x => x.QuoteLineItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CampaignPlacement>().WithMany().HasForeignKey(x => x.CampaignPlacementId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CampaignPlacementRun>().WithMany().HasForeignKey(x => x.CampaignPlacementRunId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.SnapshotJson).IsRequired();
        foreach (var name in new[] { "ActorLabel", "IdempotencyKey", "Status", "Delivery" }) b.Property<string>(name).IsRequired().HasMaxLength(128);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
    }
}
