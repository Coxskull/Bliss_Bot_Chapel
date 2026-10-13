using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CampaignObservationConfiguration : IEntityTypeConfiguration<CampaignObservation>
{
    public void Configure(EntityTypeBuilder<CampaignObservation> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        b.HasOne<WeddingPlannerWorkspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CreativeCampaignHandoff>().WithMany().HasForeignKey(x => x.HandoffId).OnDelete(DeleteBehavior.Restrict);
        foreach (var name in new[] { "Kind", "EvidenceStatus", "SourceLabel", "ActorLabel", "IdempotencyKey" }) b.Property<string>(name).IsRequired().HasMaxLength(128);
        b.Property(x => x.Evidence).IsRequired().HasMaxLength(4000);
        b.Property(x => x.LineageSnapshotJson).IsRequired();
        b.Property(x => x.LineageSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.ActorSubject).IsRequired().HasMaxLength(256);
    }
}
public sealed class CampaignLearningProposalConfiguration : IEntityTypeConfiguration<CampaignLearningProposal>
{
    public void Configure(EntityTypeBuilder<CampaignLearningProposal> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        b.HasOne<WeddingPlannerWorkspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CampaignObservation>().WithMany().HasForeignKey(x => x.ObservationId).OnDelete(DeleteBehavior.Restrict);
        foreach (var name in new[] { "Hypothesis", "Experiment", "SuccessCriterion" }) b.Property<string>(name).IsRequired().HasMaxLength(2000);
        foreach (var name in new[] { "Status", "ActorLabel", "IdempotencyKey" }) b.Property<string>(name).IsRequired().HasMaxLength(128);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.ActorSubject).IsRequired().HasMaxLength(256);
    }
}
public sealed class CampaignLearningReviewConfiguration : IEntityTypeConfiguration<CampaignLearningReview>
{
    public void Configure(EntityTypeBuilder<CampaignLearningReview> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ProposalId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.ProposalId, x.IdempotencyKey }).IsUnique();
        b.HasOne<CampaignLearningProposal>().WithMany().HasForeignKey(x => x.ProposalId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        foreach (var name in new[] { "Decision", "ActorLabel", "IdempotencyKey" }) b.Property<string>(name).IsRequired().HasMaxLength(128);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.ActorSubject).IsRequired().HasMaxLength(256);
    }
}
