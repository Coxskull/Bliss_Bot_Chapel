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

    def test_effective_account_notices_are_published_with_runtime_gated_signup(self):
        for page in (self.privacy, self.terms):
            self.assertIn('name="robots" content="index,follow"', page)
            self.assertIn("Effective November 20, 2026", page)
            self.assertNotIn("LEGAL REVIEW DRAFT", page)
            self.assertNotIn("Not yet approved for live registration.", page)
        self.assertIn("The application checks its current registration setting before enabling this form.", self.form)
        self.assertIn("New accounts remain PENDING_REVIEW", self.form)
        self.assertIn("does not approve a workspace", self.form)
        self.assertIn('configuration?.advertiserRegistrationEnabled === true', self.js)
        self.assertIn("no charge simply to submit", self.terms.lower())
        self.assertIn("PENDING_REVIEW", self.terms)

    def test_account_privacy_does_not_claim_inquiry_only_privacy_form_handles_accounts(self):
        # Public-interest case intake is NEVER a verified account-rights endpoint.
        self.assertNotIn('href="/privacy-request.html"', self.privacy)
        self.assertIn("public-interest privacy form is not an authenticated account-rights channel", self.privacy)
        self.assertIn("do not claim to have an automated account-deletion schedule", self.privacy)
        self.assertIn("mailto:alpha7auto@gmail.com", self.privacy)

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
            r"try\s*\{\s*await refreshHistory\(\);\s*\}"
            r"\s*catch \(_\) \{\s*status\.textContent = receiptMessage \+"
        )
        self.assertIn("your request was accepted. Do not resubmit it.", js[refresh:])
        self.assertIn("status.textContent = receiptMessage;", js[receipt:refresh])

    def test_privacy_request_conflict_rotates_key_only_after_definitive_409(self):
        # The old UI told applicants to refresh after HTTP 409, but reload
        # restored the same sessionStorage key and reproduced the conflict.
        # Network/unknown failures MUST keep the key for safe idempotent retry.
        js = (PUBLIC / "advertiser-account-rights.js").read_text(encoding="utf-8")
        conflict = re.search(
            r"if \(res\.status === 409\) \{(?P<body>.*?)\n      \}", js, re.S
        )
        self.assertIsNotNone(conflict)
        body = conflict.group("body")
        self.assertIn("requestKey = crypto.randomUUID();", body)
        self.assertIn("sessionStorage.setItem(keyName, requestKey);", body)
        self.assertIn("No new case was created.", body)
        self.assertNotIn("fetch(", body)
        # A rejected key is not silently resubmitted or changed on 5xx/timeout.
        self.assertIn(
            'if (!res.ok) throw new Error("The account request was not accepted.',
            js[conflict.end():]
        )
        self.assertNotIn("Refresh before retrying", js)
        self.assertIn("if (!res.ok)", js)
        self.assertIn("Request not verified; please retry with the same request key.", js)

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
        self.assertIn("IsPendingAdvertiserIdentity", controller)
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

    def test_registration_db_failures_are_not_misreported_as_duplicate_conflicts(self):
        # Only unique-key races are mapped to replay/409; all other
        # database failures bubble to the server's normal error path.
        self.assertIn("using Npgsql;", self.server)
        self.assertIn("catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg", self.server)
        self.assertIn("pg.SqlState == PostgresErrorCodes.UniqueViolation", self.server)
        self.assertNotIn("catch (DbUpdateException)\n", self.server)

    def test_registration_forms_fail_closed_until_client_auth_readback(self):
        self.assertIn('<button class="submit" type="submit" disabled>Create sign-in</button>', self.form)
        self.assertIn('<button class="submit" type="submit" disabled>Submit registration</button>', self.form)
        self.assertIn('signup.querySelector("button").disabled = !enabled', self.js)
        self.assertIn('registration.querySelector("button").disabled = !enabled || session?.isAuthenticated !== true', self.js)

    def test_existing_account_password_recovery_is_fail_closed(self):
        signin = (PUBLIC / "oauth-consent.html").read_text(encoding="utf-8")
        login = (PUBLIC / "advertiser-login.html").read_text(encoding="utf-8")
        self.assertIn('href="/advertiser-registration.html#account-recovery"', signin)
        self.assertIn('href="/advertiser-registration.html#account-recovery"', login)
        self.assertIn('id="account-recovery"', self.form)
        self.assertIn('id="recovery-request-form"', self.form)
        self.assertIn('id="recovery-update-form" hidden', self.form)
        self.assertIn('<style>[hidden]{display:none!important}</style>', self.form)
        self.assertIn('Send password-reset link</button>', self.form)
        self.assertIn('Update my password</button>', self.form)
        # Require existing-user recovery rather than a duplicate new signup.
        self.assertIn('use password recovery instead of creating a duplicate sign-in', self.form)
        self.assertIn('new URL("/auth/v1/recover", configuration.url)', self.js)
        self.assertIn('new URL("/auth/v1/user", configuration.url)', self.js)
        self.assertIn('method: "PUT"', self.js)
        self.assertIn('method: "GET"', self.js)
        self.assertIn('method: "POST"', self.js)
        self.assertIn('fragment.get("type") === "recovery" ? fragment.get("access_token") : null', self.js)
        self.assertIn('history.replaceState(null, "", window.location.pathname', self.js)
        self.assertIn('if (!recoveryToken || configuration?.advertiserRegistrationEnabled !== true) return;', self.js)
        self.assertIn('recoveryRequest.querySelector("button").disabled = !enabled', self.js)
        self.assertIn('recoveryUpdate.querySelector("button").disabled = false', self.js)
        self.assertIn('password.length < 12 || password !== confirmation', self.js)
        self.assertIn('Authorization: `Bearer ${recoveryToken}`', self.js)
        self.assertIn('If an account exists for this email', self.js)
        self.assertNotIn('service_role', self.js)
        self.assertNotIn('localStorage.setItem("recovery', self.js)
        self.assertNotIn('sessionStorage.setItem("recovery', self.js)
        self.assertNotIn('console.log(recoveryToken', self.js)

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
