#!/bin/sh
set -eu
umask 077
if [ "$(id -u)" = 0 ]; then
    install -d -m 0700 -o app -g app /data/keys /data/secrets
    exec gosu app /usr/local/bin/bliss-railway-entrypoint "$@"
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
