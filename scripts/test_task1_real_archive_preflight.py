"""ALPHA Task #1: refusal-only tests for the REAL archive metadata preflight.

No PostgreSQL server, real backup, decryption, network, credentials, or restore.
"""
from __future__ import annotations

import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().with_name("task1-railway-real-archive-preflight.sh")
SOURCE_REF = "yyesqcyjhqjryaskrqip"  # Public project reference only, not credentials.


class Task1ArchiveRefusalTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="alpha-task1-guard-")
        self.addCleanup(self.tmp.cleanup)
        root = Path(self.tmp.name)
        self.archive = root / "fixture-DO-NOT-RESTORE.bin"
        self.archive.write_bytes(b"NOT A REAL POSTGRES BACKUP" * 8)
        self.archive.chmod(0o600)
        self.expected = hashlib.sha256(self.archive.read_bytes()).hexdigest()
        self.fake_bin = root / "bin"
        self.fake_bin.mkdir()
        self.called = root / "pg_restore_was_called"
        fake_pg_restore = self.fake_bin / "pg_restore"
        # This executable must NEVER be reached in the failure tests.
        fake_pg_restore.write_text(
            "#!/bin/sh\nprintf called > \"" + str(self.called) + "\"\nexit 99\n",
            encoding="utf8",
        )
        fake_pg_restore.chmod(0o700)

    def invoke(self, overrides=None, args=None):
        env = os.environ.copy()
        env.update(
            TASK1_REAL_ARCHIVE_INSPECTION_APPROVED="yes",
            TASK1_REAL_SOURCE_PROJECT_REF=SOURCE_REF,
            TASK1_REAL_ARCHIVE_SHA256_EXPECTED=self.expected,
            PATH=str(self.fake_bin) + os.pathsep + env.get("PATH", ""),
        )
        if overrides:
            env.update(overrides)
        if args is None:
            args = ["--archive", str(self.archive), "--local-only"]
        proc = subprocess.run(
            ["bash", str(SCRIPT), *args],
            env=env, capture_output=True, text=True, check=False, timeout=10,
        )
        self.assertEqual(proc.returncode, 2, proc.stdout + proc.stderr)
        self.assertIn("TASK1_PREFLIGHT_BLOCKED", proc.stderr)
        self.assertNotIn("TASK1_READONLY_REAL_ARCHIVE_PREFLIGHT_PASS", proc.stdout)
        self.assertFalse(self.called.exists(), "pg_restore must not execute")
        self.assertNotIn(str(self.archive), proc.stdout + proc.stderr, "private archive path leaked")
        return proc

    def test_missing_explicit_authorization_refused(self):
        self.assertIn(
            "separate authorization required",
            self.invoke({"TASK1_REAL_ARCHIVE_INSPECTION_APPROVED": "no"}).stderr,
        )

    def test_source_identity_required(self):
        self.assertIn("source Supabase project ref",
                      self.invoke({"TASK1_REAL_SOURCE_PROJECT_REF": ""}).stderr)

    def test_rejects_incorrect_or_missing_manifest_digest(self):
        for digest in ("", "0" * 64, "unsafe-sha-value"):
            with self.subTest(digest=digest):
                self.invoke({"TASK1_REAL_ARCHIVE_SHA256_EXPECTED": digest})

    def test_owner_only_plaintext_permissions(self):
        self.archive.chmod(0o644)
        self.assertIn("group/other access", self.invoke().stderr)

    def test_missing_archive_refused(self):
        self.invoke(args=["--archive", str(self.archive) + ".missing", "--local-only"])

    def test_non_pg_archive_refused_even_with_matching_hash(self):
        self.assertIn("not a PostgreSQL custom archive", self.invoke().stderr)

    def test_extra_arguments_refused(self):
        self.invoke(args=["--archive", str(self.archive), "--local-only", "--restore"])

    def test_no_hidden_network_or_restore_executor(self):
        source = SCRIPT.read_text(encoding="utf8")
        for forbidden in ("psql ", "createdb ", "dropdb ", "pg_restore --dbname", "curl ", "wget ", "eval "):
            self.assertNotIn(forbidden, source, f"Restore/network behavior appeared: {forbidden}")


if __name__ == "__main__":
    unittest.main()
