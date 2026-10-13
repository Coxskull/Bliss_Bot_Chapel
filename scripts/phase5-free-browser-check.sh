#!/bin/bash
set -euo pipefail
cd /workspace/Bliss_Bot_Chapel
source /workspace/.bliss-tools/activate.sh
bash /workspace/.bliss-tools/start-db.sh
test_root=$(mktemp -d /workspace/.bliss-runtime/phase5-fixture-XXXXXX)
fixture_database="bliss_phase5_fixture_$(date +%s)_$$"
api_pid=""
cleanup() {
  if [ -n "$api_pid" ]; then kill -TERM "$api_pid" 2>/dev/null || true; wait "$api_pid" 2>/dev/null || true; fi
  dropdb -h 127.0.0.1 -p 55432 -U bliss --if-exists "$fixture_database"
}
trap cleanup EXIT
createdb -h 127.0.0.1 -p 55432 -U bliss "$fixture_database"
export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=55432;Database=$fixture_database;Username=bliss"
export Authentication__Enabled=false ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5086
export Runtime__ControlledAi__Enabled=false
(
  cd Bliss.Api
  dotnet bin/Release/net8.0/Bliss.Api.dll --migrate > "$test_root/migrate.log" 2>&1
  exec dotnet bin/Release/net8.0/Bliss.Api.dll > "$test_root/api.log" 2>&1
) &
api_pid=$!
python3 - <<'PY'
import time,urllib.request
for attempt in range(100):
 try:
  with urllib.request.urlopen('http://127.0.0.1:5086/health/ready',timeout=1) as response:
   if response.status==200: break
 except Exception: pass
 time.sleep(.2)
else: raise RuntimeError('Fixture API did not become ready')
PY
# Only in the disposable fixture database: an explicitly fictional overlay
# benchmark to exercise Economics. Production fixtures have no overlay anchor.
psql -h 127.0.0.1 -p 55432 -U bliss -d "$fixture_database" -v ON_ERROR_STOP=1 <<'SQL'
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
NODE_PATH=/workspace/.bliss-tools/browser-tests/node_modules BLISS_SMOKE_BASE=http://127.0.0.1:5086 node scripts/phase5-handoff-smoke.cjs
printf 'Fixture logs: %s\n' "$test_root"
