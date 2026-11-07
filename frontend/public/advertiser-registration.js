"use strict";
(function () {
  const privacyVersion = "advertiser-privacy-2026-11-07-v1";
  const termsVersion = "advertiser-terms-2026-11-07-v1";
  const signup = document.getElementById("signup-form");
  const registration = document.getElementById("registration-form");
  const signupResult = document.getElementById("signup-result");
  const registrationResult = document.getElementById("registration-result");
  const signinLink = document.getElementById("signin-link");
  let configuration;
  let session;
  const requestKeyStorage = "alpha.advertiser-registration.request-key.v1";
  let requestKey = sessionStorage.getItem(requestKeyStorage) || randomKey();
  sessionStorage.setItem(requestKeyStorage, requestKey);
  const randomKey = () => crypto.randomUUID();
  async function initialize() {
    try {
      const [configResponse, sessionResponse] = await Promise.all([
        fetch("/api/auth/supabase-config", { credentials: "same-origin", cache: "no-store" }),
        fetch("/api/auth/session", { credentials: "same-origin", cache: "no-store" })
      ]);
      configuration = configResponse.ok ? await configResponse.json() : null;
      session = sessionResponse.ok ? await sessionResponse.json() : null;
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
    if (!configuration || configuration.advertiserRegistrationEnabled !== true || !session?.isAuthenticated || !session.csrfToken) return;
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
          "Content-Type": "application/json", "RequestVerificationToken": session.csrfToken
        }, body: JSON.stringify(payload)
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(response.status === 409 ? "This sign-in or request already has a registration." : "Registration was rejected. Check required consent and details.");
      registration.reset();
      registrationResult.textContent = `Registration received. Status: ${body.status}. Receipt: ${body.receiptId}. Workspace access remains pending review.`;
      requestKey = randomKey();
      sessionStorage.setItem(requestKeyStorage, requestKey);
    } catch (error) { registrationResult.textContent = error.message || "Registration could not be completed."; }
  });
  initialize();
})();
