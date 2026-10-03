using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ContactRouteAuditConfiguration : IEntityTypeConfiguration<ContactRouteAuditRow>
{
    public void Configure(EntityTypeBuilder<ContactRouteAuditRow> builder)
    {
        builder.ToTable("ContactRouteAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Authorization).IsRequired().HasMaxLength(160);
        builder.Property(x => x.Adapter).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Transmission).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(400);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}
