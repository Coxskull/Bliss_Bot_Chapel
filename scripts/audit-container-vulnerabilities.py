#!/usr/bin/env python3
"""AGM-LAUNCH-010: collect image vulnerability evidence; never approve release."""
import argparse
import base64
import collections
import csv
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tarfile
import tempfile

SCANNER_VERSION = "0.120.1"
SCANNER_OCI_IMAGE = "anchore/grype"
SCANNER_OCI_MANIFEST_SHA256 = "sha256:7bb1480f73d9a23b8e6137669c9b780389492778dc2da5c818d0add7e828ed45"


def validate_database(status, now):
    if status.get("valid") is not True or status.get("error"):
        raise ValueError("Vulnerability database is not valid")
    if not re.fullmatch(r"v?6\.[0-9]+\.[0-9]+", str(status.get("schemaVersion", ""))):
        raise ValueError("Unexpected vulnerability database schema")
    built = dt.datetime.fromisoformat(status["built"].replace("Z", "+00:00"))
    if built.tzinfo is None:
        raise ValueError("Database build timestamp must include timezone")
    age = now - built
    if age > dt.timedelta(hours=48) or age < -dt.timedelta(minutes=5):
        raise ValueError("Database is stale or future-dated")
    return built.isoformat()



def verify_exported_image_provenance(report, inspected, image_archive):
    """Verify scanner bytes, manifest, image rootfs and runtime configuration.

    Docker image inspect IDs can differ from exported archive config IDs.
    Neither source is trusted alone; no provenance result approves deployment.
    """
    target = report.get("source", {}).get("target", {})
    if report.get("source", {}).get("type") != "image" or target.get("userInput") != str(image_archive):
        raise ValueError("Scanner report does not identify the requested image archive")
    with tarfile.open(image_archive, "r:*") as archive:
        manifest_file = archive.extractfile("manifest.json")
        if manifest_file is None:
            raise ValueError("Docker export lacks its image manifest")
        entries = json.loads(manifest_file.read(1024 * 1024))
        if not isinstance(entries, list) or len(entries) != 1:
            raise ValueError("Docker export must contain exactly one inspected image")
        entry = entries[0]
        config_name = entry.get("Config", "")
        if re.fullmatch(r"[0-9a-f]{64}\.json", config_name):
            config_digest = config_name[:-5]
        elif re.fullmatch(r"blobs/sha256/[0-9a-f]{64}", config_name):
            config_digest = config_name.rsplit("/", 1)[1]
        else:
            raise ValueError("Docker export config path is invalid")
        member = archive.getmember(config_name)
        if member.size > 1024 * 1024:
            raise ValueError("Docker export config is unexpectedly large")
        config_file = archive.extractfile(member)
        if config_file is None:
            raise ValueError("Docker export config is missing")
        config_bytes = config_file.read()
    exported_id = "sha256:" + hashlib.sha256(config_bytes).hexdigest()
    if exported_id != "sha256:" + config_digest or target.get("imageID") != exported_id:
        raise ValueError("Scanner target differs from exported config content")
    try:
        scanner_config = base64.b64decode(target["config"], validate=True)
        manifest_bytes = base64.b64decode(target["manifest"], validate=True)
        distribution_manifest = json.loads(manifest_bytes)
        exported_config = json.loads(config_bytes)
    except (ValueError, KeyError, TypeError) as error:
        raise ValueError("Scanner target metadata is missing or invalid") from error
    if scanner_config != config_bytes:
        raise ValueError("Scanner config does not match exported immutable bytes")
    if target.get("manifestDigest") != "sha256:" + hashlib.sha256(manifest_bytes).hexdigest():
        raise ValueError("Scanner manifest digest does not match its contents")
    if (distribution_manifest.get("schemaVersion") != 2
            or distribution_manifest.get("config", {}).get("digest") != exported_id
            or distribution_manifest.get("config", {}).get("size") != len(config_bytes)):
        raise ValueError("Scanner manifest does not bind the exported config")
    inspected_layers = inspected.get("RootFS", {}).get("Layers")
    exported_layers = exported_config.get("rootfs", {}).get("diff_ids")
    if not inspected_layers or inspected_layers != exported_layers:
        raise ValueError("Scanner export rootfs differs from Docker-inspected layers")
    expected_config = inspected.get("Config") or {}
    actual_config = exported_config.get("config") or {}
    for key in ("Labels", "User", "Env", "Entrypoint", "Cmd", "WorkingDir", "ExposedPorts"):
        if expected_config.get(key) != actual_config.get(key):
            raise ValueError("Scanner export runtime config differs from Docker-inspected image")
    for key in ("Architecture", "Os"):
        if inspected.get(key) != exported_config.get(key.lower()):
            raise ValueError("Scanner export platform differs from Docker-inspected image")
    return {"docker_inspect_image_id": inspected["Id"],
            "exported_image_config_id": exported_id,
            "image_id_comparison": "DIRECT_MATCH" if inspected["Id"] == exported_id
                                   else "EXPORT_BYTES_ROOTFS_AND_RUNTIME_MATCH",
            "provenance_validated": True, "release_authorized": False}


def summarize(report, image_id):
    if report.get("source", {}).get("target", {}).get("imageID") != image_id:
        raise ValueError("Scanner target differs from the inspected immutable image")
    if report.get("descriptor", {}).get("name") != "grype" or report["descriptor"].get("version") != SCANNER_VERSION:
        raise ValueError("Unexpected scanner provenance")
    matches = report.get("matches")
    if not isinstance(matches, list) or report.get("ignoredMatches"):
        raise ValueError("Missing findings or suppressed matches")
    if not isinstance(report.get("distro"), dict) or not report["distro"].get("name"):
        raise ValueError("OS distribution identification missing")
    rows = []
    total = collections.Counter()
    os_counts = collections.Counter()
    for match in matches:
        v, a = match["vulnerability"], match["artifact"]
        severity = v["severity"]
        if severity not in {"Negligible", "Low", "Medium", "High", "Critical", "Unknown"}:
            raise ValueError("Unknown severity vocabulary; do not silently discard findings")
        if not v.get("id") or not a.get("name") or not a.get("version") or not a.get("type"):
            raise ValueError("Finding identity incomplete")
        total[severity] += 1
        if a["type"] in {"deb", "rpm", "apk"}:
            os_counts[severity] += 1
        fix = v.get("fix", {})
        rows.append({"finding_id": v["id"], "severity": severity, "package_name": a["name"],
                     "package_type": a["type"], "installed_version": a["version"],
                     "fix_state": fix.get("state", "unknown"),
                     "fix_versions": ";".join(fix.get("versions", [])), "namespace": v.get("namespace", "")})
    rows.sort(key=lambda r: tuple(r[k] for k in ["severity", "finding_id", "package_name", "installed_version"]))
    return {"raw_match_count": len(matches), "all_package_matches_by_severity": dict(sorted(total.items())),
            "os_package_matches_by_severity": dict(sorted(os_counts.items())),
            "unique_vulnerability_ids": len({r["finding_id"] for r in rows}),
            "fixed_state_matches": sum(r["fix_state"] == "fixed" for r in rows),
            "distro": report["distro"],
            "findings_status": "FINDINGS_REQUIRE_REVIEW" if matches else "NO_MATCHES_REPORTED",
            "vulnerability_acceptance": False}, rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--scanner", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9a-f]{40}", args.revision):
        parser.error("Full source revision required")
    args.output.mkdir(parents=True, exist_ok=False)
    output = args.output.resolve()
    # Explicit config and small environment prevent inherited app secrets or GRYPE_ ignores.
    env = {k: v for k, v in os.environ.items() if k in {
        "PATH", "HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY", "http_proxy", "https_proxy", "no_proxy",
        "SSL_CERT_FILE", "SSL_CERT_DIR", "REQUESTS_CA_BUNDLE"}}

    def run(command, log_name, timeout=420, tool_env=None):
        with (output / log_name).open("w", encoding="utf-8") as log:
            result = subprocess.run(command, stdout=log, stderr=subprocess.PIPE, text=True,
                                    timeout=timeout, env=tool_env)
        (output / (log_name + ".stderr")).write_text(result.stderr, encoding="utf-8")
        if result.returncode:
            raise RuntimeError("Audit command failed: " + log_name)
        return json.loads((output / log_name).read_text()) if log_name.endswith(".json") else None

    docker = ["docker", "--host=unix:///var/run/docker.sock"]
    image = json.loads(subprocess.check_output([*docker, "image", "inspect", args.image], text=True))[0]
    image_id = image["Id"]
    if image["Config"].get("Labels", {}).get("org.opencontainers.image.revision") != args.revision:
        raise ValueError("Image source revision label mismatch")
    with tempfile.TemporaryDirectory(prefix="agm-image-audit-") as temp:
        cache = Path(temp)
        config = cache / "grype.json"
        config.write_text(json.dumps({"check-for-app-update": False, "ignore": [], "only-fixed": False,
                                     "only-notfixed": False, "ignore-wontfix": "",
                                     "search": {"scope": "squashed"},
                                     "db": {"cache-dir": str(cache / "db"), "auto-update": False,
                                            "validate-by-hash-on-start": True, "validate-age": True,
                                            "max-allowed-built-age": "48h"}}))
        scanner = [str(args.scanner.resolve()), "--config", str(config)]
        version = run(scanner + ["version", "--output", "json"], "scanner-version.json", tool_env=env)
        if version.get("version") != SCANNER_VERSION or version.get("supportedDbSchema") != 6:
            raise ValueError("Scanner version/schema does not match reviewed pin")
        run(scanner + ["db", "update"], "database-update.log", tool_env=env)
        database = run(scanner + ["db", "status", "--output", "json"], "database-status.json", tool_env=env)
        validate_database(database, dt.datetime.now(dt.timezone.utc))
        archive = cache / "image.tar"
        run([*docker, "image", "save", "--output", str(archive), image_id], "image-save.log")
        with archive.open("rb") as stream:
            archive_sha = hashlib.file_digest(stream, "sha256").hexdigest()
        report = run(scanner + ["docker-archive:" + str(archive), "--output", "json"],
                     "vulnerabilities.json", timeout=600, tool_env=env)
        provenance = verify_exported_image_provenance(report, image, archive)
        counts, rows = summarize(report, provenance["exported_image_config_id"])
        summary = {"task_id": "AGM-LAUNCH-010", "gate": "VULNERABILITY_AUDIT_EVIDENCE_ONLY",
                   "source_revision": args.revision, "image_id": image_id,
                   "image_archive_sha256": archive_sha, "scanner_version": SCANNER_VERSION,
                   "image_export_provenance": provenance,
                   "scanner_distribution": SCANNER_OCI_IMAGE,
                   "scanner_oci_manifest_sha256": SCANNER_OCI_MANIFEST_SHA256,
                   "database": database, "actual_completed_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
                   **counts, "coverage": "Grype squashed image package catalog/matches; not exploitability or complete security assurance",
                   "application_credentials_forwarded": False, "image_executed_by_audit": False,
                   "stripe_account": "UNVERIFIED / OWNER DECISION REQUIRED",
                   "provider_calls": 0, "image_pushed": False, "production_access": False,
                   "owner_acceptance": False, "release_authorized": False}
        with (output / "findings.csv").open("w", newline="", encoding="utf-8") as stream:
            writer = csv.DictWriter(stream, fieldnames=["finding_id", "severity", "package_name", "package_type",
                                                        "installed_version", "fix_state", "fix_versions", "namespace"])
            writer.writeheader(); writer.writerows(rows)
        (output / "vulnerability-summary.json").write_text(json.dumps(summary, indent=2) + "\n")
        print(json.dumps({"audit_execution": "COMPLETED", **counts, "release_authorized": False}))


if __name__ == "__main__":
    main()
