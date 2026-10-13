#!/bin/bash
# Creates only fictional local databases; never backs up or drops a user database.
set -euo pipefail
umask 077
cd /workspace/Bliss_Bot_Chapel
source /workspace/.bliss-tools/activate.sh
case "${PGHOST:-127.0.0.1}" in localhost|127.0.0.1|::1) ;; *) echo 'Recovery drill requires a loopback host.' >&2; exit 2;; esac
bash /workspace/.bliss-tools/start-db.sh
test_root=$(mktemp -d /workspace/.bliss-runtime/phase6-fixture-XXXXXX)
source_database="bliss_phase6_source_$(date +%s)_$$"
restore_database="bliss_phase6_restore_$(date +%s)_$$"
api_pid=""
source_created=false
restore_created=false
stop_api() {
  if [ -n "$api_pid" ]; then kill -TERM "$api_pid" 2>/dev/null || true; wait "$api_pid" 2>/dev/null || true; api_pid=""; fi
}
cleanup() {
  stop_api
  if $source_created; then dropdb -h 127.0.0.1 -p 55432 -U bliss --if-exists "$source_database"; fi
  if $restore_created; then dropdb -h 127.0.0.1 -p 55432 -U bliss --if-exists "$restore_database"; fi
  rm -f "$test_root/application.dump"
}
trap cleanup EXIT
createdb -h 127.0.0.1 -p 55432 -U bliss "$source_database"
source_created=true
export Authentication__Enabled=false ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5088
export Runtime__ControlledAi__Enabled=false
export NODE_PATH=/workspace/.bliss-tools/browser-tests/node_modules BLISS_SMOKE_BASE=http://127.0.0.1:5088
wait_api() {
  python3 - <<'PY'
import time,urllib.request
for attempt in range(150):
 try:
  with urllib.request.urlopen('http://127.0.0.1:5088/health/ready',timeout=1) as response:
   if response.status==200: break
 except Exception: pass
 time.sleep(.2)
else: raise RuntimeError('Recovery fixture API did not become ready')
PY
}
start_api() {
  export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=55432;Database=$1;Username=bliss"
  (
    cd Bliss.Api
    dotnet bin/Release/net8.0/Bliss.Api.dll --migrate > "$test_root/$2-migrate.log" 2>&1
    exec dotnet bin/Release/net8.0/Bliss.Api.dll > "$test_root/$2-api.log" 2>&1
  ) &
  api_pid=$!
  wait_api
}
start_api "$source_database" source
# Test-only overlay price fixture, isolated from the local development database.
psql -h 127.0.0.1 -p 55432 -U bliss -d "$source_database" -v ON_ERROR_STOP=1 <<'SQL'
INSERT INTO "InventoryRateBenchmarks"
 ("Id", "GeographicMarketId", "PricingModelId", "ResearchSourceId", "InventorySlotType",
  "DurationSecondsLow", "DurationSecondsHigh", "RangeLow", "RangeHigh", "CurrencyCode",
  "ConfidenceLevel", "VerificationStatus", "EffectiveAt", "CreatedAt")
SELECT gen_random_uuid(), "GeographicMarketId", "PricingModelId", "ResearchSourceId", 'PERIMETER_OVERLAY',
  1, 300, "RangeLow", "RangeHigh", "CurrencyCode", 'TEST', 'TEST_ONLY',
  CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_TIMESTAMP
FROM "InventoryRateBenchmarks"
WHERE "GeographicMarketId" = 'ec000001-0000-0000-0000-000000000001' AND "InventorySlotType" = 'MID_ROLL'
LIMIT 1;
SQL
node scripts/phase5-handoff-smoke.cjs
node scripts/phase6-measurement-smoke.cjs
stop_api
manifest() {
  for table in WeddingPlannerBrandDna AcademyProductionBriefs AcademyProductionCandidates AcademyProductionReviews CandidateApprovalDecisions CreativeCampaignHandoffs CampaignPlacements CampaignPlacementRuns Quotes QuoteVersions QuoteLineItems CampaignObservations CampaignLearningProposals CampaignLearningReviews WeddingPlannerAuditEvents; do
    psql -h 127.0.0.1 -p 55432 -U bliss -d "$1" -v ON_ERROR_STOP=1 -At -c "SELECT '$table', count(*), md5(COALESCE(jsonb_agg(to_jsonb(t) ORDER BY t.\"Id\")::text,'[]')) FROM \"$table\" t;"
  done
}
manifest "$source_database" > "$test_root/source-manifest.txt"
pg_dump -h 127.0.0.1 -p 55432 -U bliss -Fc -d "$source_database" -f "$test_root/application.dump"
recovery_start=$(date +%s)
createdb -h 127.0.0.1 -p 55432 -U bliss "$restore_database"
restore_created=true
pg_restore -h 127.0.0.1 -p 55432 -U bliss --no-owner --exit-on-error -d "$restore_database" "$test_root/application.dump"
manifest "$restore_database" > "$test_root/restored-manifest.txt"
diff -u "$test_root/source-manifest.txt" "$test_root/restored-manifest.txt"
start_api "$restore_database" restored
BLISS_VERIFY_RESTORE=1 node scripts/phase6-measurement-smoke.cjs
recovery_seconds=$(( $(date +%s) - recovery_start ))
printf 'PASS: 15 lineage/history table manifests match after restore. Local fixture recovery: %s seconds. Hosted RPO/RTO remain unvalidated.\n' "$recovery_seconds"
printf 'Fixture logs: %s\n' "$test_root"
