using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AdvertiserAccountMembershipConfiguration : IEntityTypeConfiguration<AdvertiserAccountMembership>
{
    public void Configure(EntityTypeBuilder<AdvertiserAccountMembership> builder)
    {
        builder.ToTable("AdvertiserAccountMemberships", table =>
            table.HasCheckConstraint("CK_AdvertiserAccountMemberships_Status",
                "\"Status\" IN ('PENDING_REVIEW', 'ACTIVE', 'REVOKED')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32).HasDefaultValue("PENDING_REVIEW");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.Subject).IsUnique();
        builder.HasIndex(x => x.AdvertiserId).IsUnique();
        builder.HasOne(x => x.Advertiser).WithMany().HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
