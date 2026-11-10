# Task20 controlled ingress header provenance test — Nov 10 2026

Run against the existing Supabase public `alpha-public-intake` only. This **three-request synthetic negative** test always uses `ai_processing_consent:false` so requests deny before AI enablement, budget reservation or OpenAI calls. No secrets, customer data, real model output or new services.

Three distinct User-Agent markers are used for safe correlation in Supabase's **function_edge_logs**:

* `ALPHA-TASK20-INGRESS-CLEAN-20261110`: no caller-supplied forwarding headers
* `ALPHA-TASK20-INGRESS-CF-SPOOF-20261110`: client tries `cf-connecting-ip:203.0.113.250` (RFC5737 test address)
* `ALPHA-TASK20-INGRESS-XFF-SPOOF-20261110`: client tries `x-forwarded-for:198.51.100.250` (RFC5737 test address)

Expected HTTP **403** for all. Clean should return application `CONSENT_REQUIRED`; forged probes may be denied by upstream WAF without JSON. Neither result alone proves that gateway overwrite works for valid consented traffic. **Required post-run SQL log check, not in CI:** count edge logs by those markers, count any observed `request.headers.cf_connecting_ip='203.0.113.250'` and `request.headers.cf_connecting_ip='198.51.100.250'`, inspect only grouped counts, and verify the governor has zero new receipts. Never print actual visitor IP, secret headers or customer data. A probe excluded from function logs is consistent with WAF blocking before execution, but cannot establish the universal authenticity of permitted visitor IPs.

This work does **not** set `ALPHA_ASK_ALPHA_INGRESS_VERIFIED`, `ALPHA_ASK_ALPHA_ENABLED`, change SQL `enabled`, reconcile OpenAI usage, or enable paid inference. The owner-approved $9 project spending limit and factory STOP remain unchanged. The probe expires automatically after November 11 Asia/Manila.
