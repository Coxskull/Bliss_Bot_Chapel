# n8n workflow templates

These files are importable orchestration templates, not active
production workflows and not business authority.

## Economics public research staging

`workflows/economics-public-research-staging.json`

Before activation, configure:

- `BLISS_API_BASE`
- `APPROVED_RESEARCH_ENDPOINT`
- `APPROVED_AI_EXTRACTION_ENDPOINT`
- an n8n HTTP-header credential named `Bliss service credential`
- provider-specific authentication on the approved endpoint nodes

The workflow is intentionally inactive and contains no secret values.
It receives a bounded research-run id, loads context from .NET, calls
environment-configured providers, rejects AI claims of `VERIFIED` or
`HIGH`, and submits an untrusted candidate to the .NET staging API.

It must never:

- write PostgreSQL directly
- promote or verify an observation
- calculate recommendations or quotes
- allocate compensation
- create settlement or payment records

Retries and execution logs may live in n8n. Permanent business and
research history lives in PostgreSQL through the .NET API.
