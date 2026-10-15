# Creative Academy prototype inbox

The owner uploaded prototype files into `inbox/`. Registration binds each file to the existing 50-niche roster. It does not approve the file.

These images teach how strong an advertisement must look. They are quality benchmarks. They are not podcast placement templates, not inventory geometry, and not advertisements to copy.

## Approved reference roster (verified 2026-10-15)

The source of record is [MANIFEST.tsv](MANIFEST.tsv), not an earlier
registration snapshot. The consolidated catalog decision is documented in
[Consolidated Academy + Catalog Readiness](../../../docs/architecture/CONSOLIDATED-ACADEMY-CATALOG-REPORT.md).

The 50-niche roster is **50/50 UPLOADED and 50/50 ACTIVE**. All 50 expected
files exist in `inbox/`, including `ACA-005-V1.png` (used-car). Every
reference records owner-supplied provenance/rights, human approval metadata,
approved permitted **internal Academy quality-reference use**, and LEARN /
DO NOT COPY guidance. Approved reference **retrieval** does not authorize
transmission of any reference to a model provider, use of the pictured brand,
identity or composition in generated advertising, or campaign delivery.

The initial registration-only inventory of 49 files and pending used-car asset
was superseded by the owner-approved active 50-file manifest on 2026-10-07.
Do not reset ACTIVE rows to CANDIDATE from that superseded snapshot.

The non-destructive repository gate `scripts/verify-academy-manifest.py` checks
file presence, a safe one-to-one mapping, approval metadata and basic actual-file
signature. It does not inspect advertising quality, copyrighted contents,
geographic fidelity or approve any new asset. Those remain separate human gates.

### Owner-confirmed filename bindings

| Reference | Owner file | Niche |
| --- | --- | --- |
| ACA-004-V1 | `Automotive.jpeg` | New-car dealership |
| ACA-014-V1 | `Italian restaurant.jpeg` | Pizza restaurant |
| ACA-033-V1 | `Home renovation.jpeg` | Furniture retailer |
| ACA-044-V1 | `Coffeee.jpeg` | Coffee-brand products |

## Not forced into a niche

These inbox files stay unassigned. They are visible on the catalog page and are not ACTIVE references:

- `Catalog.jpeg`
- `Podcast with ads.jpeg`
- `Podcast with ads 1.jpeg`
- `Podcast with ads 2.jpeg`
- `Podcast with ads 3.jpeg`
- `podcast with ads 4.jpeg`
- `Podcast with ads 5.jpeg`
- `Podcast with ads 6.jpeg`

## What registration does not do

- It does not mark a reference ACTIVE.
- It does not invent `learn` or `doNotCopy`.
- It does not replace the local teachers already stored by the Academy (VidaCare, FreshMart, Nova Fit, Taller Ruta, Brava Moto).
- It does not authorize image generation, a campaign, or a send.

The path to production use is: human review, Alpha approved, then ACTIVE. Only an ACTIVE reference may be retrieved for normal creative production. AI output may not promote itself into this library.
