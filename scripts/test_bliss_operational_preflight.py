import importlib.util
import json
import io
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

    def test_loopback_ingress_names_are_accepted_without_external_proxies(self):
        names = list(module.REQUIRED) + [
            "Authentication__ApprovedTestSubjects__0",
            "Runtime__LoopbackIngress__Enabled",
            "Runtime__LoopbackIngress__PublicOrigin",
            "Runtime__LoopbackIngress__PublicPort",
            "Runtime__LoopbackIngress__BackendPort",
        ]
        report = module.inspect(names)
        self.assertEqual(report["inventory_status"], "PRESENT_UNVERIFIED")
        self.assertEqual(report["ingress_policy_name_status"], "LOOPBACK_DECLARED_UNVERIFIED")
        self.assertFalse(report["ingress_transport_verified"])
        self.assertFalse(report["operational_acceptance"])
        self.assertFalse(report["hosted_restore_verified"])

    def test_loopback_without_public_origin_name_blocks(self):
        names = list(module.REQUIRED) + [
            "Authentication__ApprovedTestSubjects__0",
            "Runtime__LoopbackIngress__Enabled",
            "Runtime__LoopbackIngress__PublicPort",
            "Runtime__LoopbackIngress__BackendPort",
        ]
        report = module.inspect(names)
        self.assertEqual(report["inventory_status"], "BLOCKED")
        self.assertIn("Runtime__LoopbackIngress__PublicOrigin",
                      report["missing_required_variable_names"])
        self.assertEqual(report["ingress_policy_name_status"], "LOOPBACK_DECLARED_UNVERIFIED")
        self.assertFalse(report["operational_acceptance"])

    def test_existing_known_proxy_inventory_stays_unverified(self):
        names = list(module.REQUIRED) + [
            "Authentication__ApprovedTestSubjects__0", "Runtime__KnownProxies__0"
        ]
        report = module.inspect(names)
        self.assertEqual(report["ingress_policy_name_status"], "EXPLICIT_PROXIES_DECLARED_UNVERIFIED")
        self.assertEqual(report["inventory_status"], "PRESENT_UNVERIFIED")
        self.assertFalse(report["ingress_transport_verified"])

    def test_mixed_ingress_names_fail_closed(self):
        names = list(module.REQUIRED) + [
            "Authentication__ApprovedTestSubjects__0",
            "Runtime__LoopbackIngress__Enabled",
            "Runtime__KnownProxies__0",
        ]
        report = module.inspect(names)
        self.assertEqual(report["inventory_status"], "BLOCKED")
        self.assertEqual(report["ingress_policy_name_status"], "CONFLICTING_POLICY_NAMES")
        self.assertFalse(report["operational_acceptance"])

    def test_missing_or_malformed_ingress_names_are_blocked(self):
        names = list(module.REQUIRED) + ["Authentication__ApprovedTestSubjects__0"]
        for suffix in (None, "__01", "__bad"):
            with self.subTest(suffix=suffix):
                value = names if suffix is None else names + ["Runtime__KnownProxies" + suffix]
                report = module.inspect(value)
                self.assertEqual(report["inventory_status"], "BLOCKED")
                self.assertEqual(report["ingress_policy_name_status"], "MISSING")
                self.assertIn("Runtime__KnownProxies__0 OR Runtime__LoopbackIngress__Enabled",
                              report["missing_required_variable_names"])

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

    def test_unrelated_repository_pair_cannot_self_attest(self):
        state = {
            "main_repo": "Attacker/Other",
            "configured_repo": "Attacker/Other",
            "main_sha": "a" * 40, "configured_sha": "a" * 40
        }
        with self.assertRaises(ValueError):
            module.inspect([], state)

    def test_strict_json_reader_rejects_duplicates_and_non_json_numbers(self):
        for payload in (
            '{"main_repo":"Coxskull/Bliss_Bot_Chapel","main_repo":"Attacker/Other"}',
            '{"configured_sha":NaN}',
            '{"main_sha":Infinity}',
            '{"configured_sha":-Infinity}',
        ):
            with self.subTest(payload=payload[:20]):
                with self.assertRaises(ValueError):
                    module.read_strict_json(io.StringIO(payload))

    def test_strict_json_reader_accepts_a_valid_source_object(self):
        text = json.dumps({
            "main_repo": "Coxskull/Bliss_Bot_Chapel",
            "configured_repo": "Coxskull/Bliss_Bot_Chapel",
            "main_sha": "a" * 40,
            "configured_sha": "a" * 40,
        })
        state = module.read_strict_json(io.StringIO(text))
        self.assertEqual(module.inspect([], state)["configured_source_parity"], "CONFIG_MATCH")

    def test_source_absence_does_not_mean_parity(self):
        self.assertEqual(module.inspect([])["configured_source_parity"], "NOT_PROVIDED")


    def test_preview_flag_is_not_operational_authorization(self):
        report = module.inspect(["Runtime__RestrictedPreview"])
        self.assertFalse(report["operational_acceptance"])
        for name in ("hosted_login_test", "role_isolation_test", "application_database_test", "frontend_api_test"):
            self.assertEqual(report[name], "NOT_TESTED")


if __name__ == "__main__":
    unittest.main()
