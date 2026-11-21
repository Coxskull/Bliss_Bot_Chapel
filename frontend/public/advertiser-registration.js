"use strict";
(function () {
  const privacyVersion = "advertiser-privacy-2026-11-20-v2";
  const termsVersion = "advertiser-terms-2026-11-20-v2";
  const signup = document.getElementById("signup-form");
  const registration = document.getElementById("registration-form");
  const signupResult = document.getElementById("signup-result");
  const registrationResult = document.getElementById("registration-result");
  const signinLink = document.getElementById("signin-link");
  let configuration;
  let session;
  const randomKey = () => crypto.randomUUID();
  // Browser retry state is a UX hint ONLY. Authenticated database ownership
  // and receipt replay are independently enforced by the issuer+subject API.
  // Never reuse a tab-wide v1 key across distinct signed-in applicants.
  const requestKeyStorage = "alpha.advertiser-registration.request-key.v2";
  let requestKey = randomKey();
  let browserScope = null;
  let submitting = false;
  const validKey = value => typeof value === "string"
    && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  async function bindRetryKey() {
    sessionStorage.removeItem("alpha.advertiser-registration.request-key.v1");
    const email = typeof session?.email === "string" ? session.email.trim().toLowerCase() : "";
    if (!session?.isAuthenticated || !email || !crypto.subtle?.digest) {
      sessionStorage.removeItem(requestKeyStorage);
      browserScope = null;
      requestKey = randomKey();
      return;
    }
    // Store a digest of the *displayed* session email, never the email itself.
    // This is not an authorization decision and never assigns a tenant.
    const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(email));
    browserScope = Array.from(new Uint8Array(digest),
      byte => byte.toString(16).padStart(2, "0")).join("");
    let prior = null;
    try { prior = JSON.parse(sessionStorage.getItem(requestKeyStorage) || "null"); }
    catch (_) { /* corrupt legacy/forged storage becomes a fresh request */ }
    requestKey = prior?.scope === browserScope && validKey(prior.key) ? prior.key : randomKey();
    sessionStorage.setItem(requestKeyStorage, JSON.stringify({ scope: browserScope, key: requestKey }));
  }
  async function initialize() {
    try {
      const [configResponse, sessionResponse] = await Promise.all([
        fetch("/api/auth/supabase-config", { credentials: "same-origin", cache: "no-store" }),
        fetch("/api/auth/session", { credentials: "same-origin", cache: "no-store" })
      ]);
      configuration = configResponse.ok ? await configResponse.json() : null;
      session = sessionResponse.ok ? await sessionResponse.json() : null;
      await bindRetryKey();
      const enabled = configuration?.advertiserRegistrationEnabled === true;
      signup.querySelector("button").disabled = !enabled;
      registration.querySelector("button").disabled = !enabled || session?.isAuthenticated !== true;
      if (!enabled) signupResult.textContent = "Secure advertiser registration is disabled on this environment.";
      else if (session?.isAuthenticated) signupResult.textContent = "You are signed in. Complete the separate business registration form below.";
      signinLink.hidden = !enabled;
    } catch (_) {
      signupResult.textContent = "Registration configuration is unavailable.";
    }
  }
  signup.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!configuration || configuration.advertiserRegistrationEnabled !== true) return;
    signupResult.textContent = "Creating your Supabase Auth sign-in…";
    try {
      const redirectTo = new URL("/advertiser-registration.html", window.location.origin).toString();
      const endpoint = new URL("/auth/v1/signup", configuration.url);
      endpoint.searchParams.set("redirect_to", redirectTo);
      const response = await fetch(endpoint, {
        method: "POST", headers: { "Content-Type": "application/json", apikey: configuration.publishableKey },
        body: JSON.stringify({ email: signup.elements.email.value.trim(), password: signup.elements.password.value })
      });
      if (!response.ok) throw new Error("signup rejected");
      signup.reset();
      signupResult.textContent = "Sign-in request accepted. Confirm your email, then use Continue after confirming email. No advertiser workspace is created until you submit the separate business registration.";
    } catch (_) { signupResult.textContent = "Sign-in could not be created. Check the email and try again."; }
  });
  registration.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (submitting || !configuration || configuration.advertiserRegistrationEnabled !== true
        || !session?.isAuthenticated || !session.csrfToken) return;
    submitting = true;
    registration.querySelector("button").disabled = true;
    registrationResult.textContent = "Submitting registration…";
    const payload = {
      requestKey, businessName: registration.elements.businessName.value.trim(),
      countryCode: registration.elements.countryCode.value.trim().toUpperCase(),
      website: registration.elements.website.value.trim() || null,
      processingConsent: document.getElementById("privacy-consent").checked,
      termsAccepted: document.getElementById("terms-consent").checked,
      marketingOptIn: document.getElementById("marketing-consent").checked,
      privacyNoticeVersion: privacyVersion, termsVersion
    };
    try {
      const response = await fetch("/api/advertisers/register", {
        method: "POST", credentials: "same-origin", headers: {
          "Content-Type": "application/json", "X-CSRF-TOKEN": session.csrfToken
        }, body: JSON.stringify(payload)
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(response.status === 409 ? "This sign-in or request already has a registration." : "Registration was rejected. Check required consent and details.");
      // HTTP success alone is not an immutable consent/registration receipt.
      // Never clear the form or rotate its idempotency key when the response
      // may have been accepted upstream but the authoritative receipt is absent.
      const isUuid = value => typeof value === "string"
        && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
      if ((response.status !== 200 && response.status !== 201)
          || body.status !== "PENDING_REVIEW"
          || !isUuid(body.receiptId) || !isUuid(body.advertiserId)
          || !isUuid(body.requestKey)
          || body.requestKey.toLowerCase() !== payload.requestKey.toLowerCase()
          || body.privacyNoticeVersion !== payload.privacyNoticeVersion
          || body.termsVersion !== payload.termsVersion
          || body.marketingOptIn !== payload.marketingOptIn) {
        throw new Error("Registration receipt could not be verified. Keep this form and retry only with the same request key; do not create a second registration.");
      }
      registration.reset();
      registrationResult.textContent = `Registration received. Status: ${body.status}. Receipt: ${body.receiptId}. Workspace access remains pending review.`;
      requestKey = randomKey();
      if (browserScope)
        sessionStorage.setItem(requestKeyStorage, JSON.stringify({ scope: browserScope, key: requestKey }));
    } catch (error) {
      registrationResult.textContent = error.message || "Registration could not be completed.";
    } finally {
      submitting = false;
      registration.querySelector("button").disabled =
        configuration?.advertiserRegistrationEnabled !== true || !session?.isAuthenticated;
    }
  });
  initialize();
})();
