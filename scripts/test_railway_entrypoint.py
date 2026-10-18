import runpy
from pathlib import Path
import unittest

verify = runpy.run_path(str(Path(__file__).with_name('verify-railway-entrypoint.py')))['verify_refusal']

class RailwayEntrypointTests(unittest.TestCase):
    def test_nonroot_launch_and_auth_refusal_pass(self):
        verify(1654, 134, 'Bliss persistent session storage prepared; launching as UID 1654.\nOIDC authentication must be enabled outside Development.')

    def test_root_missing_refusal_or_started_application_are_rejected(self):
        for uid, code, logs in [(0, 134, 'launching as UID 0.'), (1654, 0, 'launching as UID 1654.'),
                                (1654, 134, 'launching as UID 1654.\nOIDC authentication must be enabled outside Development.\nNow listening on: http://[::]:8080')]:
            with self.subTest(uid=uid, code=code), self.assertRaises(ValueError):
                verify(uid, code, logs)
