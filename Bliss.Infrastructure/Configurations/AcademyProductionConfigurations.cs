using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AcademyProductionBriefConfiguration : IEntityTypeConfiguration<AcademyProductionBrief>
{
    public void Configure(EntityTypeBuilder<AcademyProductionBrief> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.WorkspaceId, x.IdempotencyKey }).IsUnique();
        b.HasOne<WeddingPlannerWorkspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WeddingPlannerBrandDna>().WithMany().HasForeignKey(x => x.BrandDnaId).OnDelete(DeleteBehavior.Restrict);
        foreach (var name in new[] { "Family", "ReferenceId", "IdempotencyKey", "ActorLabel" }) b.Property<string>(name).IsRequired().HasMaxLength(128);
        b.Property(x => x.Headline).IsRequired().HasMaxLength(500);
        b.Property(x => x.Cta).IsRequired().HasMaxLength(500);
        b.Property(x => x.ReferenceSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.RecipeSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.ReferenceSnapshotJson).IsRequired();
        b.Property(x => x.BrandSnapshotJson).IsRequired();
        b.Property(x => x.ProductionBriefJson).IsRequired();
    }
}
public sealed class AcademyProductionCandidateConfiguration : IEntityTypeConfiguration<AcademyProductionCandidate>
{
    public void Configure(EntityTypeBuilder<AcademyProductionCandidate> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<AcademyProductionBrief>().WithMany().HasForeignKey(x => x.BriefId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BriefId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.BriefId, x.IdempotencyKey }).IsUnique();
        b.Property(x => x.Label).IsRequired().HasMaxLength(200);
        b.Property(x => x.Notes).IsRequired().HasMaxLength(2000);
        b.Property(x => x.ImageSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        b.Property(x => x.ImageBytes).IsRequired();
        b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        b.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
    }
}
public sealed class AcademyProductionReviewConfiguration : IEntityTypeConfiguration<AcademyProductionReview>
{
    public void Configure(EntityTypeBuilder<AcademyProductionReview> b)
    {
        b.HasKey(x => x.Id);
        b.HasOne<AcademyProductionCandidate>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        b.HasIndex(x => new { x.CandidateId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.CandidateId, x.IdempotencyKey }).IsUnique();
        foreach (var name in new[] { "Decision", "Customization", "QualityParity", "Originality", "GeographicAuthenticity" }) b.Property<string>(name).IsRequired().HasMaxLength(32);
        foreach (var name in new[] { "Rationale", "Preserve", "Repair" }) b.Property<string>(name).IsRequired().HasMaxLength(2000);
        b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        b.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);
    }
}
