using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class HostedAcceptanceService(
    BlissDbContext database,
    IWebHostEnvironment environment,
    Bliss.Api.Runtime.ProductionPostureReport posture)
{
    public async Task<HostedBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var history = await database.HostedAcceptanceReadings.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new HostedBoard(environment.EnvironmentName, posture.Document, history);
    }

    public async Task<HostedWrite> StoreAsync(string? readingKey, bool claimHosted, CancellationToken cancellationToken)
    {
        var document = posture.Document;
        var stored = await database.HostedAcceptanceReadings.AsNoTracking().ToListAsync(cancellationToken);
        var reading = HostedAcceptance.Store(
            environment.EnvironmentName,
            document.ProductionGatesApplied,
            document.HostedDatabaseConfigured,
            document.DatabaseServerCertificateVerified,
            document.IdentityProviderHttps,
            document.BackupDeclared,
            document.SecretMaterialExternal,
            document.RoleClaimsDistinct,
            claimHosted,
            readingKey,
            stored.Select(item => item.ReadingKey).ToList());
        if (reading.Duplicate)
        {
            var prior = stored.First(item => item.ReadingKey == reading.ReadingKey);
            return new HostedWrite(prior, true, false);
        }

        var row = new HostedAcceptanceRow
        {
            Id = Guid.NewGuid(),
            ReadingKey = reading.ReadingKey,
            EnvironmentName = reading.EnvironmentName,
            ProductionGatesApplied = reading.ProductionGatesApplied,
            HostedDatabaseConfigured = reading.HostedDatabaseConfigured,
            DatabaseServerCertificateVerified = reading.DatabaseServerCertificateVerified,
            IdentityProviderHttps = reading.IdentityProviderHttps,
            BackupDeclared = reading.BackupDeclared,
            SecretMaterialExternal = reading.SecretMaterialExternal,
            RoleClaimsDistinct = reading.RoleClaimsDistinct,
            HostedAcceptanceClaimed = false,
            IdentityContacted = false,
            BackupDrillRun = false,
            Notice = reading.Notice,
            Delivery = reading.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.HostedAcceptanceReadings.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return new HostedWrite(row, false, true);
    }
}

public sealed record HostedBoard(
    string EnvironmentName,
    Bliss.Api.Runtime.ProductionPostureDocument Posture,
    IReadOnlyList<HostedAcceptanceRow> History);

public sealed record HostedWrite(HostedAcceptanceRow Reading, bool Duplicate, bool Written);
