#!/usr/bin/env bash
# CI-only restart. Keeps the same disposable PostgreSQL database while clearing
# ASP.NET's in-memory per-IP rate-limit window between independent smoke flows.
# Do not use for a hosted deployment or with a real database.
set -euo pipefail

stage="${1:-}"
if [[ "$stage" != phase2 && "$stage" != phase3 ]]; then
  echo "Refusing restart outside the approved fictional browser stages." >&2
  exit 2
fi
if [[ "${ASPNETCORE_ENVIRONMENT:-}" != Development ||
      "${ASPNETCORE_URLS:-}" != 'http://127.0.0.1:5086' ||
      "${Authentication__Enabled:-}" != false ||
      "${Runtime__ControlledAi__Enabled:-}" != false ||
      "${FinancialPreparation__Enabled:-}" != false ||
      "${ConnectionStrings__DefaultConnection:-}" != 'Host=127.0.0.1;Port=5432;Database=bliss_browser_ci;Username=postgres;Password=postgres' ]]; then
  echo "Refusing API restart: not the explicitly declared disposable loopback fixture." >&2
  exit 2
fi
if [[ ! -f BrowserEvidence/fixture-api.pid ]]; then
  echo "Missing fixture API PID; not starting an additional application." >&2
  exit 2
fi

old_pid="$(cat BrowserEvidence/fixture-api.pid)"
if [[ ! "$old_pid" =~ ^[0-9]+$ ]]; then
  echo "Refusing unsafe fixture PID." >&2
  exit 2
fi
kill -TERM "$old_pid"
for attempt in {1..40}; do
  if ! kill -0 "$old_pid" 2>/dev/null; then break; fi
  sleep .25
done
if kill -0 "$old_pid" 2>/dev/null; then
  echo "Fixture API failed to stop; refusing port overlap." >&2
  exit 1
fi

(cd Bliss.Api && exec dotnet bin/Release/net8.0/Bliss.Api.dll) > "BrowserEvidence/api-after-${stage}.log" 2>&1 &
echo "$!" > BrowserEvidence/fixture-api.pid
python3 - <<'PY'
import time
from urllib.request import urlopen
for attempt in range(100):
    try:
        with urlopen("http://127.0.0.1:5086/health/ready", timeout=1) as response:
            if response.status == 200:
                print("Disposable fixture API restarted; persisted database preserved")
                break
    except Exception:
        time.sleep(.3)
else:
    raise SystemExit("FAIL: restarted local API did not become ready")
PY
