using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class CatalogConversationConfiguration : IEntityTypeConfiguration<CatalogConversationRow>
{
    public void Configure(EntityTypeBuilder<CatalogConversationRow> builder)
    {
        builder.ToTable("CatalogConversations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Intent).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Reply).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.ProductIds).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ShowcaseIds).IsRequired().HasMaxLength(400);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.RecordedAt);
    }
}
