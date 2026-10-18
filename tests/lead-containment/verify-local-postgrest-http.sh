#!/usr/bin/env bash
# Strict CI isolation. Must NEVER accept a remote Supabase endpoint or secret.
set -euo pipefail
: "${PGHOST:?}" "${PGPORT:?}" "${PGDATABASE:?}" "${PGUSER:?}" "${PGPASSWORD:?}"
if [[ "${PGHOST}" != "127.0.0.1" || "${PGPORT}" != "5432" ||
      "${PGDATABASE}" != "postgres" || "${PGUSER}" != "postgres" ]]; then
  echo "REFUSED: only a disposable loopback PostgreSQL runner is allowed" >&2
  exit 1
fi
trap 'docker rm -f alpha-ci-postgrest >/dev/null 2>&1 || true' EXIT

psql -X -v ON_ERROR_STOP=1 -f tests/lead-containment/postgrest-http-fixture.sql >/dev/null

# Host networking is available on Ubuntu Linux GitHub-hosted runners.
# No production URI, cookies, JWT or Supabase publishable key is supplied.
docker run -d --rm --name alpha-ci-postgrest --network host \
  -e PGRST_DB_URI='postgres://authenticator:ci-postgrest-only-ephemeral@127.0.0.1:5432/postgres' \
  -e PGRST_DB_SCHEMAS=public \
  -e PGRST_DB_ANON_ROLE=anon \
  -e PGRST_SERVER_PORT=3000 \
  postgrest/postgrest:v12.2.3 >/dev/null

for i in $(seq 1 30); do
  if curl --max-time 2 -fsS 'http://127.0.0.1:3000/' >/dev/null 2>&1; then break; fi
  sleep 1
done

node tests/lead-containment/verify-local-postgrest-http.mjs
echo 'PASS: disposable PostgreSQL + real PostgREST HTTP denial acceptance'
echo 'NOT TESTED: Supabase gateway/publishable key, production REST, real integrations or any customer data'
