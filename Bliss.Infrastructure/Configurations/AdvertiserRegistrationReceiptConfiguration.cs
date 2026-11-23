using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AdvertiserRegistrationReceiptConfiguration : IEntityTypeConfiguration<AdvertiserRegistrationReceipt>
{
    public void Configure(EntityTypeBuilder<AdvertiserRegistrationReceipt> builder)
    {
        builder.ToTable("AdvertiserRegistrationReceipts", table =>
        {
            table.HasCheckConstraint("CK_AdvertiserRegistrationReceipts_Consent",
                "\"ProcessingConsent\" AND \"TermsAccepted\"");
            table.HasCheckConstraint("CK_AdvertiserRegistrationReceipts_RequestSha256",
                "\"RequestSha256\" ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(255);
        builder.Property(x => x.RequestSha256).IsRequired().HasMaxLength(64);
        builder.Property(x => x.PrivacyNoticeVersion).IsRequired().HasMaxLength(96);
        builder.Property(x => x.TermsVersion).IsRequired().HasMaxLength(96);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.HasIndex(x => new { x.Subject, x.AdvertiserId }).IsUnique();
        builder.HasOne(x => x.Advertiser).WithMany().HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
