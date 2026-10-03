using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class LearningNoteConfiguration : IEntityTypeConfiguration<LearningNoteRow>
{
    public void Configure(EntityTypeBuilder<LearningNoteRow> builder)
    {
        builder.ToTable("LearningNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProspectSlug).IsRequired().HasMaxLength(80);
        builder.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(400);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.Property(x => x.Delivery).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => new { x.ProspectSlug, x.IdempotencyKey }).IsUnique();
    }
}
