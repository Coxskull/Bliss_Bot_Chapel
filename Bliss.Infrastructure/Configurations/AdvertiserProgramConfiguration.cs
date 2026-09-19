using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class AdvertiserProgramConfiguration : IEntityTypeConfiguration<AdvertiserProgram>
{
    public void Configure(EntityTypeBuilder<AdvertiserProgram> builder)
    {
        builder.ToTable("AdvertiserPrograms");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(256);
        builder.Property(x => x.ExternalProgramId).HasMaxLength(256);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("ACTIVE");

        builder.HasIndex(x => x.AdvertiserId);
        builder.HasIndex(x => x.ExternalProgramId);
        builder.HasIndex(x => x.Status);

        builder.HasMany(x => x.Opportunities)
            .WithOne(x => x.AdvertiserProgram)
            .HasForeignKey(x => x.AdvertiserProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ProgramAccesses)
            .WithOne(x => x.AdvertiserProgram)
            .HasForeignKey(x => x.AdvertiserProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
