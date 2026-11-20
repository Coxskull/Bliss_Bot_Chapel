using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AdvertiserPrivacyRequestConfiguration : IEntityTypeConfiguration<AdvertiserPrivacyRequest>
{
    public void Configure(EntityTypeBuilder<AdvertiserPrivacyRequest> builder)
    {
        builder.ToTable("AdvertiserPrivacyRequests", table =>
        {
            table.HasCheckConstraint("CK_AdvertiserPrivacyRequests_Type",
                "\"RequestType\" IN ('ACCESS', 'CORRECTION', 'ERASURE', 'MARKETING_WITHDRAWAL', 'OBJECTION', 'ACCOUNT_CLOSURE')");
            table.HasCheckConstraint("CK_AdvertiserPrivacyRequests_Status",
                "\"Status\" IN ('RECEIVED', 'VERIFYING', 'RESOLVED', 'DECLINED')");
            table.HasCheckConstraint("CK_AdvertiserPrivacyRequests_DispositionEvidence",
                "(\\\"Status\\\" IN ('RECEIVED','VERIFYING') AND \\\"DispositionEvidenceSha256\\\" IS NULL AND \\\"DisposedBySubject\\\" IS NULL AND \\\"DisposedAt\\\" IS NULL) OR (\\\"Status\\\" IN ('RESOLVED','DECLINED') AND \\\"DispositionEvidenceSha256\\\" ~ '^[0-9a-f]{64}
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(255);
        builder.Property(x => x.RequestType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16).HasDefaultValue("RECEIVED");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.AcknowledgedBySubject).HasMaxLength(255);
        builder.Property(x => x.AcknowledgedAt);
        builder.Property(x => x.DisposedBySubject).HasMaxLength(255);
        builder.Property(x => x.DisposedAt);
        builder.Property(x => x.DispositionEvidenceSha256).HasMaxLength(64);
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.HasIndex(x => new { x.Subject, x.CreatedAt });
        builder.HasOne(x => x.Advertiser).WithMany().HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
 AND \\\"DisposedBySubject\\\" IS NOT NULL AND \\\"DisposedAt\\\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(255);
        builder.Property(x => x.RequestType).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16).HasDefaultValue("RECEIVED");
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.AcknowledgedBySubject).HasMaxLength(255);
        builder.Property(x => x.AcknowledgedAt);
        builder.Property(x => x.DisposedBySubject).HasMaxLength(255);
        builder.Property(x => x.DisposedAt);
        builder.Property(x => x.DispositionEvidenceSha256).HasMaxLength(64);
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.HasIndex(x => new { x.Subject, x.CreatedAt });
        builder.HasOne(x => x.Advertiser).WithMany().HasForeignKey(x => x.AdvertiserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
