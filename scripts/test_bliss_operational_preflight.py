import importlib.util
import json
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("preflight", Path(__file__).with_name("bliss-operational-preflight.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class InventoryTests(unittest.TestCase):
    def test_empty_inventory_blocks(self):
        self.assertEqual(module.inspect([])["inventory_status"], "BLOCKED")

    def test_presence_never_claims_operational_or_value_validation(self):
        report = module.inspect(list(module.REQUIRED) + [
            "Authentication__ApprovedTestSubjects__0", "Runtime__KnownProxies__0"])
        self.assertEqual(report["inventory_status"], "PRESENT_UNVERIFIED")
        self.assertFalse(report["operational_acceptance"])
        self.assertFalse(report["configuration_values_validated"])

    def test_invalid_index_does_not_satisfy_subject_requirement(self):
        for suffix in ("", "__", "__email", "__-1", "__01"):
            with self.subTest(suffix=suffix):
                report = module.inspect(["Authentication__ApprovedTestSubjects" + suffix])
                self.assertIn("Authentication__ApprovedTestSubjects__0", report["missing_required_variable_names"])

    def test_objects_with_values_are_rejected(self):
        with self.assertRaises(ValueError):
            module.inspect({"Authentication__ClientSecret": "DO_NOT_EXPORT"})

    def test_unrecognized_input_strings_are_never_echoed(self):
        report = json.dumps(module.inspect(["DO_NOT_EXPORT"]), sort_keys=True)
        self.assertNotIn("DO_NOT_EXPORT", report)

    def test_matching_configured_source_does_not_grant_release_acceptance(self):
        state = {
            "main_repo": "Coxskull/Bliss_Bot_Chapel",
            "configured_repo": "coxskull/bliss_bot_chapel",
            "main_sha": "a" * 40, "configured_sha": "A" * 40,
        }
        report = module.inspect(list(module.REQUIRED) + [
            "Authentication__ApprovedTestSubjects__0", "Runtime__KnownProxies__0"
        ], state)
        self.assertEqual(report["configured_source_parity"], "CONFIG_MATCH")
        self.assertEqual(report["inventory_status"], "PRESENT_UNVERIFIED")
        self.assertFalse(report["hosted_backup_schedule_verified"])
        self.assertFalse(report["hosted_restore_verified"])
        self.assertFalse(report["operational_acceptance"])

    def test_configured_sha_drift_is_explicit(self):
        state = {
            "main_repo": "Coxskull/Bliss_Bot_Chapel",
            "configured_repo": "Coxskull/Bliss_Bot_Chapel",
            "main_sha": "a" * 40, "configured_sha": "b" * 40,
        }
        self.assertEqual(module.inspect([], state)["configured_source_parity"], "CONFIG_DRIFT")

    def test_configured_repo_drift_is_explicit(self):
        state = {
            "main_repo": "Coxskull/Bliss_Bot_Chapel",
            "configured_repo": "SomeOrg/OtherRepo",
            "main_sha": "a" * 40, "configured_sha": "a" * 40,
        }
        self.assertEqual(module.inspect([], state)["configured_source_parity"], "CONFIG_DRIFT")

    def test_invalid_source_payload_is_rejected_without_echo(self):
        valid = {
            "main_repo": "Coxskull/Bliss_Bot_Chapel",
            "configured_repo": "Coxskull/Bliss_Bot_Chapel",
            "main_sha": "a" * 40, "configured_sha": "a" * 40,
        }
        for bad in (
            {"main_repo": "not-a-repo", **{k: v for k, v in valid.items() if k != "main_repo"}},
            {**valid, "configured_sha": "DO_NOT_EXPORT"},
            {**valid, "ConnectionStrings__DefaultConnection": "DO_NOT_EXPORT"},
            ["DO_NOT_EXPORT"],
        ):
            with self.subTest(bad_type=type(bad).__name__):
                with self.assertRaises(ValueError):
                    module.inspect([], bad)

    def test_source_absence_does_not_mean_parity(self):
        self.assertEqual(module.inspect([])["configured_source_parity"], "NOT_PROVIDED")


    def test_preview_flag_is_not_operational_authorization(self):
        report = module.inspect(["Runtime__RestrictedPreview"])
        self.assertFalse(report["operational_acceptance"])
        for name in ("hosted_login_test", "role_isolation_test", "application_database_test", "frontend_api_test"):
            self.assertEqual(report[name], "NOT_TESTED")


if __name__ == "__main__":
    unittest.main()
