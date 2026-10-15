"""Regression checks for container evidence false-positive and path boundaries."""
import copy
import hashlib
import runpy
import tempfile
import unittest
from pathlib import Path

gate = runpy.run_path(str(Path(__file__).with_name("verify-container-runtime.py")))
REVISION = "a" * 40


class ContainerEvidenceTests(unittest.TestCase):
    def test_metadata_rejects_root_wrong_revision_and_nonproduction_defaults(self):
        valid = {"Config": {"User": "1654", "Env": ["ASPNETCORE_ENVIRONMENT=Production", "ASPNETCORE_URLS=http://+:8080"],
                           "Labels": {"org.opencontainers.image.revision": REVISION},
                           "Entrypoint": ["dotnet", "Bliss.Api.dll"], "ExposedPorts": {"8080/tcp": {}}}}
        self.assertEqual(1654, gate["verify_metadata"](valid, REVISION))
        for field, value in [("User", "0"), ("User", "root"), ("User", "app"), ("User", ""),
                             ("Env", ["ASPNETCORE_ENVIRONMENT=Development"]), ("Labels", {}),
                             ("Entrypoint", ["sh"]), ("ExposedPorts", {})]:
            invalid = copy.deepcopy(valid)
            invalid["Config"][field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                gate["verify_metadata"](invalid, REVISION)

    def test_nonzero_exit_alone_is_not_authentication_refusal(self):
        message = "OIDC authentication must be enabled outside Development."
        gate["verify_refusal"](134, message)
        for code, logs in [(0, message), (137, "Killed"), (1, "Database unavailable"),
                           (134, message + " Now listening on: http://[::]:8080"),
                           (134, message + " Application started.")]:
            with self.subTest(code=code, logs=logs), self.assertRaises(ValueError):
                gate["verify_refusal"](code, logs)

    def test_assets_hash_actual_bytes_and_require_both_entry_resources(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "app.js").write_bytes(b"synthetic js")
            (root / "app.css").write_bytes(b"synthetic css")
            valid = '<script src="/operations/financial-review/app.js"></script><link rel="stylesheet" href="/operations/financial-review/app.css">'
            (root / "index.html").write_text(valid)
            report = gate["verify_assets"](root)
            self.assertEqual(hashlib.sha256(b"synthetic js").hexdigest(), report["app.js"])
            (root / "app.js").write_bytes(b"changed")
            self.assertNotEqual(report["app.js"], gate["verify_assets"](root)["app.js"])
            for path in ["https://outside.invalid/app.js", "//outside.invalid/app.js", "/operations/financial-review/../app.js",
                         "/operations/financial-review/%2e%2e/app.js", "/operations/financial-review//app.js",
                         "/operations/financial-review/app.js?key=value", "/operations/financial-review/missing.js"]:
                (root / "index.html").write_text(valid.replace("/operations/financial-review/app.js", path))
                with self.subTest(path=path), self.assertRaises(ValueError):
                    gate["verify_assets"](root)
            (root / "index.html").write_text('<script src="/operations/financial-review/app.js"></script>')
            with self.assertRaises(ValueError):
                gate["verify_assets"](root)

    def test_symlink_asset_cannot_be_reported_as_packaged_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "app.js").symlink_to(__file__)
            (root / "app.css").write_text("css")
            (root / "index.html").write_text('<script src="/operations/financial-review/app.js"></script><link rel="stylesheet" href="/operations/financial-review/app.css">')
            with self.assertRaises(ValueError):
                gate["verify_assets"](root)

    def test_media_probe_requires_actual_expected_codec_and_geometry(self):
        valid = {"streams": [{"codec_name": "h264", "width": 160, "height": 90}]}
        gate["verify_media_probe"](valid)
        for invalid in [{}, {"streams": []}, {"streams": valid["streams"] * 2},
                        {"streams": [{"codec_name": "mpeg4", "width": 160, "height": 90}]},
                        {"streams": [{"codec_name": "h264", "width": 1920, "height": 1080}]}]:
            with self.subTest(probe=invalid), self.assertRaises(ValueError):
                gate["verify_media_probe"](invalid)


if __name__ == "__main__":
    unittest.main()
