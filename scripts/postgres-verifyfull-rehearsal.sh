#!/usr/bin/env bash
# Local VerifyFull rehearsal. Loopback only. This is not a hosted database.
# The script refuses to print the database password.
set -euo pipefail

if [[ "${BLISS_VERIFYFULL_REHEARSAL:-}" != "1" || "${BLISS_VERIFYFULL_REHEARSAL_TARGET:-}" != "local" ]]; then
  echo "VerifyFull rehearsal refused: set BLISS_VERIFYFULL_REHEARSAL=1 and BLISS_VERIFYFULL_REHEARSAL_TARGET=local." >&2
  exit 2
fi

for tool in openssl psql pg_dump pg_restore createdb dropdb dotnet python3; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "VerifyFull rehearsal refused: ${tool} is not installed." >&2
    exit 2
  fi
done

set +x
unset PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD PGSSLMODE PGSSLROOTCERT || true

repo="$(cd "$(dirname "$0")/.." && pwd)"
evidence_dir="${repo}/docs/architecture/evidence/dock"
mkdir -p "$evidence_dir"
evidence="${evidence_dir}/verifyfull-rehearsal.txt"
: > "$evidence"

password="$(openssl rand -base64 32 | tr -d '\n=+/' | cut -c1-24)"
if [[ "${#password}" -lt 16 ]]; then
  echo "VerifyFull rehearsal refused: generated password was too short." >&2
  exit 1
fi

note() {
  local line="$1"
  if [[ "$line" == *"$password"* ]]; then
    echo "VerifyFull rehearsal refused to record a line that contained the password." >&2
    exit 1
  fi
  printf '%s\n' "$line" | tee -a "$evidence"
}

scrub() {
  local file="$1"
  if [[ -f "$file" ]] && grep -q "$password" "$file"; then
    rm -f "$file"
    echo "VerifyFull rehearsal discarded a log that contained the password." >&2
    exit 1
  fi
}

note "VerifyFull rehearsal. Target local. Loopback only."
note "Hosted acceptance claimed: no"
note "Identity provider contacted: no"
note "Delivery: NOT_SENT"

if ! grep -q 'bliss-verifyfull-rehearsal' /etc/hosts; then
  echo '127.0.0.1 bliss-rehearsal.local wrong-host.bliss-rehearsal.local # bliss-verifyfull-rehearsal' | sudo tee -a /etc/hosts >/dev/null
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"; unset PGPASSWORD || true' EXIT

openssl req -new -x509 -days 825 -nodes \
  -keyout "$work/ca.key" -out "$work/ca.crt" \
  -subj "/CN=Bliss Rehearsal CA" >/dev/null 2>&1
openssl req -new -nodes \
  -keyout "$work/server.key" -out "$work/server.csr" \
  -subj "/CN=bliss-rehearsal.local" >/dev/null 2>&1
cat > "$work/server.ext" <<'EOF'
basicConstraints=CA:FALSE
keyUsage=digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:bliss-rehearsal.local,IP:127.0.0.1
EOF
openssl x509 -req -in "$work/server.csr" -CA "$work/ca.crt" -CAkey "$work/ca.key" \
  -CAcreateserial -out "$work/server.crt" -days 825 -extfile "$work/server.ext" >/dev/null 2>&1
openssl req -new -x509 -days 30 -nodes \
  -keyout "$work/wrong.key" -out "$work/wrong.crt" \
  -subj "/CN=Bliss Wrong CA" >/dev/null 2>&1

sudo install -d -o ubuntu -g ubuntu -m 755 /var/lib/bliss-rehearsal
sudo install -d -o ubuntu -g ubuntu -m 755 /var/lib/bliss-rehearsal/certs
sudo chown ubuntu:ubuntu /var/lib/bliss-rehearsal
sudo install -m 644 "$work/ca.crt" /var/lib/bliss-rehearsal/certs/ca.crt
sudo install -m 644 "$work/server.crt" /var/lib/bliss-rehearsal/certs/server.crt
sudo install -m 644 "$work/wrong.crt" /var/lib/bliss-rehearsal/certs/wrong-ca.crt
sudo install -o postgres -g postgres -m 600 "$work/server.key" /var/lib/bliss-rehearsal/certs/server.key
rm -f "$work/ca.key" "$work/server.key" "$work/wrong.key"

conf="$(sudo -u postgres psql -tA -c 'SHOW config_file')"
hba="$(sudo -u postgres psql -tA -c 'SHOW hba_file')"
conf_dir="$(dirname "$conf")"
sudo tee "$conf_dir/conf.d/bliss-rehearsal.conf" >/dev/null <<'EOF'
ssl = on
ssl_cert_file = '/var/lib/bliss-rehearsal/certs/server.crt'
ssl_key_file = '/var/lib/bliss-rehearsal/certs/server.key'
ssl_ca_file = '/var/lib/bliss-rehearsal/certs/ca.crt'
EOF

hba_line="hostssl bliss_rehearsal bliss_rehearsal 127.0.0.1/32 scram-sha-256"
hba_line6="hostssl bliss_rehearsal bliss_rehearsal ::1/128 scram-sha-256"
if ! sudo grep -qxF "$hba_line" "$hba"; then
  echo "$hba_line" | sudo tee -a "$hba" >/dev/null
fi
if ! sudo grep -qxF "$hba_line6" "$hba"; then
  echo "$hba_line6" | sudo tee -a "$hba" >/dev/null
fi

if command -v pg_lsclusters >/dev/null 2>&1; then
  version="$(pg_lsclusters --no-header | awk 'NR==1 { print $1 }')"
  name="$(pg_lsclusters --no-header | awk 'NR==1 { print $2 }')"
  sudo pg_ctlcluster "$version" "$name" restart
else
  sudo systemctl restart postgresql
fi

sql="$(mktemp)"
chmod 600 "$sql"
cat > "$sql" <<EOF
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'bliss_rehearsal') THEN
    CREATE ROLE bliss_rehearsal LOGIN PASSWORD '${password}';
  ELSE
    ALTER ROLE bliss_rehearsal PASSWORD '${password}';
  END IF;
END
\$\$;
EOF
sudo -u postgres psql -v ON_ERROR_STOP=1 -f - < "$sql" >/dev/null
rm -f "$sql"
sudo -u postgres psql -v ON_ERROR_STOP=1 -c "ALTER ROLE bliss_rehearsal CREATEDB;" >/dev/null
sudo -u postgres psql -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'bliss_rehearsal' AND pid <> pg_backend_pid();" >/dev/null || true
sudo -u postgres dropdb --if-exists bliss_rehearsal
sudo -u postgres createdb -O bliss_rehearsal bliss_rehearsal

export PGPASSWORD="$password"
export PGSSLMODE=verify-full
export PGSSLROOTCERT=/var/lib/bliss-rehearsal/certs/ca.crt

matched="$(psql -h bliss-rehearsal.local -p 5432 -U bliss_rehearsal -d bliss_rehearsal -v ON_ERROR_STOP=1 -tA -c 'SELECT 1')"
if [[ "$matched" != "1" ]]; then
  note "verify-full matching name: failed"
  exit 1
fi
note "verify-full matching name: success"

loopback="$(psql -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal -v ON_ERROR_STOP=1 -tA -c 'SELECT 1')"
if [[ "$loopback" != "1" ]]; then
  note "verify-full loopback address: failed"
  exit 1
fi
note "verify-full loopback address with the IP name in the certificate: success"

set +e
PGSSLROOTCERT=/var/lib/bliss-rehearsal/certs/wrong-ca.crt \
  psql -h bliss-rehearsal.local -p 5432 -U bliss_rehearsal -d bliss_rehearsal -tA -c 'SELECT 1' \
  >/tmp/bliss-wrong-ca.out 2>/tmp/bliss-wrong-ca.err
wrong_ca=$?
PGSSLROOTCERT=/var/lib/bliss-rehearsal/certs/ca.crt \
  psql -h wrong-host.bliss-rehearsal.local -p 5432 -U bliss_rehearsal -d bliss_rehearsal -tA -c 'SELECT 1' \
  >/tmp/bliss-wrong-host.out 2>/tmp/bliss-wrong-host.err
wrong_host=$?
set -e
export PGSSLROOTCERT=/var/lib/bliss-rehearsal/certs/ca.crt
scrub /tmp/bliss-wrong-ca.out
scrub /tmp/bliss-wrong-ca.err
scrub /tmp/bliss-wrong-host.out
scrub /tmp/bliss-wrong-host.err
rm -f /tmp/bliss-wrong-ca.out /tmp/bliss-wrong-ca.err /tmp/bliss-wrong-host.out /tmp/bliss-wrong-host.err
if [[ "$wrong_ca" -eq 0 ]]; then
  note "verify-full wrong certificate authority: unexpectedly succeeded"
  exit 1
fi
note "verify-full wrong certificate authority: refused"
if [[ "$wrong_host" -eq 0 ]]; then
  note "verify-full wrong hostname: unexpectedly succeeded"
  exit 1
fi
note "verify-full wrong hostname: refused"

connection_file=/var/lib/bliss-rehearsal/connection.string
umask 077
cat > "$connection_file" <<EOF
Host=127.0.0.1;Port=5432;Database=bliss_rehearsal;Username=bliss_rehearsal;Password=${password};SSL Mode=VerifyFull;Root Certificate=/var/lib/bliss-rehearsal/certs/ca.crt
EOF
chmod 600 "$connection_file"
note "connection file mode: $(stat -c '%a' "$connection_file")"
note "connection file records the password: no"

log="$(mktemp)"
set +e
(
  export ASPNETCORE_ENVIRONMENT=Development
  export ConnectionStrings__DefaultConnection
  ConnectionStrings__DefaultConnection="$(cat "$connection_file")"
  dotnet run --project "$repo/Bliss.Api/Bliss.Api.csproj" --no-launch-profile -- --migrate
) >"$log" 2>&1
migrate_code=$?
set -e
scrub "$log"
if [[ "$migrate_code" -ne 0 ]] || ! grep -q "Migrations applied. Hosted acceptance was not claimed." "$log"; then
  echo "Migration command failed." >&2
  cat "$log" >&2
  rm -f "$log"
  exit 1
fi
rm -f "$log"
note "migrations applied through the application command"

history_count="$(psql -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal -v ON_ERROR_STOP=1 -tA -c 'SELECT COUNT(*) FROM "__EFMigrationsHistory"')"
note "migration history rows: ${history_count}"
if [[ "$history_count" == "0" ]]; then
  note "migration history was empty"
  exit 1
fi

marker="marker-$(date -u +%Y%m%dT%H%M%SZ)-$$"
psql -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal -v ON_ERROR_STOP=1 \
  -c "CREATE TABLE dock_rehearsal_marker (marker text PRIMARY KEY);" >/dev/null
psql -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal -v ON_ERROR_STOP=1 \
  -c "INSERT INTO dock_rehearsal_marker (marker) VALUES ('${marker}');" >/dev/null

dump=/var/lib/bliss-rehearsal/rehearsal.dump
pg_dump -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal -Fc -f "$dump"
chmod 600 "$dump"
note "backup dump bytes: $(stat -c '%s' "$dump")"
note "backup transport: verify-full"

sudo -u postgres psql -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'bliss_rehearsal' AND pid <> pg_backend_pid();" >/dev/null || true
sudo -u postgres dropdb bliss_rehearsal
sudo -u postgres createdb -O bliss_rehearsal bliss_rehearsal
pg_restore --no-owner --exit-on-error -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal "$dump"
found="$(psql -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal -v ON_ERROR_STOP=1 -tA -c 'SELECT marker FROM dock_rehearsal_marker;')"
restored_count="$(psql -h 127.0.0.1 -p 5432 -U bliss_rehearsal -d bliss_rehearsal -v ON_ERROR_STOP=1 -tA -c 'SELECT COUNT(*) FROM "__EFMigrationsHistory"')"
if [[ "$found" != "$marker" ]]; then
  note "restore marker: mismatch"
  exit 1
fi
note "restore marker: match"
note "restored migration history rows: ${restored_count}"
if [[ "$restored_count" != "$history_count" ]]; then
  note "restore migration history: mismatch"
  exit 1
fi

unset PGPASSWORD
note "rehearsal database host string for the application: 127.0.0.1"
note "hosted database: no"
note "server certificate verification performed: yes"
note "hosted acceptance claimed: no"
note "identity provider contacted: no"
note "delivery: NOT_SENT"
note "VerifyFull rehearsal completed."
