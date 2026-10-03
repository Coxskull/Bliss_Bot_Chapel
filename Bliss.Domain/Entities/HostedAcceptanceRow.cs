namespace Bliss.Domain.Entities;

public sealed class HostedAcceptanceRow
{
    public Guid Id { get; set; }
    public string ReadingKey { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public bool ProductionGatesApplied { get; set; }
    public bool HostedDatabaseConfigured { get; set; }
    public bool DatabaseServerCertificateVerified { get; set; }
    public bool IdentityProviderHttps { get; set; }
    public bool BackupDeclared { get; set; }
    public bool SecretMaterialExternal { get; set; }
    public bool RoleClaimsDistinct { get; set; }
    public bool HostedAcceptanceClaimed { get; set; }
    public bool IdentityContacted { get; set; }
    public bool BackupDrillRun { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
