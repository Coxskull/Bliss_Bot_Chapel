"use strict";
(function () {
  const privacyVersion = "creator-privacy-2026-11-22-v2";
  const termsVersion = "creator-terms-2026-11-22-v2";
  const signup = document.getElementById("signup-form");
  const registration = document.getElementById("registration-form");
  const signupResult = document.getElementById("signup-result");
  const registrationResult = document.getElementById("registration-result");
  const signinLink = document.getElementById("signin-link");
  const requestKeyStorage = "alpha.creator-registration.request-key.v1";
  const randomKey = () => crypto.randomUUID();
  let requestKey = sessionStorage.getItem(requestKeyStorage) || randomKey();
  sessionStorage.setItem(requestKeyStorage, requestKey);
  let configuration;
  let session;
  let creatorReady = false;
  const accessStatus = document.getElementById("creator-access-status");
  const accessSignin = document.getElementById("creator-access-signin");
  const profileButton = registration.querySelector("button");
  async function checkCreatorAccess(enabled) {
    creatorReady = false;
    profileButton.disabled = true;
    accessSignin.hidden = !enabled;
    if (!enabled) { accessStatus.textContent = "Creator registration is not currently available."; return; }
    if (!session?.isAuthenticated || !session.csrfToken) {
      accessStatus.textContent = "Sign in with your email-confirmed ALPHA account before submitting this form.";
      return;
    }
    // The existing API enforces issuer and pending creator lane ownership.
    // A valid session alone is not proof of permission to register.
    const response = await fetch("/api/creators/registration", { credentials: "same-origin", cache: "no-store" });
    if (response.status === 404) {
      creatorReady = true; profileButton.disabled = false; accessSignin.hidden = true;
      accessStatus.textContent = "Signed in for creator registration. You can submit your profile.";
    } else if (response.ok) {
      const status = await response.json();
      accessStatus.textContent = "Your creator registration already exists. Status: " + String(status.status || "PENDING_REVIEW") + ".";
      accessSignin.hidden = true;
    } else if (response.status === 401 || response.status === 403) {
      accessStatus.textContent = "This session does not have creator registration access. Use the creator sign-in link. Existing approved advertiser or staff identities require account review.";
    } else {
      accessStatus.textContent = "Creator access could not be verified. Reload the page or contact ALPHA support.";
    }
  }

  async function initialize() {
    try {
      const [configResponse, sessionResponse] = await Promise.all([
        fetch("/api/auth/supabase-config", { credentials: "same-origin", cache: "no-store" }),
        fetch("/api/auth/session", { credentials: "same-origin", cache: "no-store" })
      ]);
      configuration = configResponse.ok ? await configResponse.json() : null;
      session = sessionResponse.ok ? await sessionResponse.json() : null;
      const enabled = configuration?.creatorRegistrationEnabled === true;
      signup.querySelector("button").disabled = !enabled;
      await checkCreatorAccess(enabled);
      if (!enabled) signupResult.textContent = "Secure creator registration is disabled on this environment.";
      else if (session?.isAuthenticated) signupResult.textContent = "You are signed in. Check your creator access status below.";
      signinLink.hidden = !enabled;
    } catch (_) {
      signupResult.textContent = "Registration configuration is unavailable.";
      profileButton.disabled = true;
      accessStatus.textContent = "Creator access could not be verified. Reload the page or contact support.";
    }
  }

  signup.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!configuration || configuration.creatorRegistrationEnabled !== true) return;
    signupResult.textContent = "Creating your Supabase Auth sign-in…";
    try {
      const redirectTo = new URL("/creator-registration.html", window.location.origin).toString();
      const endpoint = new URL("/auth/v1/signup", configuration.url);
      endpoint.searchParams.set("redirect_to", redirectTo);
      const response = await fetch(endpoint, {
        method: "POST", headers: { "Content-Type": "application/json", apikey: configuration.publishableKey },
        body: JSON.stringify({ email: signup.elements.email.value.trim(), password: signup.elements.password.value })
      });
      if (!response.ok) throw new Error("signup rejected");
      signup.reset();
      signupResult.textContent = "Sign-in request accepted. Confirm your email, then continue here. No creator account or access is granted until the separate registration is reviewed.";
    } catch (_) { signupResult.textContent = "Sign-in could not be created. Check the email and try again."; }
  });

  registration.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!creatorReady || !configuration || configuration.creatorRegistrationEnabled !== true || !session?.isAuthenticated || !session.csrfToken) return;
    registrationResult.textContent = "Submitting creator registration…";
    const payload = {
      requestKey, displayName: registration.elements.displayName.value.trim(),
      countryCode: registration.elements.countryCode.value.trim().toUpperCase(),
      channelUrl: registration.elements.channelUrl.value.trim(),
      primaryLanguage: registration.elements.primaryLanguage.value.trim() || null,
      processingConsent: document.getElementById("privacy-consent").checked,
      termsAccepted: document.getElementById("terms-consent").checked,
      marketingOptIn: document.getElementById("marketing-consent").checked,
      privacyNoticeVersion: privacyVersion, termsVersion
    };
    try {
      const response = await fetch("/api/creators/register", {
        method: "POST", credentials: "same-origin", headers: {
          "Content-Type": "application/json", "RequestVerificationToken": session.csrfToken
        }, body: JSON.stringify(payload)
      });
      const body = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(response.status === 409 ? "This sign-in or request already has a creator registration." : "Registration was rejected. Check required consent and details.");
      registration.reset();
      creatorReady = false;
      profileButton.disabled = true;
      accessStatus.textContent = "Profile submitted. Manual review is required.";
      registrationResult.textContent = `Registration received. Status: ${body.status}. Receipt: ${body.receiptId}. Creator verification and account access remain pending review.`;
      requestKey = randomKey();
      sessionStorage.setItem(requestKeyStorage, requestKey);
    } catch (error) { registrationResult.textContent = error.message || "Registration could not be completed."; }
  });
  initialize();
})();
