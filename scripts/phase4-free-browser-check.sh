#!/bin/bash
set -euo pipefail
cd /workspace/Bliss_Bot_Chapel
source /workspace/.bliss-tools/activate.sh
bash /workspace/.bliss-tools/start-db.sh
test_root=$(mktemp -d /workspace/.bliss-runtime/phase4-fixture-XXXXXX)
fixture_database="bliss_phase4_fixture_$(date +%s)_$$"
gateway_pid=""
api_pid=""
cleanup() {
  if [ -n "$api_pid" ]; then kill -TERM "$api_pid" 2>/dev/null || true; wait "$api_pid" 2>/dev/null || true; fi
  if [ -n "$gateway_pid" ]; then kill -TERM "$gateway_pid" 2>/dev/null || true; wait "$gateway_pid" 2>/dev/null || true; fi
  dropdb -h 127.0.0.1 -p 55432 -U bliss --if-exists "$fixture_database"
  rm -f "$test_root/ca.key" "$test_root/server.key" "$test_root/server.csr" "$test_root/ca.srl"
}
trap cleanup EXIT
createdb -h 127.0.0.1 -p 55432 -U bliss "$fixture_database"
export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=55432;Database=$fixture_database;Username=bliss"
export Authentication__Enabled=false ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5084
export Runtime__ControlledAi__Enabled=true Runtime__ControlledAi__Protocol=Gateway Runtime__ControlledAi__Provider=LOCAL_HTTPS_FIXTURE
export Runtime__ControlledAi__ApiToken=local-fixture-token Runtime__ControlledAi__TextModel=fixture-text Runtime__ControlledAi__ImageModel=fixture-image
export Runtime__ControlledAi__TextEndpoint=https://localhost:9444/worker Runtime__ControlledAi__ImageEndpoint=https://localhost:9444/worker
export Runtime__ControlledAi__MaxCallsPerDay=10 Runtime__ControlledAi__DailyBudgetUsd=1 Runtime__ControlledAi__TotalBudgetUsd=1
export Runtime__ControlledAi__TextReservationUsd=0.01 Runtime__ControlledAi__ImageReservationUsd=0.01 Runtime__ControlledAi__TimeoutSeconds=5
export NO_PROXY="${NO_PROXY:+$NO_PROXY,}localhost,127.0.0.1"
# Test-local CA: verification stays enabled and the leaf is signed for localhost.
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$test_root/ca.key" -out "$test_root/ca.crt" -days 1 -subj '/CN=Bliss local fixture CA' > "$test_root/tls.log" 2>&1
openssl req -newkey rsa:2048 -nodes -keyout "$test_root/server.key" -out "$test_root/server.csr" -subj '/CN=localhost' >> "$test_root/tls.log" 2>&1
printf '%s\n' 'subjectAltName=DNS:localhost,IP:127.0.0.1' 'extendedKeyUsage=serverAuth' 'basicConstraints=CA:FALSE' > "$test_root/server.ext"
openssl x509 -req -in "$test_root/server.csr" -CA "$test_root/ca.crt" -CAkey "$test_root/ca.key" -CAcreateserial -out "$test_root/server.crt" -days 1 -extfile "$test_root/server.ext" >> "$test_root/tls.log" 2>&1
export SSL_CERT_FILE="$test_root/ca.crt" BLISS_FIXTURE_TLS_KEY="$test_root/server.key" BLISS_FIXTURE_TLS_CERT="$test_root/server.crt"
node scripts/phase4-fixture-gateway.cjs > "$test_root/gateway.log" 2>&1 &
gateway_pid=$!
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
  with urllib.request.urlopen('http://127.0.0.1:5084/health/ready',timeout=1) as response:
   if response.status==200: break
 except Exception: pass
 time.sleep(.2)
else: raise RuntimeError('Fixture API did not become ready')
PY
NODE_PATH=/workspace/.bliss-tools/browser-tests/node_modules BLISS_SMOKE_BASE=http://127.0.0.1:5084 node scripts/phase4-ai-smoke.cjs
printf 'Fixture logs: %s\n' "$test_root"
