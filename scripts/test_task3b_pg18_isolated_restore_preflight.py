#!/usr/bin/env python3
"""Offline-only synthetic contract for Task #3B PG18 restore preflight.

No real source backup, database, secret key, or cloud resource is accessed.
Run: python3 scripts/test_task3b_pg18_isolated_restore_preflight.py
"""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).with_name("task3b-bliss-pg18-isolated-restore.sh")
FILTER = Path(__file__).with_name("task3b_pg18_stream_header.py")

class GuardTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory(prefix="task3b-synthetic-")
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        self.archive = root / "bliss-pg18-20261121T023709Z-cda88608.gpg"
        self.archive.write_bytes(b"fictional-not-a-real-backup-" * 14)
        self.manifest = root / "bliss-pg18-20261121T023709Z-cda88608.pending.json"
        self.data = {
            "schema": "alpha-bliss-pg18-recovery/v1",
            "sourceProjectId": "fb33c1b7-03fb-42c8-89b1-081951a738fd",
            "sourceEnvironmentId": "b67a9836-36bf-4083-93f7-00c867b13f39",
            "sourceServiceId": "6aa78407-1632-48c9-8ea4-0289a7d36a5b",
            "sourceDatabase": "bliss",
            "sourcePostgresMajor": 18,
            "sourcePublicTableCount": 0,
            "encryption": "GPG_RECIPIENT",
            "gpgRecipientFingerprint": "0123456789ABCDEF0123456789ABCDEF01234567",
            "archiveFileName": self.archive.name,
            "archiveSha256": hashlib.sha256(self.archive.read_bytes()).hexdigest(),
            "isolatedRestoreVerified": False
        }
        self.save()

    def save(self):
        self.manifest.write_text(json.dumps(self.data), encoding="utf-8")

    def call(self, *args, extra_env=None):
        env = dict(os.environ)
        for name in ("RAILWAY_PROJECT_ID", "RAILWAY_SERVICE_ID",
                     "RAILWAY_ENVIRONMENT_ID", "TASK3B_OFFLINE_RECOVERY_HOST_ACK"):
            env.pop(name, None)
        env.update(extra_env or {})
        return subprocess.run(["bash", str(SCRIPT), str(self.archive), str(self.manifest), *args],
                              env=env, capture_output=True, text=True, timeout=15)

    def refused(self, result, message):
        self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("TASK3B_ISOLATED_PG18_RESTORE=FAILED_" + message, result.stdout)
        self.assertNotIn("Traceback", result.stderr)

    def test_valid_preflight_stays_held(self):
        result = self.call()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("TASK3B_ARCHIVE_MANIFEST_SHA256=VERIFIED", result.stdout)
        self.assertIn("TASK3B_SOURCE_PUBLIC_TABLE_COUNT=0", result.stdout)
        self.assertIn("TASK3B_ISOLATED_PG18_RESTORE=NOT_EXECUTED_PREFLIGHT_ONLY", result.stdout)
        self.assertIn("TASK3B_PUBLIC_REGISTRATION=HELD", result.stdout)

    def test_explicit_preflight(self):
        self.assertEqual(self.call("--preflight").returncode, 0)

    def test_tampered_ciphertext(self):
        self.archive.write_bytes(self.archive.read_bytes() + b"tamper")
        self.refused(self.call(), "SOURCE_IDENTITY_OR_CIPHERTEXT_MISMATCH")

    def test_wrong_source_project(self):
        self.data["sourceProjectId"] = "other"
        self.save()
        self.refused(self.call(), "SOURCE_IDENTITY_OR_CIPHERTEXT_MISMATCH")

    def test_wrong_pg_major(self):
        self.data["sourcePostgresMajor"] = 17
        self.save()
        self.refused(self.call(), "SOURCE_IDENTITY_OR_CIPHERTEXT_MISMATCH")

    def test_wrong_archive_basename(self):
        self.data["archiveFileName"] = "../other.gpg"
        self.save()
        self.refused(self.call(), "SOURCE_IDENTITY_OR_CIPHERTEXT_MISMATCH")

    def test_bad_json(self):
        self.manifest.write_text('{"incomplete":', encoding="utf-8")
        self.refused(self.call(), "SOURCE_IDENTITY_OR_CIPHERTEXT_MISMATCH")

    def test_symlink_refused(self):
        real = self.archive.with_suffix(".real")
        self.archive.rename(real)
        self.archive.symlink_to(real)
        self.refused(self.call(), "FILES_MISSING_OR_SYMLINK")

    def test_live_database_target_refused(self):
        self.refused(self.call("--restore", "bliss"), "TARGET_NAME_REFUSED")

    def test_no_recovery_host_attestation(self):
        self.refused(self.call("--restore", "alpha_task3b_restore_abcdef"), "RECOVERY_HOST_ATTESTATION_MISSING")

    def test_railway_environment_refused_despite_attestation(self):
        result = self.call("--restore", "alpha_task3b_restore_abcdef",
                           extra_env={"TASK3B_OFFLINE_RECOVERY_HOST_ACK": "OFFLINE_RECOVERY_HOST",
                                      "RAILWAY_PROJECT_ID": "pretend-railway"})
        self.refused(result, "RAILWAY_RUNTIME_CONTEXT_REFUSED")

class HeaderFilterTests(unittest.TestCase):
    def run_filter(self, data):
        return subprocess.run(["python3", str(FILTER)], input=data,
                              capture_output=True, timeout=10)

    def test_plain_pg_dump(self):
        raw = b"PGDMP" + bytes(range(64))
        result = self.run_filter(raw)
        self.assertEqual(0, result.returncode)
        self.assertEqual(raw, result.stdout)

    def test_one_leading_utf8_bom(self):
        raw = b"PGDMP" + bytes(range(64))
        result = self.run_filter(b"\xef\xbb\xbf" + raw)
        self.assertEqual(0, result.returncode)
        self.assertEqual(raw, result.stdout)

    def test_unknown_header_refused(self):
        result = self.run_filter(b"NOT_ARCHIVE" + bytes(range(64)))
        self.assertEqual(42, result.returncode)

    def test_double_bom_refused(self):
        result = self.run_filter(b"\xef\xbb\xbf\\xef\\xbb\\xbfPGDMP")
        self.assertEqual(42, result.returncode)

    def test_empty_input_refused(self):
        self.assertEqual(42, self.run_filter(b"").returncode)

if __name__ == "__main__":
    unittest.main(verbosity=2)
