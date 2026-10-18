(() => {
  const parameters = new URLSearchParams(location.hash.slice(1));
  if (['invite', 'recovery'].includes(parameters.get('type')) || parameters.has('error_code')) {
    // Existing invitation emails point at the application root. Keep secrets in
    // the fragment while moving to setup; never forward to an external origin.
    location.replace('/account-setup.html' + location.hash);
  }
})();
