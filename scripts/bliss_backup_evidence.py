#!/usr/bin/env python3
"""Read-only Railway production Postgres backup inventory. NEVER accepts broad API tokens.

Run locally with an existing environment-scoped RAILWAY_TOKEN and the production
Postgres volume INSTANCE id obtained independently from the Railway dashboard.
Does not create a backup, restore a volume, enable PITR, or write provider data.
"""
import argparse
from datetime import datetime, timezone, timedelta
import json
import os
import re
import ssl
import sys
import urllib.request

API = 'https://backboard.railway.com/graphql/v2'
EXPECTED_PROJECT = 'fb33c1b7-03fb-42c8-89b1-081951a738fd'
EXPECTED_ENVIRONMENT = 'b67a9836-36bf-4083-93f7-00c867b13f39'
EXPECTED_VOLUME = 'd4b4fed4-ea9b-4787-a35e-303a0cccf35a'
EXPECTED_SERVICE = 'Postgres'
EXPECTED_MOUNT = '/var/lib/postgresql/data'
UUID = re.compile(r'[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\Z', re.I)
SCOPE_QUERY = 'query Scope { projectToken { projectId environmentId } }'
BACKUP_QUERY = '''query BackupEvidence($id: String!) {
  volumeInstance(id: $id) { id mountPath volume { id name } serviceInstance { serviceName } }
  volumeInstanceBackupList(volumeInstanceId: $id) {
    id name createdAt expiresAt usedMB referencedMB
  }
  volumeInstanceBackupScheduleList(volumeInstanceId: $id) {
    id name cron kind retentionSeconds createdAt
  }
}'''

class EvidenceError(Exception):
    def __init__(self, code):
        self.code = code


def parse_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise EvidenceError('INVALID_PROVIDER_RESPONSE')
        result[key] = value
    return result


def reject_non_json(_):
    raise EvidenceError('INVALID_PROVIDER_RESPONSE')


def query(token, gql, variables=None, opener=None):
    if gql not in (SCOPE_QUERY, BACKUP_QUERY) or 'mutation' in gql.casefold():
        raise EvidenceError('UNAPPROVED_QUERY')
    if not token or '\n' in token or '\r' in token or len(token) > 4096:
        raise EvidenceError('INVALID_TOKEN')
    body = json.dumps({'query': gql, 'variables': variables or {}}, separators=(',', ':')).encode()
    req = urllib.request.Request(API, data=body, method='POST', headers={
        'Content-Type': 'application/json', 'Accept': 'application/json',
        'Project-Access-Token': token})
    try:
        if opener is None:
            # The fixed Railway HTTPS origin is the only permitted destination.
            opener = urllib.request.build_opener(urllib.request.HTTPSHandler(context=ssl.create_default_context()),
                                                  NoRedirect())
        with opener.open(req, timeout=12) as response:
            if response.geturl() != API or response.status != 200:
                raise EvidenceError('PROVIDER_QUERY_FAILED')
            raw = response.read(128 * 1024 + 1)
            if len(raw) > 128 * 1024:
                raise EvidenceError('PROVIDER_RESPONSE_TOO_LARGE')
        payload = json.loads(raw.decode('utf-8'), object_pairs_hook=parse_pairs,
                             parse_constant=reject_non_json)
        if not isinstance(payload, dict) or payload.get('errors') or not isinstance(payload.get('data'), dict):
            raise EvidenceError('PROVIDER_QUERY_FAILED')
        return payload['data']
    except EvidenceError:
        raise
    except (OSError, ValueError, UnicodeError, KeyError, TypeError) as exc:
        # The network/GraphQL error can carry user secrets: NEVER echo it.
        raise EvidenceError('PROVIDER_QUERY_FAILED') from None


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise EvidenceError('UNEXPECTED_REDIRECT')


def parse_time(value):
    if not isinstance(value, str) or not value:
        raise EvidenceError('INVALID_PROVIDER_RESPONSE')
    try:
        parsed = datetime.fromisoformat(value.replace('Z', '+00:00'))
    except ValueError:
        raise EvidenceError('INVALID_PROVIDER_RESPONSE') from None
    if parsed.tzinfo is None:
        raise EvidenceError('INVALID_PROVIDER_RESPONSE')
    return parsed.astimezone(timezone.utc)


def interpret(scope, details, instance_id, now=None):
    now = now or datetime.now(timezone.utc)
    if not isinstance(scope, dict) or scope.get('projectId') != EXPECTED_PROJECT or scope.get('environmentId') != EXPECTED_ENVIRONMENT:
        raise EvidenceError('PROJECT_ENVIRONMENT_SCOPE_MISMATCH')
    if not UUID.fullmatch(instance_id):
        raise EvidenceError('INVALID_VOLUME_INSTANCE_ID')
    vi = details.get('volumeInstance')
    if not isinstance(vi, dict) or vi.get('id') != instance_id or vi.get('mountPath') != EXPECTED_MOUNT \
       or not isinstance(vi.get('volume'), dict) or vi['volume'].get('id') != EXPECTED_VOLUME \
       or vi['volume'].get('name') != 'postgres-volume' \
       or not isinstance(vi.get('serviceInstance'), dict) or vi['serviceInstance'].get('serviceName') != EXPECTED_SERVICE:
        raise EvidenceError('WRONG_DATABASE_VOLUME')
    backups = details.get('volumeInstanceBackupList')
    schedules = details.get('volumeInstanceBackupScheduleList')
    if not isinstance(backups, list) or not isinstance(schedules, list) or len(backups) > 10000 or len(schedules) > 100:
        raise EvidenceError('INVALID_PROVIDER_RESPONSE')
    valid = []
    for item in backups:
        if not isinstance(item, dict) or not isinstance(item.get('id'), str) or not item['id']:
            raise EvidenceError('INVALID_PROVIDER_RESPONSE')
        created = parse_time(item.get('createdAt'))
        expiry = parse_time(item['expiresAt']) if item.get('expiresAt') else None
        if created > now + timedelta(minutes=5) or (expiry and expiry < created):
            raise EvidenceError('INVALID_PROVIDER_RESPONSE')
        if not expiry or expiry >= now:
            valid.append(created)
    for item in schedules:
        if not isinstance(item, dict) or not isinstance(item.get('cron'), str) or not item['cron']:
            raise EvidenceError('INVALID_PROVIDER_RESPONSE')
        if not isinstance(item.get('retentionSeconds'), int) or isinstance(item['retentionSeconds'], bool) or item['retentionSeconds'] <= 0:
            raise EvidenceError('INVALID_PROVIDER_RESPONSE')
        parse_time(item.get('createdAt'))
    latest = max(valid) if valid else None
    return {
        'scope': 'EXPECTED_PRODUCTION_POSTGRES_VOLUME',
        'backup_metadata_status': 'EXISTING_BACKUP_METADATA' if valid else 'NO_UNEXPIRED_BACKUP_METADATA',
        'schedule_metadata_status': 'CONFIGURED_SCHEDULE_METADATA' if schedules else 'NO_BACKUP_SCHEDULE_METADATA',
        'backup_count_unexpired_or_nonexpiring': len(valid),
        'schedule_count': len(schedules),
        'latest_backup_created_at_utc': latest.isoformat().replace('+00:00', 'Z') if latest else None,
        'backup_success_verified': False,  # Railway API list has no success field.
        'restore_integrity_verified': False,
        'cost_approved': False,
        'production_migrations_authorized': False,
        'release_accepted': False,
        'provider_mutations': 0,
    }


def main(argv=None, environ=None, opener=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--volume-instance-id', required=True, help='Railway production Postgres volume INSTANCE ID')
    args = parser.parse_args(argv)
    env = environ if environ is not None else os.environ
    token = env.get('RAILWAY_TOKEN', '')
    try:
        if not UUID.fullmatch(args.volume_instance_id):
            raise EvidenceError('INVALID_VOLUME_INSTANCE_ID')
        if not token or env.get('RAILWAY_API_TOKEN'):
            raise EvidenceError('PROJECT_TOKEN_REQUIRED')
        scope = query(token, SCOPE_QUERY, opener=opener).get('projectToken')
        # Verify environment-scoped credential BEFORE querying backup metadata.
        if not isinstance(scope, dict) or scope.get('projectId') != EXPECTED_PROJECT or scope.get('environmentId') != EXPECTED_ENVIRONMENT:
            raise EvidenceError('PROJECT_ENVIRONMENT_SCOPE_MISMATCH')
        data = query(token, BACKUP_QUERY, {'id': args.volume_instance_id}, opener=opener)
        report = interpret(scope, data, args.volume_instance_id)
        print(json.dumps(report, sort_keys=True))
        return 0
    except EvidenceError as e:
        print(json.dumps({'status': e.code, 'release_accepted': False, 'provider_mutations': 0}, sort_keys=True))
        return 2

if __name__ == '__main__':
    sys.exit(main())