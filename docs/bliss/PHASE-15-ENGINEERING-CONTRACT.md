# Phase 15 Engineering Contract — Export Integrity Digests

## Objective

Attach a SHA-256 digest of canonical domain content to every existing audit
export pack so an operator can verify a downloaded file without trusting
export timestamps or request identifiers.

## Required behavior

1. Hash canonical JSON of domain content only (ledger records or case-file
   entity collections). Omit `exportedAt`, `environment`, `kind`/`ledger`,
   `requestId`, and the digest field itself from the hashed payload.
2. Write the lowercase hex digest to `contentSha256` in the JSON pack.
3. Echo the same value on `X-Content-SHA256`.
4. Apply the digest to ledger packs and match, creator, and campaign case files.
5. Omit the digest header on `404` and unsupported-ledger `400` responses.
6. Show a digest prefix in the existing export toast.
7. Keep the same access policy, record cap, and snapshot/secret omissions.

## Explicit exclusions

- KMS, signed URLs, or detached signature files
- PDF, CSV, or email delivery
- durable object storage or log shipping
- APM or new identity providers
- changes to matching arithmetic, review authority, or Alpha Auto boundaries
- n8n, live affiliates, payments, or crawling

## Acceptance criteria

- a ledger export digest matches SHA-256 of canonical `records` JSON;
- two exports of the same domain content share a digest despite different
  `exportedAt` / `requestId` values;
- match, creator, and campaign case files include a matching header and body
  digest;
- missing or unsupported exports omit `X-Content-SHA256`;
- the frontend toast includes a `sha256:` prefix;
- existing tests continue to pass.
