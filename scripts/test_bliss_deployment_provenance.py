import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location(
    "provenance", Path(__file__).with_name("bliss-deployment-provenance.py"))
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
A = "a" * 40
B = "b" * 40
C = "c" * 40


def sample(**updates):
    value = {
        "main_repository": "Coxskull/Bliss_Bot_Chapel",
        "main_sha": A,
        "configured_repository": "Coxskull/Bliss_Bot_Chapel",
        "configured_branch": "main",
        "configured_sha": A,
        "deployment_metadata_sha": A,
        "deployment_status": "SUCCESS",
    }
    value.update(updates)
    return value


class ProvenanceTests(unittest.TestCase):
    def test_matching_metadata_not_release(self):
        r = mod.inspect(sample())
        self.assertEqual(r["status"], "MATCH_UNATTESTED")
        self.assertTrue(all(v == "PASS" for v in r["checks"].values()))
        for key in ("release_authorized", "running_image_verified",
                    "merged_fixes_verified", "ingress_verified", "backup_verified"):
            self.assertFalse(r[key])

    def test_three_way_mismatch(self):
        r = mod.inspect(sample(configured_sha=B, deployment_metadata_sha=C))
        self.assertEqual(r["status"], "FAIL_DRIFT")
        self.assertEqual(r["checks"]["configured_vs_main"], "FAIL")
        self.assertEqual(r["checks"]["deployment_vs_main"], "FAIL")
        self.assertEqual(r["checks"]["configured_vs_deployment"], "FAIL")

    def test_deployment_sha_is_independent_of_configured_sha(self):
        r = mod.inspect(sample(deployment_metadata_sha=B))
        self.assertEqual(r["checks"]["configured_vs_main"], "PASS")
        self.assertEqual(r["checks"]["deployment_vs_main"], "FAIL")

    def test_configured_sha_is_independent_of_deployment_sha(self):
        r = mod.inspect(sample(configured_sha=B))
        self.assertEqual(r["checks"]["deployment_vs_main"], "PASS")
        self.assertEqual(r["checks"]["configured_vs_main"], "FAIL")

    def test_missing_deployment_data_blocks(self):
        r = mod.inspect(sample(deployment_metadata_sha=None, deployment_status=None))
        self.assertEqual(r["status"], "BLOCKED_INCOMPLETE")

    def test_missing_config_blocks(self):
        self.assertEqual(mod.inspect(sample(configured_sha=None))["status"],
                         "BLOCKED_INCOMPLETE")

    def test_wrong_repository_fails(self):
        r = mod.inspect(sample(configured_repository="Elsewhere/Other"))
        self.assertEqual(r["checks"]["repository"], "FAIL")

    def test_identity_case_normalization(self):
        r = mod.inspect(sample(main_sha=A.upper(),
                               configured_repository="coxskull/bliss_bot_chapel"))
        self.assertEqual(r["status"], "MATCH_UNATTESTED")

    def test_unsupported_branch_rejected(self):
        with self.assertRaises(ValueError):
            mod.inspect(sample(configured_branch="preview"))

    def test_status_and_metadata_must_agree(self):
        with self.assertRaises(ValueError):
            mod.inspect(sample(deployment_metadata_sha=None))
        with self.assertRaises(ValueError):
            mod.inspect(sample(deployment_status=None))

    def test_reject_unknown_fields(self):
        with self.assertRaises(ValueError):
            mod.inspect(sample(unexpected="DONT_ECHO"))
        with self.assertRaises(ValueError):
            mod.inspect({"unexpected": "DONT_ECHO"})

    def test_reject_malformed_sha(self):
        for val in ("bad", "", "a" * 39, 1, []):
            with self.subTest(val_type=type(val).__name__):
                with self.assertRaises(ValueError):
                    mod.inspect(sample(configured_sha=val))

    def test_no_identifiers_echoed(self):
        import json
        r = json.dumps(mod.inspect(sample(configured_sha=B, deployment_metadata_sha=C)))
        for val in (A, B, C, "Coxskull/Bliss_Bot_Chapel"):
            self.assertNotIn(val, r)


if __name__ == "__main__":
    unittest.main()
