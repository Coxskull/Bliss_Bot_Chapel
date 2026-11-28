using Bliss.Api.Security;

namespace Bliss.Api.Runtime;

/// <summary>
/// One startup-only, fixed-vocabulary registration readiness readback.
/// Logs neither connection strings nor OAuth/client/private certificate values.
/// A configured declaration is NOT proof of a backup, authenticated session,
/// migrated schema, accepted account notice, or usable registration.
/// </summary>
public static class RegistrationReleaseReadinessMarkers
{
    private static string Status(bool value, string success = "CONFIGURED_UNVERIFIED") =>
        value ? success : "MISSING_OR_INVALID";

    public static IReadOnlyList<string> Build(
        ProductionPostureReport posture,
        BlissAuthenticationOptions authentication,
        BlissRuntimeOptions runtime)
    {
        ArgumentNullException.ThrowIfNull(posture);
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(runtime);

        var document = posture.Document;
        return
        [
            "TASK3B_RUNTIME_PREVIEW=" + (runtime.RestrictedPreview ? "LOCKED" : "UNLOCKED"),
            "TASK3B_RUNTIME_REGISTRATION_FLAG=" + (authentication.AdvertiserRegistrationRequested ? "REQUESTED" : "OFF"),
            "TASK3B_AUTH_CONFIGURATION=" + Status(
                authentication.Enabled && authentication.SupabaseEnabled
                && document.IdentityProviderHttps
                && !string.IsNullOrWhiteSpace(authentication.ClientId)
                && !string.IsNullOrWhiteSpace(authentication.ClientSecret)),
            "TASK3B_DB_VERIFYFULL_CONFIGURATION=" + Status(
                document.HostedDatabaseConfigured && document.DatabaseServerCertificateVerified),
            "TASK3B_KEYS_ENCRYPTED_CONFIGURATION=" + Status(document.KeysEncryptedAtRest),
            "TASK3B_TRUSTED_INGRESS_CONFIGURATION=" + Status(
                document.KnownProxiesConfigured || document.LoopbackIngressConfigured),
            "TASK3B_BACKUP_DECLARATION=" + Status(document.BackupDeclared),
            "TASK3B_POSTURE_CONFIGURATION=" + Status(posture.Failures.Count == 0),
            // Exact, permanent disclaimers: these require independent provider,
            // hosted sign-up, privacy/support and owner acceptance evidence.
            "TASK3B_BACKUP_RESTORE_ATTESTATION=NOT_VERIFIED",
            "TASK3B_HOSTED_SIGNUP_RECEIPTS=NOT_VERIFIED",
            "TASK3B_ACCOUNT_NOTICES_EFFECTIVE=NOT_VERIFIED"
        ];
    }
}
