#!/usr/bin/env bash
# Read-only HTTP acceptance probe for an ALREADY approved isolated preview.
# Never execute mutating requests; fail before further probing unless the
# server explicitly identifies itself as non-operational restricted preview.
set -euo pipefail

if [[ $# -ne 1 || "${1}" != https://* ]]; then
  printf 'Usage: %s https://APPROVED-PREVIEW-HOST\n' "$0" >&2
  exit 2
fi

base="${1%/}"
case "$base" in
  *"?"*|*"#"*) printf 'Supply the plain HTTPS preview origin, no query/fragment.\n' >&2; exit 2 ;;
esac

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

fetch() {
  local path="$1"
  local expected="$2"
  local actual
  actual="$(curl --silent --show-error --max-time 20 --connect-timeout 8 \
    --max-redirs 0 --dump-header "$tmp/headers" --output "$tmp/body" \
    --write-out '%{http_code}' -- "$base$path")"
  if [[ "$actual" != "$expected" ]]; then
    printf 'FAIL: GET %s expected %s, received %s\n' "$path" "$expected" "$actual" >&2
    exit 1
  fi
  if ! grep -Eiq '^x-bliss-mode:[[:space:]]*restricted-preview' "$tmp/headers"; then
    printf 'FAIL: GET %s did not carry restricted-preview marker\n' "$path" >&2
    exit 1
  fi
  printf 'PASS: GET %s => %s\n' "$path" "$actual"
}

# The VERY FIRST request is the non-operational proof. Never proceed unless
# the response is an explicit restricted-preview denial of normal operations.
fetch '/preview-status' 200
python3 - "$tmp/body" <<'PY'
import json, sys
try:
    with open(sys.argv[1], encoding='utf-8') as f:
        status = json.load(f)
except (OSError, ValueError) as exc:
    raise SystemExit(f'FAIL: invalid preview status JSON: {exc}')
if status.get('restrictedPreview') is not True or status.get('operational') is not False:
    raise SystemExit('FAIL: service does not explicitly report restrictedPreview=true, operational=false')
print('PASS: restricted-preview mode and operational=false')
PY

for path in \
  '/' '/index.html' '/wedding-planner.css' \
  '/wedding-planner.js' '/preview-lock.js' \
  '/assets/bliss-chapel-hero.jpg' '/health/live'
do
  fetch "$path" 200
done

for path in \
  '/health/ready' '/health/posture' \
  '/api/auth/session' '/api/bliss/matches' '/signin-oidc' \
  '/operations' '/operations/index.html' \
  '/operations/generated/private.jpg' '/source-media/index.html' \
  '/ai-workspace.html' '/brand-intake.html' \
  '/production.html' '/oauth-consent.html' \
  '/acquisition/index.html' '/assets/unreviewed.jpg'
do
  fetch "$path" 503
done

printf 'PASS: restricted interface HTTP checks complete; full hosted readiness is NOT established.\n'
