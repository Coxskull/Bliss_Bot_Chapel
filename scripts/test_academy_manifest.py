"""Negative controls proving the Academy catalog gate detects drift."""
import csv
import runpy
import tempfile
import unittest
from pathlib import Path


GATE = runpy.run_path(str(Path(__file__).with_name("verify-academy-manifest.py")))
verify = GATE["verify_catalog"]
COLUMNS = GATE["REQUIRED_COLUMNS"]
PNG_HEADER = b"\x89PNG\r\n\x1a\n" + b"synthetic pixel fixture"


class AcademyManifestGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.catalog = self.root / "assets" / "alpha-prototypes" / "creative-academy"
        self.inbox = self.catalog / "inbox"
        self.inbox.mkdir(parents=True)
        self.rows = []
        for number in range(1, 51):
            filename = f"synthetic-{number:03d}.png"
            (self.inbox / filename).write_bytes(PNG_HEADER)
            self.rows.append({
                "referenceId": f"ACA-{number:03d}-V1",
                "nicheNumber": str(number),
                "nicheKey": f"synthetic-niche-{number}",
                "nicheName": f"TEST Niche {number}",
                "expectedFile": filename,
                "lifecycle": "ACTIVE",
                "status": "UPLOADED",
                "learn": "Only lighting principles",
                "doNotCopy": "The fictional pictured brand and composition",
                "mappingNote": "Test-only owner approval metadata",
                "provenanceSource": "owner-upload",
                "ownership": "owner",
                "approvedOn": "2026-10-07",
                "approvingAuthority": "owner",
                "permittedInternalUse": "Academy quality reference",
            })
        self.save()

    def save(self):
        with (self.catalog / "MANIFEST.tsv").open("w", encoding="utf-8", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=COLUMNS, delimiter="\t")
            writer.writeheader()
            writer.writerows(self.rows)

    def test_exactly_fifty_approved_unique_images_pass(self):
        self.assertEqual(
            {"approved_references": 50, "mapped_local_images": 50},
            verify(self.root),
        )

    def test_incomplete_inventory_refused(self):
        self.rows.pop()
        self.save()
        with self.assertRaisesRegex(ValueError, "Expected 50"):
            verify(self.root)

    def test_missing_image_and_bad_signature_refused(self):
        image = self.inbox / self.rows[10]["expectedFile"]
        image.unlink()
        with self.assertRaisesRegex(ValueError, "missing"):
            verify(self.root)
        image.write_bytes(b"not an image")
        with self.assertRaisesRegex(ValueError, "signature"):
            verify(self.root)

    def test_duplicate_image_mapping_and_path_traversal_refused(self):
        self.rows[1]["expectedFile"] = self.rows[0]["expectedFile"]
        self.save()
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            verify(self.root)
        self.rows[1]["expectedFile"] = "../escape.png"
        self.save()
        with self.assertRaisesRegex(ValueError, "unsafe image path"):
            verify(self.root)

    def test_approval_downgrade_missing_rights_and_wrong_order_refused(self):
        for field, value, message in [
            ("lifecycle", "CANDIDATE", "no longer ACTIVE"),
            ("status", "AWAITING_UPLOAD", "no longer ACTIVE"),
            ("approvingAuthority", "", "missing required"),
            ("permittedInternalUse", "Ad generation", "non-reference use"),
            ("nicheNumber", "2", "reference/order mismatch"),
        ]:
            with self.subTest(field=field):
                original = self.rows[0][field]
                self.rows[0][field] = value
                self.save()
                with self.assertRaisesRegex(ValueError, message):
                    verify(self.root)
                self.rows[0][field] = original


if __name__ == "__main__":
    unittest.main()
