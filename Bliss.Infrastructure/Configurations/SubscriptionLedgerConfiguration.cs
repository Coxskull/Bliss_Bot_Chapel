using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bliss.Infrastructure.Configurations;

public sealed class SubscriptionRegisterRowConfiguration : IEntityTypeConfiguration<SubscriptionRegisterRow>
{
    public void Configure(EntityTypeBuilder<SubscriptionRegisterRow> builder)
    {
        builder.ToTable("SubscriptionRegisterRows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceKey).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Provider).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Capability).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Classification).HasMaxLength(40);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.AccountOwner).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Plan).IsRequired().HasMaxLength(80);
        builder.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.HasIndex(x => x.ServiceKey).IsUnique();
    }
}

public sealed class SubscriptionLedgerAuditConfiguration : IEntityTypeConfiguration<SubscriptionLedgerAudit>
{
    public void Configure(EntityTypeBuilder<SubscriptionLedgerAudit> builder)
    {
        builder.ToTable("SubscriptionLedgerAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(16);
        builder.Property(x => x.ServiceKey).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Classification).HasMaxLength(40);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.MonthlyAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.EstimatedAmount).HasPrecision(18, 2);
        builder.Property(x => x.EstimatedCurrency).HasMaxLength(3);
        builder.Property(x => x.EngineeringContract).HasMaxLength(200);
        builder.Property(x => x.Provider).HasMaxLength(120);
        builder.Property(x => x.Capability).HasMaxLength(300);
        builder.Property(x => x.UsageCharges).HasMaxLength(300);
        builder.Property(x => x.Alternatives).HasMaxLength(300);
        builder.Property(x => x.BuildAlternative).HasMaxLength(300);
        builder.Property(x => x.WhyAlphaIsInsufficient).HasMaxLength(300);
        builder.Property(x => x.RequiredDate).HasMaxLength(80);
        builder.Property(x => x.RequiredOrOptional).HasMaxLength(16);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.HasIndex(x => x.RecordedAt);
    }
}

public sealed class FactoryBudgetStateConfiguration : IEntityTypeConfiguration<FactoryBudgetState>
{
    public void Configure(EntityTypeBuilder<FactoryBudgetState> builder)
    {
        builder.ToTable("FactoryBudgetStates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).IsRequired().HasMaxLength(16);
        builder.Property(x => x.CeilingAmount).HasPrecision(18, 2);
        builder.Property(x => x.CeilingCurrency).HasMaxLength(3);
        builder.Property(x => x.RecordedSpend).HasPrecision(18, 2);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.HasIndex(x => x.Scope).IsUnique();
    }
}

public sealed class FactoryBudgetAuditConfiguration : IEntityTypeConfiguration<FactoryBudgetAudit>
{
    public void Configure(EntityTypeBuilder<FactoryBudgetAudit> builder)
    {
        builder.ToTable("FactoryBudgetAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).IsRequired().HasMaxLength(16);
        builder.Property(x => x.CeilingAmount).HasPrecision(18, 2);
        builder.Property(x => x.CeilingCurrency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.RecordedSpend).HasPrecision(18, 2);
        builder.Property(x => x.Reason).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Notice).IsRequired().HasMaxLength(800);
        builder.HasIndex(x => x.RecordedAt);
    }
}
