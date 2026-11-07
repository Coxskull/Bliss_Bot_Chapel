#!/usr/bin/env python3
"""AGM launch: offline, fail-closed deduplication of exact-image Grype evidence.

This tool NEVER authorizes a release and does not attempt CVE exploitability
inference. It groups package-level matches by unique CVE and component family.
No network, production DB, credentials, customer data or scanner suppression.
"""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path
import re

SEVERITIES = ("Critical", "High", "Medium", "Low", "Negligible", "Unknown")
MEDIA_PACKAGE_NAMES = frozenset({
    "ffmpeg", "ffprobe", "libavcodec59", "libavdevice59",
    "libavfilter8", "libavformat59", "libavutil57", "libpostproc56",
    "libswresample4", "libswscale6",
})
ID = re.compile(r"^(?:CVE-[0-9]{4}-[0-9]{4,}|GHSA-[A-Za-z0-9-]+)$")


def analyze(report, summary, report_hash):
    """Strictly bind risk output to the already accepted image scanner report."""
    if summary.get("audit_execution") not in (None, "COMPLETED"):
        raise ValueError("Scanner did not complete")
    if summary.get("gate") != "VULNERABILITY_AUDIT_EVIDENCE_ONLY":
        raise ValueError("Expected strict evidence-only scanner output")
    if summary.get("vulnerability_acceptance") is not False or summary.get("release_authorized") is not False:
        raise ValueError("Risk evidence must never authorize release")
    target = report.get("source", {}).get("target", {})
    if not target.get("imageID") or target.get("imageID") != summary.get("scanner_report_image_id"):
        raise ValueError("Scanner config digest mismatch")
    if not target.get("userInput") or target.get("userInput") != summary.get("image_id"):
        raise ValueError("Requested Docker inspected digest mismatch")
    for key in ("image_archive_sha256", "source_revision"):
        if not isinstance(summary.get(key), str) or not summary[key]:
            raise ValueError("Missing inspected image provenance: " + key)
    if report.get("descriptor", {}).get("name") != "grype" or report.get("descriptor", {}).get("version") != summary.get("scanner_version"):
        raise ValueError("Scanner identity mismatch")
    if not isinstance(report.get("distro"), dict) or not report["distro"].get("name"):
        raise ValueError("Missing operating-system identity")
    matches = report.get("matches")
    if not isinstance(matches, list) or report.get("ignoredMatches"):
        raise ValueError("Missing or suppressed scanner matches")
    if len(matches) > 100000 or len(matches) != summary.get("raw_match_count"):
        raise ValueError("Match count changed after scan")
    by_severity = collections.Counter()
    unique = collections.defaultdict(set)
    media = collections.defaultdict(set)
    packages_by_cve = collections.defaultdict(set)
    states_by_cve = collections.defaultdict(set)
    severity_by_cve = {}
    for match in matches:
        v = match.get("vulnerability") or {}
        a = match.get("artifact") or {}
        cve, sev, pkg = v.get("id"), v.get("severity"), a.get("name")
        if not isinstance(cve, str) or not ID.fullmatch(cve):
            raise ValueError("Missing or malformed advisory identifier")
        if sev not in SEVERITIES or not isinstance(pkg, str) or not pkg or not a.get("version"):
            raise ValueError("Unusable severity or package identity")
        if cve in severity_by_cve and severity_by_cve[cve] != sev:
            raise ValueError("Inconsistent severity for same vulnerability")
        severity_by_cve[cve] = sev
        by_severity[sev] += 1
        unique[sev].add(cve)
        packages_by_cve[cve].add(pkg)
        state = (v.get("fix") or {}).get("state") or "unknown"
        if state not in ("fixed", "not-fixed", "wont-fix", "unknown"):
            raise ValueError("Unrecognized vulnerability fix state")
        states_by_cve[cve].add(state)
        if pkg in MEDIA_PACKAGE_NAMES:
            media[sev].add(cve)
    expected_counts = summary.get("all_package_matches_by_severity")
    if not isinstance(expected_counts, dict) or dict(sorted(by_severity.items())) != expected_counts:
        raise ValueError("Severity totals disagree with original scan")
    high_critical = sorted(unique["Critical"] | unique["High"])
    high_critical_rows = [
        {
            "advisory": cve,
            "severity": severity_by_cve[cve],
            "package_count": len(packages_by_cve[cve]),
            "package_names": ";".join(sorted(packages_by_cve[cve])),
            "fix_states": ";".join(sorted(states_by_cve[cve])),
            "ffmpeg_family_present": bool(packages_by_cve[cve] & MEDIA_PACKAGE_NAMES),
        }
        for cve in high_critical
    ]
    return {
        "gate": "INDEPENDENT_VULNERABILITY_REVIEW_REQUIRED",
        "source_image_id": summary["image_id"],
        "source_image_config_id": target["imageID"],
        "source_revision": summary["source_revision"],
        "source_scan_sha256": report_hash,
        "scanner_version": summary["scanner_version"],
        "os_distro": report["distro"]["name"],
        "package_matches_by_severity": dict(sorted(by_severity.items())),
        "unique_advisory_ids_by_severity": {s: len(unique[s]) for s in SEVERITIES},
        "high_critical_distinct_advisory_ids": len(high_critical),
        "high_critical_unique_ffmpeg_family_advisories": len(media["Critical"] | media["High"]),
        "high_critical_ffmpeg_family_package_matches": sum(
            1 for m in matches if m["vulnerability"]["severity"] in ("Critical", "High")
            and m["artifact"]["name"] in MEDIA_PACKAGE_NAMES),
        "ffmpeg_reachability": "UNKNOWN_REQUIRES_CODE_AND_NETWORK_BOUNDARY_REVIEW",
        "remediation_required": True,
        "independent_security_review_required": True,
        "vulnerability_acceptance": False,
        "release_authorized": False,
    }, high_critical_rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--summary", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if args.output.exists():
        parser.error("Refusing to overwrite existing risk evidence")
    if args.output.resolve() in (args.report.resolve(), args.summary.resolve()):
        parser.error("Output must not modify scanner source evidence")
    for source in (args.report, args.summary):
        if not source.is_file() or source.stat().st_size > 50_000_000:
            parser.error("Missing or oversized input evidence")
    data = args.report.read_bytes()
    report = json.loads(data)
    summary = json.loads(args.summary.read_text())
    result, rows = analyze(report, summary, hashlib.sha256(data).hexdigest())
    args.output.mkdir(parents=True)
    (args.output / "risk-summary.json").write_text(
        json.dumps(result, indent=2, sort_keys=True) + "\n")
    with (args.output / "high-critical-advisories.csv").open("w", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=(
            "advisory", "severity", "package_count", "package_names",
            "fix_states", "ffmpeg_family_present"))
        writer.writeheader()
        writer.writerows(rows)
    print(json.dumps({
        "risk_review": "REQUIRED", "release_authorized": False,
        "high_critical_distinct": result["high_critical_distinct_advisory_ids"],
        "ffmpeg_family_distinct": result["high_critical_unique_ffmpeg_family_advisories"],
    }))


if __name__ == "__main__":
    main()
