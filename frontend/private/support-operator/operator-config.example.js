// Example only. Do not publish this filename as operator-config.js.
// Deployment must use its approved HTTPS origin and a Supabase *publishable*
// or legacy anon key. A SERVICE_ROLE / sb_secret key must NEVER be exposed.
// Static dashboard code is unserved in source-only review.
window.ALPHA_SUPPORT_OPERATOR_CONFIG = Object.freeze({
  environment: "staging",
  supabaseUrl: "https://wdlmcriajgsretgyusmo.supabase.co",
  publishableKey: "REPLACE_WITH_PUBLISHABLE_OR_ANON_KEY",
  operatorOrigin: "https://YOUR-PRIVATE-OPERATOR-ORIGIN.example",
  mutationsEnabled: false
});
