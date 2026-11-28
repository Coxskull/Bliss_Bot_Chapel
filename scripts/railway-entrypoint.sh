#!/bin/sh
set -eu
umask 077
# This reconciled branch is a staging release, not a production Railway image.
# Local disposable CI has no Railway identity. On Railway, all three IDs must match.
if [ -n "${RAILWAY_PROJECT_ID:-}${RAILWAY_ENVIRONMENT_ID:-}${RAILWAY_SERVICE_ID:-}" ]; then
    if [ "${RAILWAY_PROJECT_ID:-}" != "43062727-bca5-400a-b801-27828161324b" ] \
        || [ "${RAILWAY_ENVIRONMENT_ID:-}" != "04edd217-035e-4c18-b864-9b7ded76de7d" ] \
        || [ "${RAILWAY_SERVICE_ID:-}" != "990e3d3e-b09a-4c43-8873-ab10e9bffd02" ] \
        || [ "${Runtime__RestrictedPreview:-}" != "true" ] \
        || [ "${Runtime__PublicAdvertiserRegistrationOnly:-false}" != "false" ] \
        || [ "${Authentication__Enabled:-}" != "false" ] \
        || [ "${Authentication__SupabaseEnabled:-}" != "false" ] \
        || [ "${Authentication__AdvertiserRegistrationRequested:-}" != "false" ] \
        || [ "${Authentication__AdvertiserPrivacyRequestsRequested:-}" != "false" ] \
        || [ "${Authentication__AdvertiserSelfServiceRequested:-}" != "false" ] \
        || [ "${Authentication__CreatorRegistrationRequested:-false}" != "false" ] \
        || [ "${Authentication__CreatorSelfServiceRequested:-false}" != "false" ] \
        || [ "${Authentication__AdvertiserMembershipSelfServiceRequested:-false}" != "false" ] \
        || [ "${Authentication__AskAlphaVisitorEnrollmentRequested:-false}" != "false" ]; then
        echo "BLISS_CREATIVE_STAGING_SCOPE_REFUSED" >&2
        exit 2
    fi
    case "${1:-}" in
        "") test "$#" -eq 0 || exit 2 ;;
        --fictional-roundtrip|--fictional-held-video) test "$#" -eq 2 || exit 2 ;;
        --fictional-held-admission) test "$#" -eq 1 || exit 2 ;;
        *) echo "BLISS_CREATIVE_STAGING_COMMAND_REFUSED" >&2; exit 2 ;;
    esac
fi
if [ "$(id -u)" = 0 ]; then
    install -d -m 0700 -o app -g app /data/keys /data/secrets
    exec setpriv --reuid=app --regid=app --init-groups --inh-caps=-all --ambient-caps=-all --no-new-privs /usr/local/bin/bliss-railway-entrypoint "$@"
fi
test "$(id -u)" -ne 0
# Explicit operator command only. Never submit a creative job on app startup.
if [ "${1:-}" = "--fictional-roundtrip" ]; then
    if [ "$#" -ne 2 ] \
        || [ "${Runtime__RestrictedPreview:-}" != "true" ] \
        || [ "${Authentication__Enabled:-}" != "false" ] \
        || [ "${Authentication__SupabaseEnabled:-}" != "false" ] \
        || [ "${Authentication__AdvertiserRegistrationRequested:-}" != "false" ] \
        || [ "${Authentication__AdvertiserPrivacyRequestsRequested:-}" != "false" ] \
        || [ "${Authentication__AdvertiserSelfServiceRequested:-}" != "false" ] \
        || [ "${BLISS_FICTIONAL_ROUNDTRIP:-}" != "YES" ] \
        || [ "${RAILWAY_PROJECT_ID:-}" != "43062727-bca5-400a-b801-27828161324b" ] \
        || [ "${RAILWAY_ENVIRONMENT_ID:-}" != "04edd217-035e-4c18-b864-9b7ded76de7d" ] \
        || [ "${RAILWAY_SERVICE_ID:-}" != "990e3d3e-b09a-4c43-8873-ab10e9bffd02" ]; then
        echo "BLISS_FICTIONAL_HOSTED_IDENTITY_REFUSED" >&2
        exit 2
    fi
    # The existing client additionally verifies HMAC, output custody and held receipts.
    exec timeout 500s node /app/ops/alpha-p0-fictional-creative-roundtrip.mjs --hosted "$2"
fi
# Preserve the deployed staging-only admission and video readback lanes.
# Explicit invocation only; the Node client independently validates staging scope.
case "${1:-}" in
    --fictional-held-admission)
        test "$#" -eq 1 || exit 2
        exec timeout 45s node /app/ops/alpha-p0-fictional-creative-roundtrip.mjs --held-admission
        ;;
    --fictional-held-video)
        test "$#" -eq 2 || exit 2
        exec timeout 100s node /app/ops/alpha-p0-fictional-creative-roundtrip.mjs --held-video "$2"
        ;;
esac
# A strictly locked, credential-free interface preview may use ephemeral local
# data-protection keys. This does NOT make OIDC or advertiser registration usable.
# Production and any preview with an enabled auth/account feature must retain
# the existing externally provided encrypted-at-rest certificate requirement.
if [ -z "${BLISS_DP_CERTIFICATE_BASE64:-}" ] \
    && [ "${Runtime__RestrictedPreview:-}" = "true" ] \
    && [ "${Authentication__Enabled:-}" = "false" ] \
    && [ "${Authentication__SupabaseEnabled:-}" = "false" ] \
    && [ "${Authentication__AdvertiserRegistrationRequested:-}" = "false" ] \
    && [ "${Authentication__AdvertiserPrivacyRequestsRequested:-}" = "false" ] \
    && [ "${Authentication__AdvertiserSelfServiceRequested:-}" = "false" ]; then
    if [ -n "${Runtime__DataProtectionCertificatePath:-}" ] \
        || [ -n "${Runtime__DataProtectionCertificatePassword:-}" ]; then
        echo "Restricted preview refuses incomplete certificate configuration." >&2
        exit 2
    fi
    # Never reuse keys from a real session/volume. This directory disappears
    # with the preview container; the middleware denies all auth/write routes.
    export Runtime__DataProtectionKeysPath=/tmp/bliss-restricted-preview-keys
    echo "Bliss restricted interface preview: login and writes disabled."
    exec dotnet /app/Bliss.Api.dll "$@"
fi
: "${BLISS_DP_CERTIFICATE_BASE64:?Data protection certificate secret is required}"
: "${Runtime__DataProtectionCertificatePassword:?Certificate password is required}"
printf '%s' "$BLISS_DP_CERTIFICATE_BASE64" | base64 -d > /data/secrets/session.pfx.tmp
openssl pkcs12 -in /data/secrets/session.pfx.tmp -passin env:Runtime__DataProtectionCertificatePassword -noout >/dev/null 2>&1
chmod 0600 /data/secrets/session.pfx.tmp
mv /data/secrets/session.pfx.tmp /data/secrets/session.pfx
if [ -n "${BLISS_DATABASE_CA_PEM:-}" ]; then
    printf '%s\n' "$BLISS_DATABASE_CA_PEM" > /data/secrets/database-ca.pem
    openssl x509 -in /data/secrets/database-ca.pem -noout >/dev/null
    chmod 0600 /data/secrets/database-ca.pem
fi
test -w /data/keys
unset BLISS_DP_CERTIFICATE_BASE64
echo "Bliss persistent session storage prepared; launching as UID $(id -u)."
exec dotnet /app/Bliss.Api.dll "$@"
