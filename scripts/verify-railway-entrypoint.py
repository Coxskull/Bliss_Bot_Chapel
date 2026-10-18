#!/usr/bin/env python3
"""Isolated Railway bootstrap/refusal check using disposable certificates only."""
import argparse
import base64
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


def verify_refusal(uid, exit_code, logs):
    if uid <= 0 or f'launching as UID {uid}.' not in logs:
        raise ValueError('Entrypoint did not demonstrate a non-root application launch')
    if exit_code == 0 or 'OIDC authentication must be enabled outside Development.' not in logs:
        raise ValueError('Missing production authentication refusal')
    if 'Now listening on:' in logs or 'Application started.' in logs:
        raise ValueError('Application served before authentication refusal')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--image', required=True)
    parser.add_argument('--revision', required=True)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    if not re.fullmatch(r'[0-9a-f]{40}', args.revision):
        parser.error('Full source revision required')
    args.output.mkdir(parents=True, exist_ok=False)
    # No application credentials or ambient Docker settings are forwarded.
    clean_env = {k: v for k, v in os.environ.items() if k in {'PATH', 'HOME'}}

    def run(command, *, env=None):
        result = subprocess.run(command, capture_output=True, text=True, timeout=60, env=env or clean_env)
        if result.returncode:
            raise RuntimeError('Isolated bootstrap command failed: ' + command[0])
        return result.stdout.strip()

    metadata = json.loads(run(['docker', 'image', 'inspect', args.image]))[0]
    image = metadata['Id']
    config = metadata['Config']
    if config.get('Labels', {}).get('org.opencontainers.image.revision') != args.revision:
        raise ValueError('Image revision mismatch')
    if config.get('Entrypoint') != ['/usr/local/bin/bliss-railway-entrypoint']:
        raise ValueError('Unexpected Railway entrypoint')
    uid = int(run(['docker', 'run', '--rm', '--network', 'none', '--entrypoint', 'id', image, '-u', 'app']))
    with tempfile.TemporaryDirectory(prefix='bliss-bootstrap-fixture-') as temporary:
        directory = Path(temporary)
        key, cert, pfx = (directory / name for name in ('fixture.key', 'fixture.crt', 'fixture.pfx'))
        password = 'Disposable-bootstrap-fixture-only-2026'
        run(['openssl', 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '1',
             '-subj', '/CN=synthetic.invalid', '-keyout', str(key), '-out', str(cert)])
        run(['openssl', 'pkcs12', '-export', '-inkey', str(key), '-in', str(cert),
             '-out', str(pfx), '-passout', 'pass:' + password])
        env = {**clean_env, 'BLISS_DP_CERTIFICATE_BASE64': base64.b64encode(pfx.read_bytes()).decode(),
               'Runtime__DataProtectionCertificatePassword': password}
        command = ['docker', 'create', '--network', 'none', '--read-only', '--cap-drop', 'ALL',
                   '--cap-add', 'CHOWN', '--cap-add', 'FOWNER', '--cap-add', 'DAC_OVERRIDE',
                   '--cap-add', 'SETUID', '--cap-add', 'SETGID', '--security-opt', 'no-new-privileges',
                   '--pids-limit', '128', '--memory', '512m',
                   '--tmpfs', '/data:rw,nosuid,nodev,size=16m', '--tmpfs', '/tmp:rw,nosuid,nodev,size=16m',
                   '-e', 'BLISS_DP_CERTIFICATE_BASE64', '-e', 'Runtime__DataProtectionCertificatePassword', image]
        container = run(command, env=env)
        try:
            run(['docker', 'start', container])
            exit_code = int(run(['docker', 'wait', container]))
            # docker logs may put application stderr on its own stderr stream.
            captured = subprocess.run(['docker', 'logs', container], capture_output=True, text=True, env=clean_env, timeout=15)
            logs = captured.stdout + captured.stderr
            verify_refusal(uid, exit_code, logs)
            report = {'source_revision': args.revision, 'image_id': image,
                      'application_uid': uid, 'production_auth_refusal': True,
                      'entrypoint_exercised': True, 'network': 'none', 'readonly_root': True,
                      'certificate': 'disposable-self-signed-fixture', 'application_credentials_forwarded': False,
                      'database_contacted': False, 'hosted_acceptance': False, 'production_access': False}
            (args.output / 'railway-entrypoint.json').write_text(json.dumps(report, indent=2) + '\n')
            print('Railway bootstrap passed: non-root application launch and production authentication refusal.')
        finally:
            run(['docker', 'rm', '-f', container])


if __name__ == '__main__':
    main()
