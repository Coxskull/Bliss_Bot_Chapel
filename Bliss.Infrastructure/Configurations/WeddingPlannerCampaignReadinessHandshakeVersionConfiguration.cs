using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class WeddingPlannerCampaignReadinessHandshakeVersionConfiguration
    : IEntityTypeConfiguration<WeddingPlannerCampaignReadinessHandshakeVersion>
{
    public void Configure(EntityTypeBuilder<WeddingPlannerCampaignReadinessHandshakeVersion> builder)
    {
        builder.ToTable("WeddingPlannerCampaignReadinessHandshakeVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SchemaVersion).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DocumentJson).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.QaReviewReportDocumentSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.CreativePackageDocumentSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedVariantId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.SelectedCreativeAssetSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.SelectedConceptId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.MatchStatusSnapshot).IsRequired().HasMaxLength(64);
        builder.Property(x => x.MatchOverallScoreSnapshot).HasPrecision(18, 6);
        builder.Property(x => x.OpportunityStatusSnapshot).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RulesFindingsJson).IsRequired();
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ActorType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ActorLabel).IsRequired().HasMaxLength(128);

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.WorkspaceId);
        builder.HasIndex(x => x.CreatorId);
        builder.HasIndex(x => x.AdvertiserOpportunityId);
        builder.HasIndex(x => x.RuleVersionId);
        builder.HasIndex(x => new { x.WorkspaceId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();

        builder.HasOne(x => x.Advertiser)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.CampaignReadinessHandshakeVersions)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.QaReviewReportVersion)
            .WithMany()
            .HasForeignKey(x => x.QaReviewReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_QaReport");

        builder.HasOne(x => x.QaAcceptDecision)
            .WithMany()
            .HasForeignKey(x => x.QaAcceptDecisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_QaDecision");

        builder.HasOne(x => x.ApprovedCreativePackageVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedCreativePackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_CreativePackage");

        builder.HasOne(x => x.CreativePackageDecision)
            .WithMany()
            .HasForeignKey(x => x.CreativePackageDecisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_CreativeDecision");

        builder.HasOne(x => x.SelectedCreativeAsset)
            .WithMany()
            .HasForeignKey(x => x.SelectedCreativeAssetId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_CreativeAsset");

        builder.HasOne(x => x.ApprovedConceptPackageVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedConceptPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_ConceptPackage");

        builder.HasOne(x => x.ApprovedBrandDnaVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBrandDnaVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_BrandDna");

        builder.HasOne(x => x.ApprovedColorProfileVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedColorProfileVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_ColorProfile");

        builder.HasOne(x => x.ApprovedResearchReportVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedResearchReportVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_ResearchReport");

        builder.HasOne(x => x.BlissMatch)
            .WithMany()
            .HasForeignKey(x => x.BlissMatchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_BlissMatch");

        builder.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_Creator");

        builder.HasOne(x => x.AdvertiserOpportunity)
            .WithMany()
            .HasForeignKey(x => x.AdvertiserOpportunityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_Opportunity");

        builder.HasOne(x => x.RuleVersion)
            .WithMany()
            .HasForeignKey(x => x.RuleVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_RuleVersion");

        builder.HasOne(x => x.Campaign)
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_Campaign");

        builder.HasOne(x => x.ContentItem)
            .WithMany()
            .HasForeignKey(x => x.ContentItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_Content");

        builder.HasOne(x => x.AdInventorySlot)
            .WithMany()
            .HasForeignKey(x => x.AdInventorySlotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_Slot");

        builder.HasOne(x => x.CampaignPlacement)
            .WithMany()
            .HasForeignKey(x => x.CampaignPlacementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_Placement");

        builder.HasOne(x => x.CampaignPlacementRun)
            .WithMany()
            .HasForeignKey(x => x.CampaignPlacementRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WPCrHandshake_PlacementRun");
    }
}
