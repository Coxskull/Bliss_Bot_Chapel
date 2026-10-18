#!/usr/bin/env python3
"""Offline synthetic fixtures: absolutely no outbound HTTP requests."""
import importlib.util
import pathlib
import socket
import ssl
import unittest
import urllib.error
import urllib.request

SOURCE = pathlib.Path(__file__).with_name('agm-website-preflight.py')
spec = importlib.util.spec_from_file_location('agm_site_preflight', SOURCE)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


class PublicSitePreflightTests(unittest.TestCase):
    def test_only_approved_https_origin(self):
        for bad in ['http://alphacreativecontent.com', 'https://localhost/',
                    'https://alphacreativecontent.com.evil.test/',
                    'https://alphacreativecontent.com@evil.test/',
                    'https://alphacreativecontent.com:8443/',
                    'https://alphacreativecontent.com/admin',
                    'https://alphacreativecontent.com/?token=sensitive']:
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                mod.validate_root(bad)
        self.assertEqual(mod.validate_root('https://www.alphacreativecontent.com'),
                         'https://www.alphacreativecontent.com/')

    def test_safe_link_scoping(self):
        home = 'https://alphacreativecontent.com/'
        self.assertEqual(mod.safe_url(home, '/privacy-policy?email=redact#x'),
                         home + 'privacy-policy')
        for bad in ['https://evil.test/privacy', 'http://alphacreativecontent.com/',
                    '//127.0.0.1/private', 'javascript:alert(1)',
                    'https://alphacreativecontent.com:8443/privacy']:
            with self.subTest(bad=bad):
                self.assertIsNone(mod.safe_url(home, bad))

    def test_redirect_to_external_host_is_denied_before_network(self):
        request = urllib.request.Request('https://alphacreativecontent.com/')
        handler = mod.SameSiteOnly()
        with self.assertRaises(urllib.error.URLError):
            handler.redirect_request(request, None, 302, 'moved', {},
                                     'https://other-merchant.example/collect')

    def test_live_fetch_only_constructs_get_without_sensitive_headers(self):
        class FakeResponse:
            status = 200
            headers = {'Content-Type': 'text/html'}
            def read(self, n): return b'<html><title>Test</title></html>'
            def __enter__(self): return self
            def __exit__(self, *args): pass
        class FakeOpener:
            def __init__(self): self.method = None; self.headers = None
            def open(self, request, timeout=0):
                self.method = request.get_method()
                self.headers = dict(request.header_items())
                return FakeResponse()
        opener = FakeOpener()
        result = mod.public_get('https://alphacreativecontent.com/', opener=opener)
        self.assertEqual(result['state'], 'RETRIEVED')
        self.assertEqual(opener.method, 'GET')
        self.assertNotIn('Cookie', opener.headers)
        self.assertNotIn('Authorization', opener.headers)

    def test_reachable_homepage_does_not_assert_policy_adequacy(self):
        paths = []
        html = '''<html><head><title>ALPHA GLOBAL MEDIA</title></head><body>
          <h1>Advertiser and creator coordination</h1>
          <form action="/api/contact"><input type="email" value="secret@example.com"></form>
          <footer><a href="/privacy-policy?email=secret@example.com">Privacy</a>
          <a href="https://thirdparty.test/terms">External</a></footer></body></html>'''
        def synthetic_get(url):
            paths.append(url)
            return {'state': 'RETRIEVED', 'status': 200, 'content_type': 'text/html',
                    'body': html if url.endswith('/') else '<html>policy draft</html>'}
        report = mod.audit('https://alphacreativecontent.com', getter=synthetic_get)
        self.assertEqual(report['website_status'], 'HOMEPAGE_RETRIEVED_NEEDS_REVIEW')
        self.assertEqual(report['legal_adequacy'], 'UNVERIFIED')
        self.assertEqual(report['stripe_connect'], 'UNVERIFIED')
        self.assertEqual(report['form_submission'], 'NOT_PERFORMED')
        self.assertEqual(report['form_inventory']['count_observed_in_html'], 1)
        self.assertEqual(paths, ['https://alphacreativecontent.com/',
                                 'https://alphacreativecontent.com/privacy-policy'])
        self.assertNotIn('secret@example.com', str(report))
        self.assertEqual(report['policy_links']['terms']['state'], 'NOT_LINKED_IN_STATIC_HTML')

    def test_failed_homepage_keeps_every_policy_unverified(self):
        paths = []
        def inaccessible(url):
            paths.append(url)
            return {'state': 'INACCESSIBLE', 'reason': 'URLError'}
        report = mod.audit('https://alphacreativecontent.com/', getter=inaccessible)
        self.assertEqual(len(paths), 1)
        self.assertEqual(report['website_status'], 'INSUFFICIENT_EVIDENCE')
        self.assertTrue(all(x['state'] == 'UNVERIFIED' for x in report['policy_links'].values()))

    def test_no_sensitive_form_action_or_hidden_values_in_report(self):
        html = '''<html><form action="https://example.test/hook?secret=value">
        <input type="hidden" value="never-include-this"><input type="checkbox"></form></html>'''
        r = mod.audit('https://alphacreativecontent.com/', getter=lambda _: {
            'state':'RETRIEVED','status':200,'content_type':'text/html','body':html})
        self.assertNotIn('never-include-this', str(r))
        self.assertNotIn('secret=value', str(r))
        self.assertEqual(r['form_inventory']['input_type_sets'], [['checkbox','hidden']])


    def test_dns_error_is_a_runtime_observation_not_domain_dns_failure(self):
        error = urllib.error.URLError(socket.gaierror(-3, 'Temporary failure in name resolution'))
        self.assertEqual(
            mod.classify_transport_error(error),
            'DNS_LOOKUP_FAILED_IN_AUDIT_RUNTIME')
        class NoNetworkOpener:
            def open(self, request, timeout=0):
                raise error
        response = mod.public_get(
            'https://alphacreativecontent.com/', opener=NoNetworkOpener())
        self.assertEqual(response['state'], 'INACCESSIBLE')
        self.assertEqual(response['reason'], 'DNS_LOOKUP_FAILED_IN_AUDIT_RUNTIME')
        result = mod.audit('https://alphacreativecontent.com/', getter=lambda _: response)
        self.assertEqual(result['website_status'], 'INSUFFICIENT_EVIDENCE')
        self.assertTrue(any('domain DNS status remains UNVERIFIED' in n for n in result['notes']))
        self.assertTrue(all(item['state'] == 'UNVERIFIED'
                            for item in result['policy_links'].values()))

    def test_tls_timeout_and_redirect_are_distinct_and_redacted(self):
        cases = [
            (urllib.error.URLError(ssl.SSLCertVerificationError(
                1, 'certificate verify failed token=must-not-leak')),
             'TLS_CERTIFICATE_VALIDATION_FAILED_IN_AUDIT_RUNTIME'),
            (urllib.error.URLError(socket.timeout('token=must-not-leak')),
             'NETWORK_TIMEOUT_IN_AUDIT_RUNTIME'),
            (urllib.error.URLError(ConnectionRefusedError('token=must-not-leak')),
             'CONNECTION_REFUSED_IN_AUDIT_RUNTIME'),
            (urllib.error.URLError('redirect leaves approved HTTPS website scope'),
             'OUT_OF_SCOPE_REDIRECT_REJECTED'),
            (urllib.error.URLError('token=must-not-leak'),
             'NETWORK_ACCESS_UNVERIFIED'),
        ]
        for exception, expected in cases:
            with self.subTest(expected=expected):
                class ErrorOpener:
                    def open(self, request, timeout=0):
                        raise exception
                result = mod.public_get(
                    'https://alphacreativecontent.com/', opener=ErrorOpener())
                self.assertEqual(result['reason'], expected)
                self.assertNotIn('must-not-leak', str(result))

    def test_xhtml_homepage_can_be_parsed_without_claiming_legal_adequacy(self):
        result = mod.audit('https://alphacreativecontent.com/', getter=lambda _: {
            'state': 'RETRIEVED', 'status': 200,
            'content_type': 'application/xhtml+xml',
            'body': '<html><head><title>ALPHA</title></head><body><h1>Media</h1></body></html>'
        })
        self.assertEqual(result['website_status'], 'HOMEPAGE_RETRIEVED_NEEDS_REVIEW')
        self.assertEqual(result['title_present'], True)
        self.assertEqual(result['h1_present'], True)
        self.assertEqual(result['legal_adequacy'], 'UNVERIFIED')
        self.assertEqual(result['stripe_payments'], 'UNVERIFIED')

    def test_unknown_network_error_is_not_confused_with_a_website_response(self):
        result = mod.classify_transport_error(
            urllib.error.URLError('a unique unknown proxy error'))
        self.assertEqual(result, 'NETWORK_ACCESS_UNVERIFIED')


    def test_artifact_never_contains_path_tokens_or_dynamic_page_text(self):
        requested = []
        html = '''<html><title>visitor-secret@example.com</title>
        <h1>Private Visitor Identity</h1>
        <a href="/privacy/visitor-secret@example.com?token=123">Privacy</a>
        <a href="/terms-of-service?token=secret-value">Terms</a>
        <a href="/contact/customer/555-9999">Contact</a></html>'''
        def synthetic_get(url):
            requested.append(url)
            return {'state': 'RETRIEVED', 'status': 200,
                    'content_type': 'text/html', 'body': html}
        report = mod.audit('https://alphacreativecontent.com/', getter=synthetic_get)
        output = str(report)
        self.assertNotIn('visitor-secret@example.com', output)
        self.assertNotIn('Private Visitor Identity', output)
        self.assertNotIn('555-9999', output)
        self.assertNotIn('secret-value', output)
        self.assertEqual(report['title_present'], True)
        self.assertEqual(report['h1_present'], True)
        self.assertEqual(report['policy_links']['privacy']['state'],
                         'UNVERIFIED_NONCANONICAL_LINKS_PRESENT')
        self.assertEqual(report['policy_links']['contact']['state'],
                         'UNVERIFIED_NONCANONICAL_LINKS_PRESENT')
        self.assertEqual(report['policy_links']['terms']['state'],
                         'LINK_DISCOVERED')
        self.assertEqual(requested,
                         ['https://alphacreativecontent.com/',
                          'https://alphacreativecontent.com/terms-of-service'])
        self.assertEqual(report['legal_adequacy'], 'UNVERIFIED')

    def test_unexpected_policy_path_is_not_fetched_even_when_onsite(self):
        requested = []
        html = '''<html><a href="/privacy-policy/session-token-007">Privacy</a>
        <a href="/refund-policy">Refunds</a></html>'''
        def synthetic_get(url):
            requested.append(url)
            return {'state': 'RETRIEVED', 'status': 200,
                    'content_type': 'text/html', 'body': html}
        report = mod.audit('https://alphacreativecontent.com/', getter=synthetic_get)
        self.assertNotIn('session-token-007', str(report))
        self.assertEqual(report['policy_links']['privacy']['urls'], [])
        self.assertEqual(report['policy_links']['privacy']['state'],
                         'UNVERIFIED_NONCANONICAL_LINKS_PRESENT')
        self.assertEqual(requested,
                         ['https://alphacreativecontent.com/',
                          'https://alphacreativecontent.com/refund-policy'])


if __name__ == '__main__':
    unittest.main()
