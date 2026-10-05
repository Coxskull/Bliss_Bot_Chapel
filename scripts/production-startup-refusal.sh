#!/usr/bin/env bash
# Start Production with no secrets and record the fail-closed result.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
evidence_dir="${repo}/docs/architecture/evidence/dock"
mkdir -p "$evidence_dir"
evidence="${evidence_dir}/production-startup-refusal.txt"
log="$(mktemp)"

set +e
(
  cd "$repo"
  export ASPNETCORE_ENVIRONMENT=Production
  export Authentication__Enabled=true
  export Authentication__Authority=https://identity.example.test/realms/bliss
  export Authentication__ClientId=bliss-chapel
  export Runtime__DataProtectionKeysPath=/tmp/bliss-refusal-keys
  unset ConnectionStrings__DefaultConnection || true
  unset Authentication__ClientSecret || true
  unset Runtime__DataProtectionCertificatePassword || true
  mkdir -p /tmp/bliss-refusal-keys
  dotnet run --project Bliss.Api/Bliss.Api.csproj --no-launch-profile
) >"$log" 2>&1
code=$?
set -e

if grep -q 'Password=' "$log"; then
  rm -f "$log"
  echo "Production refusal discarded a log that contained a password assignment." >&2
  exit 1
fi

posture_line="$(grep -m1 'Production posture is incomplete' "$log" | sed 's/^.*Production posture is incomplete/Production posture is incomplete/' || true)"
{
  echo "Production startup exit: ${code}"
  if [[ -n "$posture_line" ]]; then
    printf '%s\n' "$posture_line"
  else
    echo "Production posture message was not found."
  fi
  echo "Delivery: NOT_SENT"
  echo "Hosted acceptance claimed: no"
} > "$evidence"
if grep -q 'Password=' "$evidence"; then
  rm -f "$log" "$evidence"
  echo "Production refusal discarded evidence that contained a password assignment." >&2
  exit 1
fi

if [[ "$code" -eq 0 ]] || ! grep -q "Production posture is incomplete" "$log"; then
  cat "$log" >&2
  rm -f "$log"
  echo "Production startup was expected to fail closed." >&2
  exit 1
fi

rm -f "$log"
cat "$evidence"
