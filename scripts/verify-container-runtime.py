#!/usr/bin/env python3
"""AGM-LAUNCH-010: offline container packaging/refusal checks, never activation."""
import argparse
import hashlib
import json
import re
import subprocess
import tempfile
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlsplit


class EntryAssets(HTMLParser):
    def __init__(self):
        super().__init__()
        self.urls = []

    def handle_starttag(self, tag, attrs):
        values = dict(attrs)
        if tag == "script" and "src" in values:
            self.urls.append(values["src"])
        if tag == "link" and values.get("rel") == "stylesheet":
            self.urls.append(values.get("href", ""))


def verify_assets(directory):
    """Require local entry JS/CSS; refuse traversal, symlinks and external URLs."""
    entry = directory / "index.html"
    if not entry.is_file() or entry.is_symlink():
        raise ValueError("Financial entry missing or symlinked")
    parser = EntryAssets()
    parser.feed(entry.read_text(encoding="utf-8"))
    suffixes = set()
    prefix = "/operations/financial-review/"
    for url in parser.urls:
        parts = urlsplit(url)
        if (parts.scheme or parts.netloc or parts.query or parts.fragment
                or not url.startswith(prefix) or "%" in url or "\\" in url):
            raise ValueError("Entry asset URL must be a canonical local path")
        relative = url[len(prefix):]
        if not relative or any(p in ("", ".", "..") for p in relative.split("/")):
            raise ValueError("Entry asset path traversal or empty segment")
        asset = directory / relative
        if not asset.is_file() or any(p.is_symlink() for p in [asset, *asset.parents]):
            raise ValueError("Entry asset missing or symlinked")
        suffixes.add(asset.suffix)
    if not {".js", ".css"}.issubset(suffixes):
        raise ValueError("Entry must reference packaged JavaScript and CSS")
    hashes = {}
    for path in sorted(directory.rglob("*")):
        if path.is_symlink():
            raise ValueError("Packaged asset symlink refused")
        if path.is_file():
            hashes[str(path.relative_to(directory))] = hashlib.sha256(path.read_bytes()).hexdigest()
    return hashes


def verify_metadata(metadata, revision):
    config = metadata["Config"]
    user = config.get("User", "")
    # The reviewed .NET runtime supplies numeric APP_UID; reject root/name ambiguity.
    if not re.fullmatch(r"[1-9][0-9]*", user):
        raise ValueError("Expected explicit nonzero numeric runtime user")
    env = dict(item.split("=", 1) for item in config.get("Env", []))
    if env.get("ASPNETCORE_ENVIRONMENT") != "Production" or env.get("ASPNETCORE_URLS") != "http://+:8080":
        raise ValueError("Production/8080 defaults changed")
    if config.get("Labels", {}).get("org.opencontainers.image.revision") != revision:
        raise ValueError("Image revision does not match checked-out source")
    if config.get("Entrypoint") != ["dotnet", "Bliss.Api.dll"]:
        raise ValueError("Unexpected runtime entrypoint")
    if "8080/tcp" not in config.get("ExposedPorts", {}):
        raise ValueError("Expected exposed port 8080")
    return int(user)


def verify_refusal(exit_code, logs):
    if exit_code == 0 or "OIDC authentication must be enabled outside Development." not in logs:
        raise ValueError("Missing exact unauthenticated Production refusal")
    if "Now listening on:" in logs or "Application started." in logs:
        raise ValueError("Application served before refusing unauthenticated Production")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", args.revision):
        parser.error("revision must be a full lowercase commit SHA")
    args.output.mkdir(parents=True, exist_ok=False)

    def docker(*arguments, timeout=30):
        # Arguments never inherit application credentials; no -e host-var forwarding.
        result = subprocess.run(["docker", *arguments], capture_output=True, text=True, timeout=timeout)
        if result.returncode:
            raise RuntimeError("Docker operation failed: " + arguments[0])
        return (result.stdout + (result.stderr if arguments[0] == "logs" else "")).strip()

    # Resolve once, then use immutable image ID for every operation (no retag race).
    metadata = json.loads(docker("image", "inspect", args.image))[0]
    image_id = metadata["Id"]
    uid = verify_metadata(metadata, args.revision)
    isolation = ["--network", "none", "--read-only", "--cap-drop", "ALL",
                 "--security-opt", "no-new-privileges", "--pids-limit", "128",
                 "--memory", "512m", "--tmpfs", "/tmp:rw,nosuid,nodev,size=64m"]
    actual_uid = int(docker("run", "--rm", *isolation, "--entrypoint", "id", image_id, "-u"))
    if actual_uid != uid or actual_uid == 0:
        raise ValueError("Observed runtime UID differs from configured non-root user")
    # Execute as configured user, with no network, ports, volumes or credentials.
    media = docker("run", "--rm", *isolation, "--entrypoint", "sh", image_id, "-ec",
                   "command -v ffmpeg; test -s /usr/share/fonts/truetype/dejavu/DejaVuSans.ttf; "
                   "test -s /usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf; "
                   "if command -v node || command -v npm; then exit 1; fi; "
                   "test ! -e /run/secrets/proxy_ca")
    container = docker("create", *isolation, image_id)
    try:
        with tempfile.TemporaryDirectory(prefix="agm-runtime-assets-") as temp:
            assets = Path(temp) / "financial-review"
            docker("cp", container + ":/app/frontend/operations/financial-review", str(assets))
            hashes = verify_assets(assets)
        docker("start", container)
        exit_code = int(docker("wait", container, timeout=30))
        logs = docker("logs", container)
        (args.output / "production-refusal.log").write_text(logs + "\n", encoding="utf-8")
        verify_refusal(exit_code, logs)
        state = json.loads(docker("inspect", "--format", "{{json .State}}", container))
        if state.get("OOMKilled") or state.get("Error"):
            raise ValueError("Container infrastructure failure cannot prove auth refusal")
        report = {"task_id": "AGM-LAUNCH-010", "gate": "PARTIAL_CONTAINER_ACCEPTANCE",
                  "source_revision": args.revision, "image_id": image_id,
                  "configured_uid": uid, "observed_uid": actual_uid,
                  "production_auth_refusal_exit": exit_code, "financial_assets_sha256": hashes,
                  "media_path": media, "network": "none", "published_ports": [],
                  "readonly_root": True, "host_credentials_forwarded": False,
                  "authenticated_mock_workflow_verified": False,
                  "browser_verified": False, "os_vulnerability_audit_verified": False,
                  "stripe_account": "UNVERIFIED / OWNER DECISION REQUIRED",
                  "provider_calls": 0, "deployment_performed": False, "owner_acceptance": False}
        (args.output / "container-runtime.json").write_text(json.dumps(report, indent=2) + "\n")
        print("Partial container acceptance passed: non-root, assets, media, Production auth refusal.")
    finally:
        docker("rm", "-f", container)


if __name__ == "__main__":
    main()
