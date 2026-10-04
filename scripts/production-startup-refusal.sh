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
  unset ConnectionStrings__DefaultConnection || true
  unset Authentication__ClientSecret || true
  unset Runtime__DataProtectionCertificatePassword || true
  dotnet run --project Bliss.Api/Bliss.Api.csproj --no-launch-profile
) >"$log" 2>&1
code=$?
set -e

if grep -q 'Password=' "$log"; then
  rm -f "$log"
  echo "Production refusal discarded a log that contained a password assignment." >&2
  exit 1
fi

{
  echo "Production startup exit: ${code}"
  if grep -q "Production posture is incomplete" "$log"; then
    echo "Production posture is incomplete."
  else
    echo "Production posture message was not found."
  fi
  echo "Delivery: NOT_SENT"
  echo "Hosted acceptance claimed: no"
} > "$evidence"

if [[ "$code" -eq 0 ]] || ! grep -q "Production posture is incomplete" "$log"; then
  cat "$log" >&2
  rm -f "$log"
  echo "Production startup was expected to fail closed." >&2
  exit 1
fi

rm -f "$log"
cat "$evidence"
