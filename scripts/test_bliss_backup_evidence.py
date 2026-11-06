import importlib.util
import io
import json
from contextlib import redirect_stdout
from datetime import datetime, timezone, timedelta
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('backup_evidence', Path(__file__).with_name('bliss_backup_evidence.py'))
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
VOLUME_INSTANCE = '11111111-1111-4111-8111-111111111111'
NOW = datetime(2026, 10, 24, 8, 0, tzinfo=timezone.utc)
def iso(n):
    return (NOW + timedelta(hours=n)).isoformat().replace('+00:00', 'Z')

def source():
    return {'projectId': mod.EXPECTED_PROJECT, 'environmentId': mod.EXPECTED_ENVIRONMENT}

def backup():
    return {'volumeInstance': {'id': VOLUME_INSTANCE, 'mountPath': mod.EXPECTED_MOUNT,
             'volume': {'id': mod.EXPECTED_VOLUME, 'name': 'postgres-volume'},
             'serviceInstance': {'serviceName': mod.EXPECTED_SERVICE}},
            'volumeInstanceBackupList': [{'id': 'backup-1', 'createdAt': iso(-5),
                                         'expiresAt': iso(50)}],
            'volumeInstanceBackupScheduleList': [{'id': 'sched-1', 'cron': '0 0 * * *',
                'retentionSeconds': 518400, 'createdAt': iso(-100)}]}

class FakeResponse:
    status = 200
    def __init__(self, data): self.data = json.dumps({'data': data}).encode()
    def __enter__(self): return self
    def __exit__(self, *args): return False
    def geturl(self): return mod.API
    def read(self, size): return self.data[:size]

class FakeOpener:
    def __init__(self): self.calls = []
    def open(self, req, timeout=12):
        info = json.loads(req.data)
        self.calls.append((req.full_url, req.get_method(), info['query'], info['variables'], req.get_header('Project-access-token')))
        return FakeResponse({'projectToken': source()} if info['query'] == mod.SCOPE_QUERY else backup())

class Tests(unittest.TestCase):
    def test_matching_scope_and_volume_returns_nonrelease_evidence(self):
        r = mod.interpret(source(), backup(), VOLUME_INSTANCE, NOW)
        self.assertEqual(r['backup_count_unexpired_or_nonexpiring'], 1)
        self.assertEqual(r['schedule_count'], 1)
        self.assertFalse(r['release_accepted'])
        self.assertFalse(r['backup_success_verified'])
        self.assertFalse(r['restore_integrity_verified'])
        self.assertEqual(r['provider_mutations'], 0)

    def test_no_backup_or_schedule_remains_nonrelease(self):
        x = backup(); x['volumeInstanceBackupList'] = []; x['volumeInstanceBackupScheduleList'] = []
        r = mod.interpret(source(), x, VOLUME_INSTANCE, NOW)
        self.assertEqual(r['backup_metadata_status'], 'NO_UNEXPIRED_BACKUP_METADATA')
        self.assertEqual(r['schedule_metadata_status'], 'NO_BACKUP_SCHEDULE_METADATA')
        self.assertFalse(r['release_accepted'])

    def test_expired_backup_not_counted(self):
        x=backup(); x['volumeInstanceBackupList'][0]['expiresAt']=iso(-1)
        self.assertEqual(mod.interpret(source(),x,VOLUME_INSTANCE,NOW)['backup_count_unexpired_or_nonexpiring'],0)

    def test_wrong_scope_rejected(self):
        for key in ('projectId','environmentId'):
            s=source(); s[key]='wrong'
            with self.subTest(key=key), self.assertRaisesRegex(mod.EvidenceError,'SCOPE_MISMATCH'):
                mod.interpret(s, backup(), VOLUME_INSTANCE, NOW)

    def test_wrong_volume_name_id_mount_or_service_rejected(self):
        cases=[('volume','id'),('volume','name'),('serviceInstance','serviceName'),('','mountPath'),('','id')]
        for nested,key in cases:
            x=backup(); row=x['volumeInstance']
            if nested: row=row[nested]
            row[key]='evil'
            with self.subTest(nested=nested,key=key), self.assertRaisesRegex(mod.EvidenceError,'WRONG_DATABASE_VOLUME'):
                mod.interpret(source(),x,VOLUME_INSTANCE,NOW)

    def test_invalid_backup_dates_fail(self):
        for value in ('tomorrow', '2026-10-24T08:00:00', '2099-10-24T08:00:00Z'):
            x=backup(); x['volumeInstanceBackupList'][0]['createdAt']=value
            with self.subTest(value=value),self.assertRaises(mod.EvidenceError):
                mod.interpret(source(),x,VOLUME_INSTANCE,NOW)

    def test_invalid_schedule_retention_fail(self):
        for value in (0,-2,'86400',True):
            x=backup(); x['volumeInstanceBackupScheduleList'][0]['retentionSeconds']=value
            with self.subTest(value=value),self.assertRaises(mod.EvidenceError):
                mod.interpret(source(),x,VOLUME_INSTANCE,NOW)

    def test_query_rejects_mutation_and_user_supplied_queries(self):
        for code in ('mutation {volumeInstanceBackupCreate}', 'query Evil{me{name}}'):
            with self.subTest(code=code),self.assertRaisesRegex(mod.EvidenceError,'UNAPPROVED_QUERY'):
                mod.query('synthetic',code,opener=FakeOpener())

    def test_strict_json_prevents_duplicate_fields_or_non_json_numbers(self):
        for raw in ('{"a":1,"a":2}','{"a":NaN}','{"a":Infinity}'):
            with self.subTest(raw=raw),self.assertRaises(mod.EvidenceError):
                json.loads(raw,object_pairs_hook=mod.parse_pairs,parse_constant=mod.reject_non_json)

    def test_project_token_only_and_scope_is_checked_before_backup(self):
        opener=FakeOpener(); output=io.StringIO()
        with redirect_stdout(output):
            rc=mod.main(['--volume-instance-id',VOLUME_INSTANCE],
                        {'RAILWAY_TOKEN':'synthetic'},opener=opener)
        self.assertEqual(rc,0)
        self.assertEqual(len(opener.calls),2)
        self.assertEqual([x[2] for x in opener.calls],[mod.SCOPE_QUERY,mod.BACKUP_QUERY])
        self.assertTrue(all(x[0] == mod.API and x[1]=='POST' for x in opener.calls))
        self.assertTrue(all('mutation' not in x[2].casefold() for x in opener.calls))
        self.assertFalse(json.loads(output.getvalue())['release_accepted'])
        self.assertNotIn('synthetic',output.getvalue())

    def test_wrong_environment_token_scope_prevents_backup_query(self):
        class WrongScope(FakeOpener):
            def open(self, req, timeout=12):
                response = super().open(req, timeout)
                if len(self.calls) == 1:
                    wrong = source(); wrong['environmentId'] = 'wrong-environment'
                    return FakeResponse({'projectToken':wrong})
                return response
        opener = WrongScope()
        output = io.StringIO()
        with redirect_stdout(output):
            rc=mod.main(['--volume-instance-id',VOLUME_INSTANCE],
                        {'RAILWAY_TOKEN':'synthetic'},opener=opener)
        self.assertEqual(rc, 2)
        self.assertEqual(len(opener.calls),1)
        self.assertEqual(json.loads(output.getvalue())['status'],
                         'PROJECT_ENVIRONMENT_SCOPE_MISMATCH')

    def test_broad_or_missing_tokens_rejected(self):
        for env in ({},{'RAILWAY_API_TOKEN':'broad'},
                    {'RAILWAY_TOKEN':'project','RAILWAY_API_TOKEN':'broad'}):
            output=io.StringIO()
            with redirect_stdout(output):
                rc=mod.main(['--volume-instance-id',VOLUME_INSTANCE],env,opener=FakeOpener())
            self.assertEqual(rc,2)
            self.assertNotIn('broad',output.getvalue())

    def test_bad_instance_id_rejected_before_query(self):
        output=io.StringIO()
        with redirect_stdout(output):
            rc=mod.main(['--volume-instance-id','not-a-uuid'],
                        {'RAILWAY_TOKEN':'synthetic'},opener=FakeOpener())
        self.assertEqual(rc,2)
        self.assertEqual(json.loads(output.getvalue())['status'],'INVALID_VOLUME_INSTANCE_ID')

    def test_no_redirect_handler_accepts_redirect(self):
        with self.assertRaisesRegex(mod.EvidenceError,'UNEXPECTED_REDIRECT'):
            mod.NoRedirect().redirect_request(None,None,302,'Moved',{},'https://evil.example/')

    def test_duplicate_snapshot_or_schedule_ids_fail_closed(self):
        for key in ('volumeInstanceBackupList', 'volumeInstanceBackupScheduleList'):
            data = backup()
            data[key].append(data[key][0].copy())
            with self.subTest(key=key), self.assertRaisesRegex(mod.EvidenceError, 'INVALID_PROVIDER_RESPONSE'):
                mod.interpret(source(), data, VOLUME_INSTANCE, NOW)

    def test_nondictionary_provider_details_fail_closed(self):
        for bad in (None, [], 'not a dict', 7):
            with self.subTest(bad=repr(bad)), self.assertRaisesRegex(mod.EvidenceError, 'INVALID_PROVIDER_RESPONSE'):
                mod.interpret(source(), bad, VOLUME_INSTANCE, NOW)

    def test_nonstring_token_fails_without_network_or_leak(self):
        for bad in (7, [], {'token': 'secret'}):
            with self.subTest(bad=type(bad).__name__), self.assertRaisesRegex(mod.EvidenceError, 'INVALID_TOKEN'):
                mod.query(bad, mod.SCOPE_QUERY, opener=FakeOpener())

    def test_runtime_disables_inherited_proxy_before_scoped_token_request(self):
        from unittest.mock import patch
        transport = FakeOpener()
        with patch.object(mod.urllib.request, 'build_opener', return_value=transport) as factory:
            result = mod.query('synthetic', mod.SCOPE_QUERY)
        self.assertEqual(result['projectToken'], source())
        handlers = factory.call_args.args
        no_proxy = [h for h in handlers if isinstance(h, mod.urllib.request.ProxyHandler)]
        self.assertEqual(len(no_proxy), 1)
        self.assertEqual(no_proxy[0].proxies, {})
        self.assertTrue(any(isinstance(h, mod.NoRedirect) for h in handlers))

if __name__=='__main__': unittest.main()