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

    def test_preview_flag_is_not_operational_authorization(self):
        report = module.inspect(["Runtime__RestrictedPreview"])
        self.assertFalse(report["operational_acceptance"])
        for name in ("hosted_login_test", "role_isolation_test", "application_database_test", "frontend_api_test"):
            self.assertEqual(report[name], "NOT_TESTED")


if __name__ == "__main__":
    unittest.main()
