# Creative Academy prototype inbox

Upload the 50 approved quality prototypes here.

These images teach how strong an advertisement must look. They are quality benchmarks. They are not podcast placement templates, not inventory geometry, and not advertisements to copy.

## How to upload

1. Save each prototype as `ACA-001-V1.png` through `ACA-050-V1.png`. JPEG or WebP is acceptable when the extension matches the file. Prefer PNG.
2. Put the file in `inbox/`.
3. In `MANIFEST.tsv`, set that row's `status` from `AWAITING_UPLOAD` to `UPLOADED`.
4. Leave `learn` and `doNotCopy` blank until a human writes them. Do not let a model fill those fields unsupervised.

`MANIFEST.tsv` already maps each reference ID to the existing 50-niche roster in niche order. Niche 1 is `ACA-001-V1` (pharmacy). Niche 50 is `ACA-050-V1` (household cleaning). If a supplied prototype belongs to a different niche, change `nicheKey` and `nicheName` on that row. Do not invent a 51st niche.

## What upload does not do

- It does not mark the reference ACTIVE.
- It does not replace the local teachers already stored by the Academy (VidaCare, FreshMart, Nova Fit, Taller Ruta, Brava Moto) until a human connects that file to the reference record.
- It does not authorize image generation, a campaign, or a send.

Lifecycle for a new file starts at `CANDIDATE`. The path to production use is: human review, Alpha approved, then ACTIVE. Only an ACTIVE reference may be retrieved for normal creative production. AI output may not promote itself into this library.
