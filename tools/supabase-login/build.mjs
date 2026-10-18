import { build } from 'esbuild';
for (const [entry, output] of [['login.js', 'oauth-consent.bundle.js'], ['account-setup.js', 'account-setup.bundle.js']]) {
  await build({ entryPoints: [entry], bundle: true, minify: true, platform: 'browser', outfile: '../../frontend/public/' + output });
}
