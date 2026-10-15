"""Reject false-clean evidence, stale databases, and cross-image audit reports."""
import copy
import datetime as dt
from pathlib import Path
import runpy
import unittest

gate = runpy.run_path(str(Path(__file__).with_name("audit-container-vulnerabilities.py")))
NOW = dt.datetime(2026, 10, 15, tzinfo=dt.timezone.utc)
IMAGE = "sha256:" + "a" * 64


class VulnerabilityEvidenceTests(unittest.TestCase):
    def test_database_requires_hash_valid_recent_timestamp_and_expected_schema(self):
        valid = {"valid": True, "built": "2026-10-14T10:00:00Z", "schemaVersion": "6.0.0"}
        gate["validate_database"](valid, NOW)
        # Actual Grype0.120.1 DB status prefixes its semantic version with v.
        gate["validate_database"]({**valid, "schemaVersion": "v6.1.10"}, NOW)
        for update in [{"valid": False}, {"error": "download failed"}, {"schemaVersion": "5.0.0"},
                       {"built": "2026-10-12T00:00:00Z"}, {"built": "2026-10-16T00:00:00Z"},
                       {"built": "2026-10-14T10:00:00"}, {"schemaVersion": "6.bad"},
                       {"schemaVersion": "v7.1.10"}]:
            with self.subTest(update=update), self.assertRaises(ValueError):
                gate["validate_database"]({**valid, **update}, NOW)

    def report(self):
        return {"source": {"target": {"imageID": IMAGE}},
                "descriptor": {"name": "grype", "version": gate["SCANNER_VERSION"]},
                "distro": {"name": "debian", "version": "12"}, "ignoredMatches": [],
                "matches": [{"vulnerability": {"id": "CVE-2099-0001", "severity": "High",
                                                 "namespace": "debian:12", "fix": {"state": "not-fixed", "versions": []}},
                             "artifact": {"name": "fixture-lib", "version": "1.0", "type": "deb"}},
                            {"vulnerability": {"id": "CVE-2099-0002", "severity": "Unknown", "fix": {"state": "fixed", "versions": ["2.0"]}},
                             "artifact": {"name": "fixture-app", "version": "1.0", "type": "dotnet"}}]}

    def test_unfixed_and_unknown_findings_are_preserved_and_os_counts_are_separate(self):
        summary, rows = gate["summarize"](self.report(), IMAGE)
        self.assertEqual(2, summary["raw_match_count"])
        self.assertEqual({"High": 1, "Unknown": 1}, summary["all_package_matches_by_severity"])
        self.assertEqual({"High": 1}, summary["os_package_matches_by_severity"])
        self.assertEqual("FINDINGS_REQUIRE_REVIEW", summary["findings_status"])
        self.assertFalse(summary["vulnerability_acceptance"])
        self.assertEqual("not-fixed", rows[0]["fix_state"])
        self.assertEqual("2.0", rows[1]["fix_versions"])

    def test_wrong_image_scanner_missing_os_or_suppressed_results_are_not_clean(self):
        for change in [lambda r: r["source"]["target"].update(imageID="sha256:other"),
                       lambda r: r["descriptor"].update(version="other"), lambda r: r.update(matches=None),
                       lambda r: r.update(distro={}), lambda r: r.update(ignoredMatches=[{}]),
                       lambda r: r["matches"][0]["vulnerability"].update(severity="unexpected")]:
            report = copy.deepcopy(self.report());change(report)
            with self.assertRaises(ValueError):
                gate["summarize"](report, IMAGE)

    def test_no_matches_never_grants_vulnerability_or_release_acceptance(self):
        report = self.report();report["matches"] = []
        summary, rows = gate["summarize"](report, IMAGE)
        self.assertEqual("NO_MATCHES_REPORTED", summary["findings_status"])
        self.assertFalse(summary["vulnerability_acceptance"])
        self.assertEqual([], rows)


if __name__ == "__main__":
    unittest.main()
