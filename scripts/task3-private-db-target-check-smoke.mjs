import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";

const script = readFileSync("scripts/task3-private-db-target-check.ps1", "utf8");
const match = script.match(/\$probe\s*=\s*@'\r?\n([\s\S]*?)\r?\n'@/);
assert.ok(match, "embedded Perl probe must remain present");
const probe = match[1];

function classify(connection, expected, label) {
  const env = { ...process.env };
  if (connection === null) delete env.ConnectionStrings__DefaultConnection;
  else env.ConnectionStrings__DefaultConnection = connection;
  const result = spawnSync("perl", ["-"], { input: probe, encoding: "utf8", env });
  assert.equal(result.error, undefined, `${label}: Perl must be available`);
  const output = result.stdout ?? "";
  assert.ok(!output.includes("never-print-this"), `${label}: password leaked`);
  assert.ok(!output.includes("evil.internal"), `${label}: injected host leaked`);
  if (expected.ok) {
    assert.equal(result.status, 0, `${label}: expected classification success; stderr=${result.stderr}`);
    for (const line of expected.lines) assert.ok(output.includes(line), `${label}: missing ${line}`);
  } else {
    assert.notEqual(result.status, 0, `${label}: expected fail-closed status`);
    assert.ok(output.includes("TASK3_DB_IDENTITY=INVALID_OR_UNSUPPORTED"), `${label}: missing refusal marker`);
    assert.ok(!output.includes("TASK3_HOST_CLASS="), `${label}: emitted a partial host classification`);
  }
}

classify(
  'Host=app.railway.internal;Database=bliss;Username=test;Password="never-print-this; Host=evil.internal"',
  { ok: true, lines: ["TASK3_DB_IDENTITY=CLASSIFIED", "TASK3_HOST_CLASS=RAILWAY_INTERNAL", "TASK3_DATABASE_CLASS=BLISS", "TASK3_NO_DATABASE_WRITES=PASS", "TASK3_SCHEMA_AND_BACKUP_ACCEPTANCE=NOT_ESTABLISHED"] },
  "quoted delimiter and Railway internal target"
);
classify(
  "Host=db.bkutbglzivfdnoerigyb.supabase.co;Database=postgres;Username=test;Password=never-print-this",
  { ok: true, lines: ["TASK3_HOST_CLASS=BLISS_SUPABASE_DIRECT", "TASK3_DATABASE_CLASS=POSTGRES"] },
  "known Bliss Supabase target"
);
classify(
  "Host=app.proxy.rlwy.net;Database=bliss;Username=test;Password=never-print-this",
  { ok: true, lines: ["TASK3_HOST_CLASS=RAILWAY_PUBLIC_PROXY", "TASK3_DATABASE_CLASS=BLISS"] },
  "Railway public proxy target"
);
classify(
  "Host=other.example.invalid;Database=other;Username=test;Password=never-print-this",
  { ok: true, lines: ["TASK3_HOST_CLASS=OTHER_OR_EXTERNAL", "TASK3_DATABASE_CLASS=OTHER"] },
  "unknown host is only classified as other"
);
classify(
  "Host=app.railway.internal;Host=evil.internal;Database=bliss;Password=never-print-this",
  { ok: false },
  "conflicting hosts"
);
classify(
  "Host=app.railway.internal;Database=bliss;Database=other;Password=never-print-this",
  { ok: false },
  "conflicting databases"
);
classify(
  null,
  { ok: false },
  "missing connection setting"
);
classify(
  "postgresql://user:never-print-this@db.example.invalid/postgres",
  { ok: false },
  "URI connection string is unsupported"
);
classify(
  'Host=app.railway.internal;Database="bliss;Password=never-print-this',
  { ok: false },
  "unterminated quoted value"
);

console.log("Task3 private DB target classifier: 9/9 synthetic fixtures PASS");
