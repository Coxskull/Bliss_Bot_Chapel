"""ALPHA fail-closed risk-triage contracts. Synthetic records only."""
import copy
import hashlib
import io
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent
SCRIPT = ROOT / "triage-container-findings.py"
triage = runpy.run_path(str(SCRIPT))
DOCKER_ID = "sha256:" + "1" * 64
CONFIG_ID = "sha256:" + "2" * 64


def report():
    return {
        "source": {"target": {"imageID": CONFIG_ID, "userInput": DOCKER_ID}},
        "descriptor": {"name": "grype", "version": "0.120.1"},
        "distro": {"name": "debian", "version": "12.15"},
        "ignoredMatches": [],
        "matches": [
            {"vulnerability": {"id": "CVE-2026-75143", "severity": "Critical",
                               "fix": {"state": "not-fixed"}},
             "artifact": {"name": "ffmpeg", "version": "5.1.9"}},
            {"vulnerability": {"id": "CVE-2026-75143", "severity": "Critical",
                               "fix": {"state": "not-fixed"}},
             "artifact": {"name": "libavcodec59", "version": "5.1.9"}},
            {"vulnerability": {"id": "CVE-2026-88888", "severity": "High",
                               "fix": {"state": "wont-fix"}},
             "artifact": {"name": "libc6", "version": "2.36"}},
        ]
    }


def summary():
    return {
        "gate": "VULNERABILITY_AUDIT_EVIDENCE_ONLY",
        "vulnerability_acceptance": False,
        "release_authorized": False,
        "scanner_report_image_id": CONFIG_ID,
        "image_id": DOCKER_ID,
        "image_archive_sha256": "a" * 64,
        "source_revision": "b" * 40,
        "scanner_version": "0.120.1",
        "raw_match_count": 3,
        "all_package_matches_by_severity": {"Critical": 2, "High": 1},
    }


class RiskReviewTests(unittest.TestCase):
    def test_critical_duplicates_are_not_counted_as_distinct_cves(self):
        result, rows = triage["analyze"](report(), summary(), "f" * 64)
        self.assertEqual(result["high_critical_distinct_advisory_ids"], 2)
        self.assertEqual(result["unique_advisory_ids_by_severity"]["Critical"], 1)
        self.assertEqual(result["high_critical_unique_ffmpeg_family_advisories"], 1)
        self.assertEqual(result["high_critical_ffmpeg_family_package_matches"], 2)
        self.assertEqual(result["ffmpeg_reachability"],
                         "UNKNOWN_REQUIRES_CODE_AND_NETWORK_BOUNDARY_REVIEW")
        self.assertFalse(result["release_authorized"])
        self.assertFalse(result["vulnerability_acceptance"])
        self.assertEqual(len(rows), 2)
        media_row = next(r for r in rows if r["advisory"] == "CVE-2026-75143")
        self.assertEqual(media_row["package_count"], 2)

    def test_wrong_image_scanner_or_untrusted_release_acceptance_is_rejected(self):
        mutations = [
            lambda r, s: s.update(image_id="sha256:wrong"),
            lambda r, s: s.update(scanner_report_image_id="sha256:wrong"),
            lambda r, s: r["source"]["target"].update(userInput="sha256:wrong"),
            lambda r, s: r["source"]["target"].update(imageID="sha256:wrong"),
            lambda r, s: s.update(release_authorized=True),
            lambda r, s: s.update(vulnerability_acceptance=True),
            lambda r, s: s.update(raw_match_count=2),
            lambda r, s: s.update(all_package_matches_by_severity={"Critical": 3}),
            lambda r, s: s.update(scanner_version="0.11"),
            lambda r, s: r.update(ignoredMatches=[{"test": "suppressed"}]),
            lambda r, s: r.update(distro={}),
            lambda r, s: r["matches"][0]["vulnerability"].update(id="bad"),
            lambda r, s: r["matches"][0]["artifact"].update(version=""),
            lambda r, s: r["matches"][0]["vulnerability"].update(severity="BAD"),
            lambda r, s: r["matches"][0]["vulnerability"]["fix"].update(state="unknown-new-state"),
        ]
        for mutate in mutations:
            with self.subTest(mutate=repr(mutate)):
                r, s = report(), summary()
                mutate(r, s)
                with self.assertRaises(ValueError):
                    triage["analyze"](r, s, "f" * 64)

    def test_same_cve_cannot_have_inconsistent_severity(self):
        r = report()
        r["matches"][1]["vulnerability"]["severity"] = "High"
        s = summary()
        s["all_package_matches_by_severity"] = {"Critical": 1, "High": 2}
        with self.assertRaisesRegex(ValueError, "Inconsistent severity"):
            triage["analyze"](r, s, "f" * 64)

    def test_cli_writes_deduplicated_metadata_only_and_is_not_release_approval(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            report_path = root / "report.json"
            summary_path = root / "summary.json"
            output = root / "out"
            payload = json.dumps(report())
            report_path.write_text(payload)
            summary_path.write_text(json.dumps(summary()))
            invocation = [sys.executable, str(SCRIPT), "--report", str(report_path),
                          "--summary", str(summary_path), "--output", str(output)]
            subprocess.run(invocation, check=True, capture_output=True, text=True)
            result = json.loads((output / "risk-summary.json").read_text())
            self.assertEqual(result["source_scan_sha256"],
                             hashlib.sha256(payload.encode()).hexdigest())
            self.assertFalse(result["release_authorized"])
            self.assertNotIn("PRIVATE", json.dumps(result))
            self.assertIn("CVE-2026-75143",
                          (output / "high-critical-advisories.csv").read_text())
            second = subprocess.run(invocation, check=False, capture_output=True, text=True)
            self.assertNotEqual(second.returncode, 0)

    def test_empty_findings_cannot_be_construed_as_release_authorization(self):
        r, s = report(), summary()
        r["matches"] = []
        s["raw_match_count"] = 0
        s["all_package_matches_by_severity"] = {}
        result, rows = triage["analyze"](r, s, "a" * 64)
        self.assertFalse(result["release_authorized"])
        self.assertTrue(result["independent_security_review_required"])
        self.assertEqual(rows, [])


if __name__ == "__main__":
    unittest.main()
