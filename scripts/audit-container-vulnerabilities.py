#!/usr/bin/env python3
"""AGM-LAUNCH-010: collect image vulnerability evidence; never approve release."""
import argparse
import collections
import csv
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import tarfile

SCANNER_VERSION = "0.120.1"
SCANNER_ARCHIVE_SHA256 = "0a9ee97ef5ae2ee953b0a80098105052e846cdbe319a57d808b519c33cd1343d"


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



def archive_config_image_id(archive):
    """Hash the actual image config bytes in an exact docker-save archive.

    Docker's inspected image ID can name a higher-level image/index, whereas
    Grype reports the selected platform image *config* ID. Never accept a
    caller-supplied scanner target alone as cryptographic provenance.
    """
    with tarfile.open(archive, mode="r:") as bundle:
        try:
            member = bundle.getmember("manifest.json")
            if member.size < 2 or member.size > 1024 * 1024:
                raise ValueError("Ambiguous image manifest size")
            source = bundle.extractfile(member)
            if source is None:
                raise ValueError("Missing saved-image manifest")
            manifest = json.loads(source.read())
            if not isinstance(manifest, list) or len(manifest) != 1:
                raise ValueError("Saved Docker image must have exactly one platform manifest")
            config_name = manifest[0].get("Config") if isinstance(manifest[0], dict) else None
            if not isinstance(config_name, str) or not config_name or config_name.startswith("/") or ".." in Path(config_name).parts:
                raise ValueError("Unsafe saved-image configuration path")
            config = bundle.getmember(config_name)
            if config.size < 2 or config.size > 10 * 1024 * 1024:
                raise ValueError("Invalid saved-image configuration size")
            data = bundle.extractfile(config)
            if data is None:
                raise ValueError("Missing saved-image configuration blob")
            return "sha256:" + hashlib.sha256(data.read()).hexdigest()
        except (KeyError, json.JSONDecodeError, AttributeError, TypeError) as exc:
            raise ValueError("Saved-image configuration cannot be proven") from exc


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
        archive_config_id = archive_config_image_id(archive)
        # Scan the same immutable locally inspected Docker image, not an archive
        # whose extracted image config ID can diverge from the Docker image ID.
        # The report is still strictly checked against image_id by summarize().
        # An absent local image or ID mismatch FAILS CLOSED, never falls back to
        # a mutable tag, registry or synthetic clean scan.
        report = run(scanner + ["docker:" + image_id, "--output", "json"],
                     "vulnerabilities.json", timeout=600, tool_env=env)
        # Two independent exact-identity checks: the requested immutable
        # Docker inspection ID and the content-hash of the saved image config.
        # The latter is Grype's selected-platform imageID, which may differ
        # from the higher-level Docker image/index Id.
        if report.get("source", {}).get("target", {}).get("userInput") != image_id:
            raise ValueError("Scanner did not target the exact inspected Docker image ID")
        counts, rows = summarize(report, archive_config_id)
        summary = {"task_id": "AGM-LAUNCH-010", "gate": "VULNERABILITY_AUDIT_EVIDENCE_ONLY",
                   "source_revision": args.revision, "image_id": image_id,
                   "image_archive_sha256": archive_sha,
                   "archive_config_image_id": archive_config_id,
                   "scanner_report_image_id": report["source"]["target"]["imageID"],
                   "scanner_version": SCANNER_VERSION,
                   "scanner_archive_sha256": SCANNER_ARCHIVE_SHA256,
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
