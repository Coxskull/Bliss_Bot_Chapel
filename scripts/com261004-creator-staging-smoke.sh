#!/usr/bin/env bash
set -Eeuo pipefail
# Fail closed outside the exact non-production GitHub Actions fixture.
expected='postgresql://postgres:postgres@127.0.0.1:5432/agm_creator_ci?sslmode=disable'
[[ "${CI:-}" == "true" && "${GITHUB_ACTIONS:-}" == "true" &&
   "${CREATOR_CI_DATABASE_URL:-}" == "$expected" ]] || {
  echo "BLOCKED: disposable GitHub Actions database only" >&2
  exit 2
}
command -v psql >/dev/null
psql "$expected" -v ON_ERROR_STOP=1 -X -q \
  -f Bliss.Infrastructure/Migrations/CreatorRegistrationStagingFixtureV1.sql \
  -f scripts/com261004-creator-staging-smoke.sql >/dev/null
echo "Creator intake fixture: consent, ACL, persistence and idempotent receipt tests PASS"

# Eight genuinely separate PostgreSQL connections race on the same identity.
# No real contact information is accepted; no provider account is called.
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT
declare -a children=()
for i in $(seq 1 8); do
  psql "$expected" -v ON_ERROR_STOP=1 -AtX -q -c \
    "SELECT is_replay FROM agm_creator_fixture.register_synthetic(
      'testkey000001','Synthetic Creator One','creator@example.invalid',
      'https://example.invalid/channel/podcast','Test City','podcast',true,false)" \
      >"$tmp/$i.out" 2>"$tmp/$i.err" &
  children+=("$!")
done
for id in "${children[@]}"; do wait "$id" || {
  echo "Concurrent retry failed" >&2; exit 1;
}; done
for i in $(seq 1 8); do
  [[ "$(cat "$tmp/$i.out")" == t ]] || {
    echo "Concurrent retry did not reuse the canonical record" >&2; exit 1;
  }
done
readback="$(psql "$expected" -v ON_ERROR_STOP=1 -AtX -q -c "
SELECT count(*)::text || ':' || (SELECT count(*) FROM agm_creator_fixture.receipts)::text
FROM agm_creator_fixture.intakes")"
[[ "$readback" == "1:1" ]] || { echo "Readback mismatch" >&2; exit 1; }
echo "Creator intake fixture: 8 concurrent canonical retries, 1/1 independent readback PASS"
