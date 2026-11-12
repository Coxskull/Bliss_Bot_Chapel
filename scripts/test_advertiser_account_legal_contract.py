"""Task #3 fail-closed contract between advertiser account registration and legal notices.

Runs on disposable CI / source-only: no login, email, external host, or database writes.
This verifies wiring and identifier parity, NOT owner/legal approval.
"""
from html.parser import HTMLParser
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
PUBLIC = ROOT / "frontend" / "public"
CONTROLLER = ROOT / "Bliss.Api" / "Controllers" / "AdvertiserRegistrationController.cs"


class Links(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.hrefs = []
        self.inputs = []

    def handle_starttag(self, tag, attrs):
        attributes = dict(attrs)
        if tag == "a":
            self.hrefs.append(attributes.get("href", ""))
        if tag == "input":
            self.inputs.append(attributes)


class AdvertiserAccountNoticeContractTests(unittest.TestCase):
    def setUp(self):
        self.form = (PUBLIC / "advertiser-registration.html").read_text(encoding="utf-8")
        self.js = (PUBLIC / "advertiser-registration.js").read_text(encoding="utf-8")
        self.server = CONTROLLER.read_text(encoding="utf-8")
        self.privacy = (PUBLIC / "advertiser-account-privacy.html").read_text(encoding="utf-8")
        self.terms = (PUBLIC / "advertiser-account-terms.html").read_text(encoding="utf-8")
        self.parsed = Links()
        self.parsed.feed(self.form)

    def test_signup_and_business_consent_use_account_not_public_interest_terms(self):
        privacy = "/advertiser-account-privacy.html"
        terms = "/advertiser-account-terms.html"
        self.assertGreaterEqual(self.parsed.hrefs.count(privacy), 2)
        self.assertGreaterEqual(self.parsed.hrefs.count(terms), 2)
        self.assertNotIn("/alpha-privacy.html", self.parsed.hrefs)
        self.assertNotIn("/alpha-terms.html", self.parsed.hrefs)

    def test_server_browser_and_notice_versions_match_exactly(self):
        for name, javascript, label, page in [
            ("PrivacyNoticeVersion", "privacyVersion", "advertiser-privacy-", self.privacy),
            ("TermsVersion", "termsVersion", "advertiser-terms-", self.terms),
        ]:
            server = re.search(rf"public const string {name} = \"([^\"]+)\"", self.server)
            browser = re.search(rf"const {javascript} = \"([^\"]+)\"", self.js)
            self.assertIsNotNone(server, name)
            self.assertIsNotNone(browser, javascript)
            value = server.group(1)
            self.assertTrue(value.startswith(label))
            self.assertEqual(value, browser.group(1))
            self.assertIn(f"<code>{value}</code>", self.form)
            self.assertIn(f"<code>{value}</code>", page)

    def test_draft_pages_cannot_be_mistaken_for_approved_live_terms(self):
        for page in (self.privacy, self.terms):
            self.assertIn('name="robots" content="noindex,nofollow"', page)
            self.assertIn("Not yet approved for live registration.", page)
            self.assertIn("LEGAL REVIEW DRAFT", page)
        self.assertIn("legally approved", self.form)
        self.assertIn("Registration is disabled", self.form)

    def test_marketing_is_optional_and_not_preselected(self):
        marketing = [x for x in self.parsed.inputs if x.get("id") == "marketing-consent"]
        self.assertEqual(1, len(marketing))
        self.assertEqual("checkbox", marketing[0].get("type"))
        self.assertNotIn("checked", marketing[0])
        self.assertNotIn("required", marketing[0])
        self.assertIn("marketingOptIn", self.js)
        self.assertIn("MarketingOptIn", self.server)

    def test_required_processing_and_terms_separate_from_marketing(self):
        inputs = {x.get("id"): x for x in self.parsed.inputs}
        for identifier in ("privacy-consent", "terms-consent", "signup-privacy"):
            self.assertEqual("checkbox", inputs[identifier].get("type"))
            self.assertIn("required", inputs[identifier])
        self.assertNotEqual(inputs["privacy-consent"].get("id"), inputs["marketing-consent"].get("id"))


if __name__ == "__main__":
    unittest.main()
