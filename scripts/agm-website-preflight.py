#!/usr/bin/env python3
"""Read-only public-site preflight for ALPHA GLOBAL MEDIA LLC.

GET only, no cookies, credentials, form submission or website mutation.
Results show reachability, not a legal approval or Stripe acceptance.
"""
import argparse
import datetime as dt
import html.parser
import json
import pathlib
import re
import socket
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

HOSTS = frozenset({'alphacreativecontent.com', 'www.alphacreativecontent.com'})
POLICIES = {
    'privacy': ('privacy', 'data-protection'),
    'terms': ('terms', 'conditions', 'tos'),
    'refunds': ('refund', 'cancellation', 'returns'),
    'contact': ('contact', 'support'),
}
# Only generic, reviewed routes may be probed or included in CI artifacts.
# Arbitrary same-site paths can encode visitor identities or secret tokens.
PUBLIC_POLICY_PATHS = {
    'privacy': frozenset({'/privacy', '/privacy-policy', '/data-protection'}),
    'terms': frozenset({'/terms', '/terms-of-service', '/terms-and-conditions', '/tos'}),
    'refunds': frozenset({'/refund', '/refunds', '/refund-policy',
                          '/refund-and-cancellation-policy', '/cancellation-policy',
                          '/returns'}),
    'contact': frozenset({'/contact', '/contact-us', '/support'}),
}
USER_AGENT = 'AGM-Site-Preflight/1.0 (read-only policy and form inventory)'


class PublicHtml(html.parser.HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.links = []
        self.forms = []
        self.in_footer = False
        self.in_title = False
        self.in_heading = False
        self.title = []
        self.headings = []
        self.footer_links = []
        self._current_form = None

    def handle_starttag(self, tag, attrs):
        att = dict(attrs)
        if tag == 'footer':
            self.in_footer = True
        elif tag == 'title':
            self.in_title = True
        elif tag == 'h1':
            self.in_heading = True
        elif tag == 'a' and att.get('href'):
            href = att['href']
            self.links.append(href)
            if self.in_footer:
                self.footer_links.append(href)
        elif tag == 'form':
            self._current_form = {'input_types': set(), 'has_consent_checkbox': False}
            self.forms.append(self._current_form)
        elif self._current_form is not None and tag in ('input', 'textarea', 'select'):
            t = (att.get('type') or tag).lower()
            self._current_form['input_types'].add(t)
            if t == 'checkbox':
                self._current_form['has_consent_checkbox'] = True

    def handle_endtag(self, tag):
        if tag == 'footer':
            self.in_footer = False
        elif tag == 'title':
            self.in_title = False
        elif tag == 'h1':
            self.in_heading = False
        elif tag == 'form':
            self._current_form = None

    def handle_data(self, data):
        if self.in_title and len(''.join(self.title)) < 200:
            self.title.append(data)
        if self.in_heading and len(''.join(self.headings)) < 300:
            self.headings.append(data)


def validate_root(value):
    try:
        p = urllib.parse.urlsplit(value)
        if (p.scheme != 'https' or p.hostname not in HOSTS or
                p.port not in (None, 443) or p.username or p.password or
                p.path not in ('', '/') or p.query or p.fragment):
            raise ValueError('only the HTTPS origin of the approved ALPHA domain is permitted')
        return f'https://{p.hostname}/'
    except (ValueError, TypeError) as exc:
        raise ValueError('only the HTTPS origin of the approved ALPHA domain is permitted') from exc


def safe_url(base, link):
    """Return only approved HTTPS hostnames without query/fragment.

    DNS answers are supplied by the audit runtime and are not independently
    validated by this URL filter. Do not infer DNS security from hostname checks.
    """
    try:
        dest = urllib.parse.urlsplit(urllib.parse.urljoin(base, link))
        if (dest.scheme != 'https' or dest.hostname not in HOSTS or
                dest.port not in (None, 443) or dest.username or dest.password):
            return None
        if any(ch in dest.path for ch in ('\\', '\x00')):
            return None
        return urllib.parse.urlunsplit(('https', dest.netloc, dest.path or '/', '', ''))
    except (ValueError, TypeError):
        return None


class SameSiteOnly(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        uri = safe_url(req.full_url, newurl)
        if uri is None:
            raise urllib.error.URLError('redirect leaves approved HTTPS website scope')
        return super().redirect_request(req, fp, code, msg, headers, uri)


def classify_transport_error(error):
    """Report safe error categories without exposing private URLs or exception text.

    Failures are localized to this auditor's network/runtime. A DNS failure
    here alone is NOT evidence that the website's authoritative DNS is broken.
    """
    reason = error.reason if isinstance(error, urllib.error.URLError) else error
    if isinstance(reason, ssl.SSLCertVerificationError):
        return 'TLS_CERTIFICATE_VALIDATION_FAILED_IN_AUDIT_RUNTIME'
    if isinstance(reason, socket.gaierror):
        return 'DNS_LOOKUP_FAILED_IN_AUDIT_RUNTIME'
    if isinstance(reason, (socket.timeout, TimeoutError)):
        return 'NETWORK_TIMEOUT_IN_AUDIT_RUNTIME'
    if isinstance(reason, ConnectionRefusedError):
        return 'CONNECTION_REFUSED_IN_AUDIT_RUNTIME'
    if isinstance(reason, ssl.SSLError):
        return 'TLS_HANDSHAKE_FAILED_IN_AUDIT_RUNTIME'
    # This literal is emitted ONLY by SameSiteOnly above. Never copy arbitrary
    # exception text into the public evidence JSON.
    if isinstance(reason, str) and reason == 'redirect leaves approved HTTPS website scope':
        return 'OUT_OF_SCOPE_REDIRECT_REJECTED'
    return 'NETWORK_ACCESS_UNVERIFIED'


def public_get(url, timeout=10, opener=None):
    if safe_url(url, url) != url:
        raise ValueError('unsafe requested URL')
    request = urllib.request.Request(url, headers={'User-Agent': USER_AGENT}, method='GET')
    opener = opener or urllib.request.build_opener(SameSiteOnly())
    try:
        with opener.open(request, timeout=timeout) as resp:
            status = resp.status
            content_type = resp.headers.get('Content-Type', '').split(';')[0].lower().strip()
            data = resp.read(1024 * 1024 + 1)
            if len(data) > 1024 * 1024:
                return {'state': 'INCONCLUSIVE', 'reason': 'response exceeds 1 MiB inspection limit'}
            return {'state': 'RETRIEVED', 'status': status, 'content_type': content_type,
                    'body': data.decode('utf-8', errors='replace') if content_type in ('text/html', 'application/xhtml+xml') else ''}
    except urllib.error.HTTPError as exc:
        return {'state': 'HTTP_ERROR', 'status': exc.code}
    except (OSError, ValueError, TimeoutError) as exc:
        return {'state': 'INACCESSIBLE', 'reason': classify_transport_error(exc)}


def audit(origin, getter=public_get):
    origin = validate_root(origin)
    report = {
        'reference': 'AGM-WEB-STRIPE-20261017-01',
        'target': origin,
        'run_utc': dt.datetime.now(dt.timezone.utc).isoformat(timespec='seconds'),
        'mode': 'READ_ONLY_PUBLIC_GET',
        'website_status': 'INSUFFICIENT_EVIDENCE',
        'legal_adequacy': 'UNVERIFIED',
        'stripe_payments': 'UNVERIFIED',
        'stripe_connect': 'UNVERIFIED',
        'hosted_mobile_acceptance': 'UNVERIFIED',
        'form_submission': 'NOT_PERFORMED',
        'homepage': {},
        'policy_links': {p: {'state': 'UNVERIFIED', 'urls': []} for p in POLICIES},
        'form_inventory': {'count_observed_in_html': 0, 'note': 'JS-rendered/iframe forms not detected by static fetch'},
        'notes': ['No POST, cookies, account login, payment, screenshot or external-domain crawl is performed.'],
    }
    homepage = getter(origin)
    report['homepage'] = {k: v for k, v in homepage.items() if k != 'body'}
    if homepage.get('state') != 'RETRIEVED' or homepage.get('status') != 200 or homepage.get('content_type') not in ('text/html', 'application/xhtml+xml'):
        report['notes'].append('Homepage could not be independently parsed; policy absence is not established.')
        if homepage.get('reason') == 'DNS_LOOKUP_FAILED_IN_AUDIT_RUNTIME':
            report['notes'].append('DNS failure is local to this audit runtime; authoritative domain DNS status remains UNVERIFIED.')
        return report
    parsed = PublicHtml()
    parsed.feed(homepage.get('body', ''))
    report['website_status'] = 'HOMEPAGE_RETRIEVED_NEEDS_REVIEW'
    # Raw page titles/headings can include personal data in dynamic pages.
    report['title_present'] = bool(''.join(parsed.title).strip())
    report['h1_present'] = bool(''.join(parsed.headings).strip())
    report['footer_link_count'] = len(parsed.footer_links)
    report['form_inventory']['count_observed_in_html'] = len(parsed.forms)
    report['form_inventory']['input_type_sets'] = [sorted(x['input_types']) for x in parsed.forms][:15]
    report['form_inventory']['notes'] = 'Observed HTML forms only; no submit, CRM readback or consent inference.'
    collected = set()
    ignored_noncanonical = set()
    for href in parsed.links:
        url = safe_url(origin, href)
        if not url:
            continue
        raw_path = urllib.parse.urlsplit(url).path
        lower = raw_path.lower().replace('-', ' ').replace('_', ' ')
        for policy, tokens in POLICIES.items():
            if not any(re.search(rf'\\b{re.escape(t)}\\b', lower) for t in tokens):
                continue
            # Same-site hostnames do not make every untrusted URL path safe.
            canonical = raw_path.lower().rstrip('/') or '/'
            if canonical not in PUBLIC_POLICY_PATHS[policy]:
                ignored_noncanonical.add(policy)
                continue
            if url not in collected and len(collected) >= 30:
                continue
            collected.add(url)
            report['policy_links'][policy]['urls'].append(url)

    for policy, entry in report['policy_links'].items():
        unique = list(dict.fromkeys(entry['urls']))[:4]
        entry['urls'] = unique
        if unique:
            entry['state'] = 'LINK_DISCOVERED'
            entry['checks'] = [{k: v for k, v in getter(u).items() if k != 'body'} for u in unique]
        elif policy in ignored_noncanonical:
            entry['state'] = 'UNVERIFIED_NONCANONICAL_LINKS_PRESENT'
        else:
            entry['state'] = 'NOT_LINKED_IN_STATIC_HTML'
    report['notes'].append('Unexpected policy paths were not fetched or exported; inspect in authorized GHL browser.')
    report['notes'].append('Visible link reachability does not establish legal adequacy or Stripe approval.')
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', default='https://alphacreativecontent.com/', help='Approved ALPHA HTTPS origin only')
    parser.add_argument('--output', type=pathlib.Path, help='Save sanitized JSON preflight evidence')
    args = parser.parse_args(argv)
    try:
        result = audit(args.url)
    except ValueError as exc:
        parser.error(str(exc))
    payload = json.dumps(result, indent=2, ensure_ascii=False) + '\n'
    if args.output:
        args.output.write_text(payload, encoding='utf-8')
    print(payload, end='')
    return 0 if result['website_status'] == 'HOMEPAGE_RETRIEVED_NEEDS_REVIEW' else 2


if __name__ == '__main__':
    sys.exit(main())
