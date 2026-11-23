using Bliss.Api.Runtime;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

public sealed class RegistrationReleaseReadinessMarkersTests
{
    private static ProductionPostureReport Report(bool configured) => new(
        new ProductionPostureDocument(
            ProductionGatesApplied: configured,
            KeysEncryptedAtRest: configured,
            HostedDatabaseConfigured: configured,
            DatabaseTransportEncrypted: configured,
            DatabaseServerCertificateVerified: configured,
            IdentityProviderHttps: configured,
            RoleClaimType: "roles",
            RoleClaimsDistinct: configured,
            ConfiguredRoles: ["bliss.viewer", "bliss.admin"],
            KnownProxiesConfigured: configured,
            BackupDeclared: configured,
            BackupProvider: configured ? "owner-provided" : string.Empty,
            BackupSchedule: configured ? "daily" : string.Empty,
            BackupRetentionDays: configured ? 14 : 0,
            SecretMaterialExternal: configured),
        configured ? [] : ["synthetic failure containing a PRIVATE_SECRET_VALUE"]);

    [Fact]
    public void Locked_production_status_is_fixed_fail_closed_and_secret_free()
    {
        var auth = new BlissAuthenticationOptions
        {
            Enabled = false,
            SupabaseEnabled = false,
            ClientSecret = "PRIVATE_SECRET_VALUE",
            ClientId = "PRIVATE_CLIENT_ID"
        };
        var lines = RegistrationReleaseReadinessMarkers.Build(
            Report(false), auth, new BlissRuntimeOptions { RestrictedPreview = true });
        Assert.Contains("TASK3B_RUNTIME_PREVIEW=LOCKED", lines);
        Assert.Contains("TASK3B_RUNTIME_REGISTRATION_FLAG=OFF", lines);
        Assert.Contains("TASK3B_AUTH_CONFIGURATION=MISSING_OR_INVALID", lines);
        Assert.Contains("TASK3B_BACKUP_DECLARATION=MISSING_OR_INVALID", lines);
        Assert.Contains("TASK3B_POSTURE_CONFIGURATION=MISSING_OR_INVALID", lines);
        Assert.Contains("TASK3B_HOSTED_SIGNUP_RECEIPTS=NOT_VERIFIED", lines);
        Assert.DoesNotContain("PRIVATE", string.Join("\n", lines));
        Assert.DoesNotContain("synthetic failure", string.Join("\n", lines));
    }

    [Fact]
    public void Even_complete_configuration_never_claims_registered_customers_or_backup()
    {
        var auth = new BlissAuthenticationOptions
        {
            Enabled = true, SupabaseEnabled = true, AdvertiserRegistrationRequested = true,
            ClientId = "PRIVATE_CLIENT_ID", ClientSecret = "PRIVATE_SECRET_VALUE"
        };
        var lines = RegistrationReleaseReadinessMarkers.Build(
            Report(true), auth, new BlissRuntimeOptions { RestrictedPreview = false });
        Assert.Contains("TASK3B_RUNTIME_PREVIEW=UNLOCKED", lines);
        Assert.Contains("TASK3B_RUNTIME_REGISTRATION_FLAG=REQUESTED", lines);
        Assert.Contains("TASK3B_BACKUP_DECLARATION=CONFIGURED_UNVERIFIED", lines);
        Assert.Contains("TASK3B_POSTURE_CONFIGURATION=CONFIGURED_UNVERIFIED", lines);
        Assert.Contains("TASK3B_BACKUP_RESTORE_ATTESTATION=NOT_VERIFIED", lines);
        Assert.Contains("TASK3B_HOSTED_SIGNUP_RECEIPTS=NOT_VERIFIED", lines);
        Assert.Contains("TASK3B_ACCOUNT_NOTICES_EFFECTIVE=NOT_VERIFIED", lines);
        Assert.DoesNotContain("PRIVATE", string.Join("\n", lines));
    }
}
