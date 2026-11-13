using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CreatorAccountMembershipConfiguration : IEntityTypeConfiguration<CreatorAccountMembership>
{
    public void Configure(EntityTypeBuilder<CreatorAccountMembership> builder)
    {
        builder.ToTable("CreatorAccountMemberships", table =>
            table.HasCheckConstraint("CK_CreatorAccountMemberships_Status",
                "\"Status\" IN ('PENDING_REVIEW', 'ACTIVE', 'REVOKED')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(255);
        builder.Property(x => x.SubmittedChannelUrl).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasDefaultValue("PENDING_REVIEW");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.Subject).IsUnique();
        builder.HasIndex(x => x.CreatorId).IsUnique();
        builder.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
