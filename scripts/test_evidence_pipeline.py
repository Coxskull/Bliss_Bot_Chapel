import importlib.util
import json
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("pipeline", Path(__file__).with_name("evidence_pipeline.py"))
p = importlib.util.module_from_spec(spec); spec.loader.exec_module(p)


class EvidencePipelineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "source"; self.root.mkdir()
        self.downloads = Path(self.temp.name) / "downloads"; self.downloads.mkdir()
        (self.root / "tests.json").write_text('{"passed":1,"failed":0,"skipped":0}')
        self.id = p.generate_evidence_id([])
        self.manifest = p.construct_manifest(self.root, self.id, "AGM-OPS-011", "LOCAL_TEST_FIXTURE",
            {"name":"fixture/repository","branch":"feature/fixture","head_sha":"a"*40},
            {"passed":1,"failed":0,"skipped":0,"test_commands":["python3 local-test"],
             "test_environment":"synthetic local fixture","execution_timestamp":"2026-10-14T00:00:00Z","ci_references":[]},
            ["tests.json"], ["Synthetic test; not actual submission"], [])
        shutil.copy(self.root / "tests.json", self.downloads / "tests.json")
        (self.downloads / "manifest.json").write_bytes(p.canonical(self.manifest))
        self.receipts = [{"path":name,"file_id":"fixture-"+name,"parent_id":"fixture-folder"} for name in ["tests.json","manifest.json"]]
        self.access = {"status":"VERIFIED_EXISTING_ACL_ACCOUNT_BINDING","evidence_reference":"synthetic-test-only"}

    def verify(self, access=None, receipts=None):
        return p.verify_drive_upload(self.root, self.manifest, self.receipts if receipts is None else receipts,
                                     self.downloads, self.access if access is None else access, "fixture-folder")

    def test_secure_uuid_version_and_uniqueness(self):
        ids = [p.generate_evidence_id([]) for _ in range(100)]
        self.assertEqual(100, len(set(ids)))
        for value in ids: self.assertEqual(value, p.valid_id(value))

    def test_collision_is_retried(self):
        with patch.object(p.secrets, "token_bytes", side_effect=[bytes(16), bytes([1])*16]):
            old = "AGM-EV-00000000-0000-4000-8000-000000000000"
            self.assertNotEqual(old, p.generate_evidence_id([old]))

    def test_collision_exhaustion_is_refused(self):
        with patch.object(p.secrets, "token_bytes", return_value=bytes(16)):
            with self.assertRaises(ValueError): p.generate_evidence_id(["AGM-EV-00000000-0000-4000-8000-000000000000"])

    def test_non_v4_identifier_is_refused(self):
        with self.assertRaises(ValueError): p.valid_id("AGM-EV-00000000-0000-1000-8000-000000000000")

    def test_duplicate_json_and_nonfinite_are_refused(self):
        for content in ['{"a":1,"a":2}', '{"a":NaN}']:
            path = self.root / "bad.json"; path.write_text(content)
            with self.assertRaises(ValueError): p.load_json(path)

    def test_path_escape_and_noncanonical_names_are_refused(self):
        for name in ["../tests.json", "/tests.json", "./tests.json", "a//b", "C:foo", "a\\b"]:
            with self.assertRaises(ValueError): p.artifact_path(self.root, name)

    def test_symlink_is_refused(self):
        (self.root / "alias").symlink_to(self.root / "tests.json")
        with self.assertRaises(ValueError): p.artifact_path(self.root, "alias")

    def test_duplicate_and_reserved_artifacts_are_refused(self):
        for paths in [["tests.json","tests.json"],["manifest.json"],["tests.json","TESTS.JSON"]]:
            with self.assertRaises(ValueError): p.collect_test_artifacts(self.root, paths)

    def test_missing_artifact_is_refused(self):
        with self.assertRaises(ValueError): p.collect_test_artifacts(self.root,["missing.log"])

    def test_unknown_and_boolean_counts_are_refused(self):
        for value in [None,True,-1]:
            self.manifest["verification"]["passed"] = value
            with self.assertRaises(ValueError): p.validate_manifest(self.root,self.manifest)

    def test_engineering_acceptance_is_refused(self):
        self.manifest["executive_decision"] = "ACCEPTED"
        with self.assertRaises(ValueError): p.validate_manifest(self.root,self.manifest)

    def test_self_supersession_is_refused(self):
        self.manifest["supersedes_evidence_id"] = self.id
        with self.assertRaises(ValueError): p.validate_manifest(self.root,self.manifest)

    def test_source_changes_are_refused(self):
        (self.root / "tests.json").write_text("changed")
        with self.assertRaises(ValueError): self.verify()

    def test_package_refuses_overwrite_and_contains_exact_manifest(self):
        import zipfile
        target = Path(self.temp.name)/"evidence.zip"
        sha = p.package_evidence(self.root,self.manifest,target)
        self.assertEqual(sha,p.calculate_sha256(target))
        with zipfile.ZipFile(target) as z: self.assertEqual(z.read("manifest.json"),p.canonical(self.manifest))
        with self.assertRaises(FileExistsError): p.package_evidence(self.root,self.manifest,target)

    def test_missing_duplicate_and_extra_receipts_are_refused(self):
        for receipts in [self.receipts[:1],self.receipts+self.receipts[:1],self.receipts+[{"path":"extra","file_id":"x"}]]:
            with self.assertRaises(ValueError): self.verify(receipts=receipts)

    def test_wrong_folder_and_reused_drive_id_are_refused(self):
        for key,value in [("parent_id","wrong"),("file_id",self.receipts[1]["file_id"])]:
            bad=[dict(r) for r in self.receipts];bad[0][key]=value
            with self.assertRaises(ValueError): self.verify(receipts=bad)

    def test_changed_download_is_refused(self):
        (self.downloads / "tests.json").write_text("different")
        with self.assertRaises(ValueError): self.verify()

    def test_unverified_reviewer_access_keeps_submission_incomplete(self):
        verified=self.verify(access={"status":"UNKNOWN"})
        index=p.commit_submission(self.root,self.manifest,verified,1,self.downloads)
        self.assertEqual("SUBMISSION_INCOMPLETE",index["submission_status"])
        with self.assertRaises(ValueError): p.generate_owner_notification(index,self.downloads/"manifest.json",{})

    def test_registry_collision_and_missing_registry_refuse_commit(self):
        for count in [0,2]:
            with self.assertRaises(ValueError): p.commit_submission(self.root,self.manifest,self.verify(),count,self.downloads)

    def test_failed_tests_are_preserved_and_never_ready(self):
        self.manifest["verification"]["failed"]=1
        (self.downloads/"manifest.json").write_bytes(p.canonical(self.manifest))
        index=p.commit_submission(self.root,self.manifest,self.verify(),1,self.downloads)
        self.assertEqual(1,self.manifest["verification"]["failed"])
        self.assertEqual("SUBMISSION_INCOMPLETE",index["submission_status"])

    def test_content_change_after_verification_prevents_commit(self):
        verified=self.verify();(self.downloads/"tests.json").write_text("late change")
        with self.assertRaises(ValueError): p.commit_submission(self.root,self.manifest,verified,1,self.downloads)

    def test_notification_requires_index_readback_and_receipt(self):
        index=p.commit_submission(self.root,self.manifest,self.verify(),1,self.downloads)
        self.assertEqual("READY_FOR_EXECUTIVE_REVIEW",index["submission_status"])
        target=self.downloads/"submission-index.json";target.write_bytes(p.canonical(index))
        with self.assertRaises(ValueError): p.generate_owner_notification(index,target,{})
        note=p.generate_owner_notification(index,target,{"file_id":"fixture-index","parent_id":"fixture-folder"})
        self.assertIn(self.id,note);self.assertFalse(index["engineering_self_approval"])
        self.assertEqual("PENDING_REVIEW",index["executive_decision"])
        target.write_text("changed")
        with self.assertRaises(ValueError): p.generate_owner_notification(index,target,{"file_id":"fixture-index","parent_id":"fixture-folder"})


if __name__ == "__main__":
    import os
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(EvidencePipelineTests)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    path=os.environ.get("AGM_EVIDENCE_TEST_RESULTS")
    if path:
        Path(path).write_text(json.dumps({"passed":result.testsRun-len(result.failures)-len(result.errors)-len(result.skipped),
            "failed":len(result.failures)+len(result.errors),"skipped":len(result.skipped),"total":result.testsRun,
            "failures":[str(t) for t,_ in result.failures+result.errors]}))
    raise SystemExit(not result.wasSuccessful())
