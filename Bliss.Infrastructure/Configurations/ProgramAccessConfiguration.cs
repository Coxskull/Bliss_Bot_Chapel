using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class ProgramAccessConfiguration : IEntityTypeConfiguration<ProgramAccess>
{
    public void Configure(EntityTypeBuilder<ProgramAccess> builder)
    {
        builder.ToTable("ProgramAccesses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).IsRequired().HasMaxLength(64).HasDefaultValue("UNKNOWN");

        builder.HasIndex(x => x.AdvertiserProgramId);
        builder.HasIndex(x => x.Status);
    }
}
