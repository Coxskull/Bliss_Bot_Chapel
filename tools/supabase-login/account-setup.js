import { createClient } from '@supabase/supabase-js';
const status = document.getElementById('status');
const form = document.getElementById('password-setup');
const parameters = new URLSearchParams(location.hash.slice(1));
// Consume only invitation/recovery sessions; never retain link secrets in history.
const linkType = parameters.get('type');
let accessToken = parameters.get('access_token');
let refreshToken = parameters.get('refresh_token');
history.replaceState(null, '', '/account-setup.html');
let supabase;
let verifiedUserId;
const unavailable = () => { status.textContent = 'This setup link is unavailable or expired. Ask your ALPHA administrator for a new invitation.'; };
form.addEventListener('submit', async event => {
  event.preventDefault();
  const button = form.querySelector('button');
  button.disabled = true;
  try {
    if (!verifiedUserId || !supabase) throw new Error('No verified invitation.');
    if (form.password.value.length < 12 || form.password.value !== form.confirmation.value) {
      status.textContent = 'Use at least 12 characters and enter the same password twice.';
      return;
    }
    // Revalidate the identity immediately before changing its credential.
    const { data, error } = await supabase.auth.getUser();
    if (error || data?.user?.id !== verifiedUserId) throw new Error('Session changed.');
    const result = await supabase.auth.updateUser({ password: form.password.value });
    if (result.error) throw new Error('Password update refused.');
    form.hidden = true;
    status.textContent = 'Password saved. Continue to sign in with your email and password.';
    document.getElementById('continue').hidden = false;
  } catch {
    status.textContent = 'Password setup could not be completed. Try again or request a new invitation.';
  } finally {
    form.password.value = ''; form.confirmation.value = ''; button.disabled = false;
  }
});
(async () => {
  try {
    if (!['invite', 'recovery'].includes(linkType) || !accessToken || !refreshToken) throw new Error('Invalid setup link.');
    const response = await fetch('/api/auth/supabase-config', { cache: 'no-store' });
    if (!response.ok) {
      status.textContent = 'Account setup is not available yet. Your administrator must finish the deployment first.';
      return;
    }
    const config = await response.json();
    supabase = createClient(config.url, config.publishableKey, { auth: { storage: sessionStorage, detectSessionInUrl: false } });
    const { error } = await supabase.auth.setSession({ access_token: accessToken, refresh_token: refreshToken });
    if (error) throw new Error('Invalid invitation session.');
    const verified = await supabase.auth.getUser();
    if (verified.error || !verified.data?.user?.id || !verified.data.user.email) throw new Error('Unverified identity.');
    verifiedUserId = verified.data.user.id;
    status.textContent = 'Choose a password for ' + verified.data.user.email + '.';
    form.hidden = false;
  } catch { unavailable(); }
  finally { accessToken = ''; refreshToken = ''; parameters.delete('access_token'); parameters.delete('refresh_token'); }
})();
