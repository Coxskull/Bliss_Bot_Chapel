"""Reject false-clean evidence, stale databases, and cross-image audit reports."""
import copy
import base64
import hashlib
import io
import json
import tarfile
import tempfile
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


    def make_export_fixture(self, root):
        revision = "f" * 40
        labels = {"org.opencontainers.image.revision": revision}
        runtime = {"Labels": labels, "User": "1654", "Env": ["PATH=/usr/bin"],
                   "Entrypoint": ["dotnet", "Bliss.Api.dll"], "Cmd": None,
                   "WorkingDir": "/app", "ExposedPorts": {"8080/tcp": {}}}
        layers = ["sha256:" + "1" * 64]
        config = {"architecture": "amd64", "os": "linux", "rootfs": {"diff_ids": layers},
                  "config": runtime}
        config_bytes = json.dumps(config, separators=(",", ":")).encode()
        config_sha = hashlib.sha256(config_bytes).hexdigest()
        target_id = "sha256:" + config_sha
        manifest_bytes = json.dumps({"schemaVersion": 2,
            "config": {"digest": target_id, "size": len(config_bytes)}}).encode()
        archive = root / "image.tar"
        archive_manifest = json.dumps([{"Config": config_sha + ".json",
                                        "Layers": ["layer.tar"]}]).encode()
        with tarfile.open(archive, mode="w") as tar:
            for name, contents in [("manifest.json", archive_manifest),
                                   (config_sha + ".json", config_bytes)]:
                info = tarfile.TarInfo(name)
                info.size = len(contents)
                tar.addfile(info, io.BytesIO(contents))
        report = {"source": {"type": "image", "target": {
            "userInput": str(archive), "imageID": target_id,
            "config": base64.b64encode(config_bytes).decode(),
            "manifest": base64.b64encode(manifest_bytes).decode(),
            "manifestDigest": "sha256:" + hashlib.sha256(manifest_bytes).hexdigest()}}}
        inspected = {"Id": "sha256:" + "a" * 64, "RootFS": {"Layers": layers},
                     "Config": runtime, "Architecture": "amd64", "Os": "linux"}
        return archive, report, inspected

    def test_equivalent_exported_config_id_must_verify_layers_and_runtime(self):
        with tempfile.TemporaryDirectory() as folder:
            archive, report, inspected = self.make_export_fixture(Path(folder))
            proof = gate["verify_exported_image_provenance"](report, inspected, archive)
            self.assertEqual("EXPORT_BYTES_ROOTFS_AND_RUNTIME_MATCH", proof["image_id_comparison"])
            self.assertTrue(proof["provenance_validated"])
            self.assertFalse(proof["release_authorized"])
            inspected["Id"] = report["source"]["target"]["imageID"]
            proof = gate["verify_exported_image_provenance"](report, inspected, archive)
            self.assertEqual("DIRECT_MATCH", proof["image_id_comparison"])

    def test_falsified_cross_image_and_tampered_scanner_provenance_denied(self):
        with tempfile.TemporaryDirectory() as folder:
            archive, report, inspected = self.make_export_fixture(Path(folder))
            failures = [
                lambda r, i: i["RootFS"].update(Layers=["sha256:" + "2" * 64]),
                lambda r, i: i["Config"]["Labels"].update({"org.opencontainers.image.revision": "other"}),
                lambda r, i: i["Config"].update(User="root"),
                lambda r, i: r["source"]["target"].update(imageID="sha256:" + "3" * 64),
                lambda r, i: r["source"]["target"].update(config=base64.b64encode(b"forged").decode()),
                lambda r, i: r["source"]["target"].update(manifestDigest="sha256:" + "4" * 64),
                lambda r, i: r["source"]["target"].update(userInput="/tmp/unrelated.tar"),
            ]
            for mutation in failures:
                altered_report, altered_inspected = copy.deepcopy(report), copy.deepcopy(inspected)
                mutation(altered_report, altered_inspected)
                with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                    gate["verify_exported_image_provenance"](altered_report, altered_inspected, archive)


if __name__ == "__main__":
    unittest.main()
