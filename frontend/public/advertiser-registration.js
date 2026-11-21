"use strict";
(function () {
  const privacyVersion = "advertiser-privacy-2026-11-20-v2";
  const termsVersion = "advertiser-terms-2026-11-20-v2";
  const signup = document.getElementById("signup-form");
  const registration = document.getElementById("registration-form");
  const signupResult = document.getElementById("signup-result");
  const registrationResult = document.getElementById("registration-result");
  const signinLink = document.getElementById("signin-link");
  const recoveryDetails = document.getElementById("account-recovery");
  const recoveryRequest = document.getElementById("recovery-request-form");
  const recoveryUpdate = document.getElementById("recovery-update-form");
  const recoveryResult = document.getElementById("recovery-result");
  // Supabase's implicit password-recovery callback returns an access token in
  // the URL fragment. Never persist, log, copy into a link, or send it to Bliss.
  const fragment = new URLSearchParams(window.location.hash.slice(1));
  let recoveryToken = null;
  if (fragment.get("type") === "recovery" || fragment.has("error")) {
    recoveryToken = fragment.get("type") === "recovery" ? fragment.get("access_token") : null;
    history.replaceState(null, "", window.location.pathname + window.location.search + "#account-recovery");
    recoveryDetails.open = true;
    recoveryResult.textContent = recoveryToken ? "Verifying the recovery link…" : "This recovery link is invalid or expired. Request a new link.";
  } else if (window.location.hash === "#account-recovery") {
    recoveryDetails.open = true;
  }
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
      recoveryRequest.querySelector("button").disabled = !enabled;
      if (recoveryToken && enabled) {
        try {
          // Verify the bearer token with the configured issuer before enabling
          // the password-change form. The token stays in memory only.
          const response = await fetch(new URL("/auth/v1/user", configuration.url), {
            method: "GET", headers: {
              apikey: configuration.publishableKey,
              Authorization: `Bearer ${recoveryToken}`
            }, cache: "no-store"
          });
          if (!response.ok) throw new Error("invalid recovery session");
          recoveryRequest.hidden = true;
          recoveryUpdate.hidden = false;
          recoveryUpdate.querySelector("button").disabled = false;
          recoveryResult.textContent = "Recovery link verified. Choose a new password below.";
        } catch (_) {
          recoveryToken = null;
          recoveryResult.textContent = "This recovery link is invalid or expired. Request a new link.";
        }
      }
    } catch (_) {
      signupResult.textContent = "Registration configuration is unavailable.";
    }
  }
  recoveryRequest.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (configuration?.advertiserRegistrationEnabled !== true) return;
    const button = recoveryRequest.querySelector("button");
    button.disabled = true;
    recoveryResult.textContent = "Requesting a password-reset email…";
    try {
      const endpoint = new URL("/auth/v1/recover", configuration.url);
      // Reuse the already configured sign-up confirmation return URL: no new
      // callback host or wildcard redirect allowlist is required.
      endpoint.searchParams.set("redirect_to",
        new URL("/advertiser-registration.html", window.location.origin).toString());
      const response = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json", apikey: configuration.publishableKey },
        body: JSON.stringify({ email: recoveryRequest.elements.email.value.trim() })
      });
      if (response.status === 429) throw new Error("Too many attempts. Please try again later.");
      if (!response.ok) throw new Error("Recovery email could not be sent. Contact ALPHA support.");
      recoveryRequest.reset();
      // Do not disclose whether any given address has an ALPHA account.
      recoveryResult.textContent =
        "If an account exists for this email, a password-reset link will arrive. Open the link, set a new password, then sign in again.";
    } catch (error) {
      recoveryResult.textContent = error.message || "Password recovery is temporarily unavailable.";
    } finally {
      button.disabled = configuration?.advertiserRegistrationEnabled !== true;
    }
  });

  recoveryUpdate.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!recoveryToken || configuration?.advertiserRegistrationEnabled !== true) return;
    const button = recoveryUpdate.querySelector("button");
    const password = recoveryUpdate.elements.password.value;
    const confirmation = recoveryUpdate.elements.confirmPassword.value;
    if (password.length < 12 || password !== confirmation) {
      recoveryResult.textContent = "Use at least 12 characters and enter the same password twice.";
      return;
    }
    button.disabled = true;
    try {
      // Authenticate directly against Supabase Auth. Never send a password or
      // recovery token to an ALPHA API, analytics service, or URL parameter.
      const response = await fetch(new URL("/auth/v1/user", configuration.url), {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          apikey: configuration.publishableKey,
          Authorization: `Bearer ${recoveryToken}`
        },
        body: JSON.stringify({ password })
      });
      if (!response.ok) throw new Error("Password update was rejected. Request a fresh recovery link.");
      recoveryToken = null;
      recoveryUpdate.reset();
      recoveryUpdate.hidden = true;
      recoveryRequest.hidden = false;
      recoveryResult.textContent =
        "Password updated. Return to Advertiser login and sign in with your new password.";
    } catch (error) {
      recoveryResult.textContent = error.message || "Password update could not be completed.";
    } finally {
      button.disabled = !recoveryToken;
    }
  });

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
      signupResult.textContent = "Sign-up request received. If this is a new account, check your confirmation email. If you already have an account, use Sign in or Forgot password instead; creating another sign-in will not replace your current password.";
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
      if (!response.ok) {
        // A verified operator/staff or already assigned identity is NOT a
        // pending advertiser applicant. Never weaken server role boundaries
        // or imply that changing business fields would solve HTTP 403.
        const messages = {
          400: "Registration details or required consent were invalid. Review the form.",
          401: "Your sign-in has expired. Sign in again before submitting.",
          403: "This signed-in account cannot create a new advertiser registration. ALPHA staff/operator accounts must use a separate verified applicant account to register a business.",
          409: "This sign-in or request already has an advertiser registration.",
          429: "Too many attempts. Please retry later."
        };
        throw new Error(messages[response.status] || "Registration could not be completed. Please contact ALPHA support.");
      }
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
