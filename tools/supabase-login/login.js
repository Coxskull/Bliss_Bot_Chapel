import { createClient } from '@supabase/supabase-js';
const status = document.getElementById('status');
const login = document.getElementById('login');
const consent = document.getElementById('consent');
const authorizationId = new URLSearchParams(location.search).get('authorization_id');
let supabase, config;
function fail(message) { status.textContent = message; }
function redirect(target) {
  const url = new URL(target);
  // Only the one registered callback is allowed, including error redirects.
  if (url.origin !== location.origin || url.pathname !== '/signin-oidc') throw new Error('Unexpected callback.');
  location.assign(url.href);
}
async function showConsent() {
  const { data: { user }, error } = await supabase.auth.getUser();
  if (error || !user) { login.hidden = false; status.textContent = 'Use your approved test account.'; return; }
  const { data, error: detailError } = await supabase.auth.oauth.getAuthorizationDetails(authorizationId);
  if (detailError || !data) throw new Error('Unable to validate this sign-in request. Start again from Bliss Bot.');
  if (!('authorization_id' in data)) { redirect(data.redirect_url); return; }
  if (data.client.id !== config.clientId || new URL(data.redirect_uri).origin !== location.origin
      || new URL(data.redirect_uri).pathname !== '/signin-oidc') throw new Error('This application is not approved.');
  login.hidden = true; consent.hidden = false;
  document.getElementById('scope').textContent = 'Requested information: ' + data.scope;
  status.textContent = 'Signed in. Review the request below.';
}
login.addEventListener('submit', async event => {
  event.preventDefault(); const button = login.querySelector('button'); button.disabled = true;
  try {
    const { error } = await supabase.auth.signInWithPassword({ email: login.email.value, password: login.password.value });
    login.password.value = '';
    if (error) throw new Error('Sign-in failed. Check your credentials and account approval.');
    await showConsent();
  } catch (error) { fail(error.message); } finally { button.disabled = false; }
});
for (const action of ['approve', 'deny']) document.getElementById(action).addEventListener('click', async () => {
  const buttons = consent.querySelectorAll('button'); buttons.forEach(button => button.disabled = true);
  try {
    const { data, error } = await (action === 'approve'
      ? supabase.auth.oauth.approveAuthorization(authorizationId) : supabase.auth.oauth.denyAuthorization(authorizationId));
    if (error || !data?.redirect_url) throw new Error('The authorization decision could not be completed.');
    redirect(data.redirect_url);
  } catch (error) { fail(error.message); buttons.forEach(button => button.disabled = false); }
});
(async () => {
  try {
    if (!authorizationId) throw new Error('Start sign in from Bliss Bot to continue.');
    const response = await fetch('/api/auth/supabase-config', { cache: 'no-store' });
    if (!response.ok) throw new Error('Test sign in is not available yet.');
    config = await response.json();
    // Short-lived Supabase UI session stays in this tab. Bliss session cookies
    // are issued only by the backend after token verification and test-user checks.
    supabase = createClient(config.url, config.publishableKey, { auth: { storage: sessionStorage, detectSessionInUrl: false } });
    await showConsent();
  } catch (error) { fail(error.message); }
})();
