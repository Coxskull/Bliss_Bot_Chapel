# Replacement ingress policy — review and activation approval required

PR #305's environment-wide trust is removed. PostgreSQL and other Railway services
are NOT trusted proxies. The retired Runtime:RailwayIngress:Enabled option refuses
startup rather than silently retaining broad trust.

One existing container/process runs two listeners using pinned Microsoft YARP 2.3.0:
- Public sanitizing proxy: 0.0.0.0:$PORT (8080 currently).
- Authenticated API: 127.0.0.1:8081 only. Never publish this port.
- API Forwarded Headers trusts exactly 127.0.0.1, with zero trusted networks,
  one hop and symmetric scheme/address headers.

The proxy removes incoming Forwarded, X-Real-IP, X-Forwarded-* and X-Original-*.
It sets the backend host from the approved canonical HTTPS origin, scheme=https
from the deployment's TLS contract, and X-Forwarded-For from the real socket peer.
The backend origin is fixed loopback, never caller-controlled. Unknown Host values
are rejected, except GET/HEAD /health/live for platform probes. Credentials, cookies,
request bodies and CSRF headers are passed through; authorization remains at the API.
Proxy request URL/header logging is suppressed to protect OIDC callback codes.

Public Railway HTTPS edge and internal private callers both enter the sanitizing
listener as untrusted clients. Neither can nominate a trusted proxy IP or user IP.
This does not cryptographically identify the Railway edge; the required TLS public
origin remains a provider guarantee. It removes reliance on an undocumented peer-IP
range or on trusting every private service. Local processes inside the same container
are trusted. No new Railway service, paid resource or separate orchestration is needed.

Tradeoff: the application sees the actual transport peer (often a shared Railway
edge), not an unprovable X-Real-IP. Existing IP rate limits may aggregate public users;
they remain enforced. Review rate-limit behavior before expanding beyond internal use.

Recommended configuration AFTER approval:
- Runtime__LoopbackIngress__Enabled=true
- Runtime__LoopbackIngress__PublicOrigin=https://bliss-bot-production-5eb8.up.railway.app
- Runtime__LoopbackIngress__PublicPort=8080 (must match PORT)
- Runtime__LoopbackIngress__BackendPort=8081
- Runtime__ForwardedForHeaderName=X-Forwarded-For (default)
- No Runtime__KnownProxies entries; no old Runtime__RailwayIngress activation
- Railway public domain still targets 8080; no TCP proxy or exposure of 8081

It stays disabled until approved. CI exercises real Kestrel/YARP listeners, forged
headers, incorrect hosts, unauthorized access, request-body/CSRF preservation and
nonloopback backend rejection. Hosted acceptance remains required after deployment.
Keep authentication, VerifyFull database TLS, backup/recovery declarations and
restricted-preview guards unchanged until their own acceptance requirements pass.
