// Visitor-facing indicator only. The server's RestrictedPreviewGate remains
// the authorization control for all API requests and protected resources.
fetch('/preview-status', { cache: 'no-store' }).then(async response => {
  if (!response.ok) return;
  const status = await response.json();
  if (status.restrictedPreview !== true || status.operational !== false) return;

  const banner = document.createElement('aside');
  banner.setAttribute('role', 'status');
  banner.textContent = 'INTERFACE PREVIEW · Data, login and actions are unavailable while setup is completed.';
  banner.style.cssText = 'position:sticky;top:0;z-index:99999;padding:14px 20px;background:#37204f;color:#fff;text-align:center;font:600 14px/1.5 system-ui;';
  document.body.prepend(banner);

  // Do not imply that an operator is online when only a static page works.
  const onlineLabel = document.querySelector('.planner-profile .online');
  if (onlineLabel) onlineLabel.textContent = 'Preview only · Offline';

  // Preserve in-page navigation and the public landing page, but avoid
  // sending visitors to protected HTML that intentionally responds 503.
  const publicPaths = new Set(['/', '/index.html']);
  document.querySelectorAll('a[href]').forEach(anchor => {
    const href = anchor.getAttribute('href');
    if (!href || href.startsWith('#')) return;
    let target;
    try {
      target = new URL(href, window.location.href);
    } catch {
      target = null;
    }
    if (target && target.origin === window.location.origin && publicPaths.has(target.pathname)) return;
    anchor.removeAttribute('href');
    anchor.setAttribute('aria-disabled', 'true');
    anchor.setAttribute('tabindex', '-1');
    anchor.setAttribute('title', 'Unavailable in interface preview');
    anchor.style.opacity = '0.55';
    anchor.style.cursor = 'not-allowed';
  });

  // Leave the mobile navigation toggle functional; lock business actions.
  document.querySelectorAll('.suggestions button, form button, form input, form textarea, form select').forEach(control => {
    control.disabled = true;
    control.setAttribute('aria-disabled', 'true');
  });
}).catch(() => {});
