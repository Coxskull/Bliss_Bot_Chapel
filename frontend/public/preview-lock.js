fetch('/preview-status', { cache: 'no-store' }).then(async response => {
  if (!response.ok || !(await response.json()).restrictedPreview) return;
  const banner = document.createElement('aside');
  banner.setAttribute('role', 'status');
  banner.textContent = 'INTERFACE PREVIEW · Data, login and actions are unavailable while setup is completed.';
  banner.style.cssText = 'position:sticky;top:0;z-index:99999;padding:14px 20px;background:#37204f;color:#fff;text-align:center;font:600 14px/1.5 system-ui;';
  document.body.prepend(banner);
  document.querySelectorAll('button[type="submit"],input[type="submit"]').forEach(element => element.disabled = true);
}).catch(() => {});
