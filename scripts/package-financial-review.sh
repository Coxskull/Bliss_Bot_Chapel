#!/usr/bin/env bash
# AGM-LAUNCH-010: local artifact creation only; does not deploy or start services.
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
package_output="${1:-}"
if [[ -z "$package_output" || "$package_output" != /* || -e "$package_output" ]]; then
  echo "Pass a new absolute output directory; existing paths are refused." >&2
  exit 2
fi
[[ -z "$(git -C "$repo_root" status --porcelain)" ]] || { echo "Package refused: source must be a clean committed checkout." >&2; exit 2; }
node -e 'const [major,minor]=process.versions.node.split(".").map(Number);if(major<22||(major===22&&minor<12))process.exit(1)' || { echo "Node22.12+ required; Node24 recommended." >&2; exit 2; }
[[ "$(dotnet --version)" == 8.* ]] || { echo ".NET8 SDK required." >&2; exit 2; }
cd "$repo_root/bliss-frontend"
npm ci --ignore-scripts
npm test
npm run build
cd "$repo_root"
dotnet publish Bliss.Api/Bliss.Api.csproj -c Release -o "$package_output" /p:UseAppHost=false
bash scripts/scan-publish-for-secrets.sh "$package_output"
python3 - "$repo_root" "$package_output" <<'PY'
import hashlib,json,re,subprocess,sys
from pathlib import Path
source=Path(sys.argv[1])/'frontend/operations/financial-review';target=Path(sys.argv[2])/'frontend/operations/financial-review'
assert (target/'index.html').is_file(), 'Published financial index missing'
assets=re.findall(r'(?:src|href)="(/operations/financial-review/[^"?#]+)"',(target/'index.html').read_text())
assert assets and any(a.endswith('.js') for a in assets) and any(a.endswith('.css') for a in assets), 'Published entry assets missing'
for url in assets:
 relative=url.removeprefix('/operations/financial-review/');assert (target/relative).is_file(), 'Missing entry asset'
files={str(p.relative_to(source)):hashlib.sha256(p.read_bytes()).hexdigest() for p in source.rglob('*') if p.is_file()}
assert files and all((target/name).is_file() and hashlib.sha256((target/name).read_bytes()).hexdigest()==digest for name,digest in files.items()), 'Published assets differ from build'
manifest={'taskId':'AGM-LAUNCH-010','head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=sys.argv[1],text=True).strip(),'financialAssets':files,'accountStatus':'UNVERIFIED / OWNER DECISION REQUIRED','deploymentPerformed':False}
(Path(sys.argv[2])/'financial-package-manifest.json').write_text(json.dumps(manifest,indent=2))
print('Published financial entry and all asset hashes verified.')
PY
