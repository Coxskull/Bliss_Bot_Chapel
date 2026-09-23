using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class QuoteApprovalDecisionConfiguration :
    IEntityTypeConfiguration<QuoteApprovalDecision>
{
    public void Configure(EntityTypeBuilder<QuoteApprovalDecision> builder)
    {
        builder.ToTable("QuoteApprovalDecisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Decision).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ReviewerLabel).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => new { x.SourceSystem, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => x.QuoteVersionId);
        builder.HasOne(x => x.Quote).WithMany(x => x.ApprovalDecisions)
            .HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuoteVersion).WithMany()
            .HasForeignKey(x => x.QuoteVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
