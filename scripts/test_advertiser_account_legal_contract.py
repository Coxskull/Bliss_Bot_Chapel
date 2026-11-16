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

    def test_account_privacy_does_not_claim_inquiry_only_privacy_form_handles_accounts(self):
        # The public interest-form privacy endpoint is not an account data-subject request route.
        self.assertNotIn('href="/privacy-request.html"', self.privacy)
        self.assertIn("limited to public-interest submissions", self.privacy)
        self.assertIn("not</strong> a verified account-registration privacy request channel", self.privacy)

    def test_account_rights_preview_is_separate_from_public_inquiry_and_not_live(self):
        rights = (PUBLIC / "advertiser-account-rights.html").read_text(encoding="utf-8")
        js = (PUBLIC / "advertiser-account-rights.js").read_text(encoding="utf-8")
        self.assertIn('href="/advertiser-account-rights.html"', self.form)
        self.assertIn('name="robots" content="noindex,nofollow"', rights)
        self.assertIn("Not available for live account requests yet.", rights)
        self.assertIn('id="account-right-submit"', rights)
        self.assertIn("disabled", rights)
        self.assertNotIn('data-endpoint="https://bkutbglzivfdnoerigyb.', rights)
        self.assertIn('"/api/advertisers/privacy-requests"', js)
        self.assertIn("advertiserPrivacyRequestsEnabled === true", js)
        self.assertIn("session?.csrfToken", js)
        self.assertIn('"X-CSRF-TOKEN"', js)
        self.assertIn("not a completed deletion", js)

    def test_accepted_privacy_case_receipt_survives_history_refresh_failure(self):
        # The mutation response is durable even if the separate history GET
        # fails. Avoid telling users to retry a successful privacy request.
        js = (PUBLIC / "advertiser-account-rights.js").read_text(encoding="utf-8")
        receipt = js.index("const receiptMessage = `Request ")
        refresh = js.index("await refreshHistory();", receipt)
        rotate = js.index("requestKey = crypto.randomUUID();", receipt)
        self.assertLess(receipt, rotate)
        self.assertLess(rotate, refresh)
        self.assertRegex(
            js[receipt:],
            r"try\\s*\\{\\s*await refreshHistory\\(\\);\\s*\\}"
            r"\\s*catch \\(_\\) \\{\\s*status\\.textContent = receiptMessage \\+"
        )
        self.assertIn("your request was accepted. Do not resubmit it.", js[refresh:])
        self.assertIn("status.textContent = receiptMessage;", js[receipt:refresh])

    def test_registration_and_account_case_csrf_headers_match_server(self):
        program = (ROOT / "Bliss.Api" / "Program.cs").read_text(encoding="utf-8")
        options = (ROOT / "Bliss.Api" / "Security" / "BlissAuthenticationOptions.cs").read_text(encoding="utf-8")
        registration = self.js
        case_js = (PUBLIC / "advertiser-account-rights.js").read_text(encoding="utf-8")
        self.assertIn('options.HeaderName = "X-CSRF-TOKEN";', program)
        self.assertIn('"X-CSRF-TOKEN": session.csrfToken', registration)
        self.assertIn('"X-CSRF-TOKEN": session.csrfToken', case_js)
        self.assertIn("AdvertiserPrivacyRequestsRequested", options)
        self.assertIn("advertiserPrivacyRequestsEnabled", program)
        self.assertNotIn("RequestVerificationToken", registration)
        self.assertNotIn("RequestVerificationToken", case_js)

    def test_approved_advertiser_case_access_is_exact_and_default_off(self):
        gate = (ROOT / "Bliss.Api" / "Security" / "AdvertiserApiBoundary.cs").read_text(encoding="utf-8")
        pending = (ROOT / "Bliss.Api" / "Security" / "AdvertiserRegistrationBoundary.cs").read_text(encoding="utf-8")
        self.assertIn("auth.AdvertiserPrivacyRequestsRequested", gate)
        self.assertIn('path.Equals("/api/advertisers/privacy-requests"', gate)
        self.assertIn("options.AdvertiserPrivacyRequestsRequested", pending)
        self.assertIn('path.Equals("/api/advertisers/privacy-requests"', pending)

    def test_account_request_backend_is_scoped_and_does_not_claim_auto_erasure(self):
        boundary = (ROOT / "Bliss.Api" / "Security" / "AdvertiserRegistrationBoundary.cs").read_text(encoding="utf-8")
        controller = (ROOT / "Bliss.Api" / "Controllers" / "AdvertiserPrivacyRequestsController.cs").read_text(encoding="utf-8")
        migration = (ROOT / "Bliss.Infrastructure" / "Migrations" / "20261115110000_AdvertiserPrivacyRequestIntake.cs").read_text(encoding="utf-8")
        self.assertIn('path.Equals("/api/advertisers/privacy-requests"', boundary)
        self.assertIn("IsPendingAdvertiserRegistration", controller)
        self.assertIn("TryResolveVerifiedAdvertiserId", controller)
        self.assertIn("RequestKey", controller)
        self.assertIn("RECEIVED", controller)
        self.assertIn("REVOKE ALL ON TABLE", migration)
        self.assertNotIn('HttpDelete', controller)
        self.assertNotIn('MarketingOptIn = false', controller)

    def test_registration_writes_use_existing_authenticated_rate_limit(self):
        # Signup creates three relational records. A valid OIDC+CSRF session
        # should still be throttled before an attacker can flood the database.
        program = (ROOT / "Bliss.Api" / "Program.cs").read_text(encoding="utf-8")
        runtime = (ROOT / "Bliss.Api" / "Runtime" / "BlissRuntimeOptions.cs").read_text(encoding="utf-8")
        self.assertRegex(self.server,
                         r'\[HttpPost\("register"\)\]\s*\[EnableRateLimiting\(BlissRateLimitPolicies\.Writes\)\]')
        self.assertIn("using Microsoft.AspNetCore.RateLimiting;", self.server)
        self.assertIn("using Bliss.Api.Runtime;", self.server)
        self.assertIn("options.AddPolicy(", program)
        self.assertIn("BlissRateLimitPolicies.Writes", program)
        self.assertIn("context.User.FindFirst(\"sub\")?.Value", program)
        self.assertIn("QueueLimit = 0", program)
        self.assertIn("app.UseRateLimiter();", program)
        self.assertIn('public const string Writes = "writes"', runtime)

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
