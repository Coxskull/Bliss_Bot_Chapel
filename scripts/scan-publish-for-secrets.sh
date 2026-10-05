#!/usr/bin/env bash
# Fail when a published folder contains a committed secret or a private key.
# The scanner prints the setting name. It does not print the secret value.
set -euo pipefail

root="${1:-}"
if [[ -z "$root" || ! -d "$root" ]]; then
  echo "Publish scan refused: pass the published directory." >&2
  exit 2
fi

python3 - "$root" <<'PY'
import json
import sys
from pathlib import Path

root = Path(sys.argv[1])
failed = False

def reject(message: str) -> None:
    global failed
    failed = True
    print(message, file=sys.stderr)

blocked_names = {".env", "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519"}
blocked_suffixes = {".pfx", ".pem", ".key", ".dump"}

for path in root.rglob("*"):
    if not path.is_file():
        continue
    name = path.name
    if name in blocked_names or path.suffix.lower() in blocked_suffixes:
        reject(f"Publish scan failed: {name} must not be in the published folder.")
        continue
    if not name.startswith("appsettings") or path.suffix.lower() != ".json":
        continue
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        reject(f"Publish scan failed: {name} is not valid JSON.")
        continue
    connection = document.get("ConnectionStrings", {}).get("DefaultConnection", "")
    secret = document.get("Authentication", {}).get("ClientSecret", "")
    password = document.get("Runtime", {}).get("DataProtectionCertificatePassword", "")
    if isinstance(connection, str) and connection.strip():
        reject("Publish scan failed: ConnectionStrings:DefaultConnection is not empty.")
    if isinstance(secret, str) and secret.strip():
        reject("Publish scan failed: Authentication:ClientSecret is not empty.")
    if isinstance(password, str) and password.strip():
        reject("Publish scan failed: Runtime:DataProtectionCertificatePassword is not empty.")

if failed:
    sys.exit(1)
print("Publish scan passed. No embedded secret was found.")
PY
