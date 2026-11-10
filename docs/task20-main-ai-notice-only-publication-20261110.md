# Task20 production website AI notice-only release, November 10 2026

**Scope:** This cherry-picks only the existing reviewed text of the optional AI-processing notice from the tested Task20 integration branch into current production `main`, and adds one same-site link to it from the existing scripted Ask Alpha guide. It deliberately does **not** cherry-pick the integration branch's optional AI UI, consent checkbox, or JavaScript. Ask Alpha remains a scripted guide on the public website until independently verified public inference release.

**Existing form policy retained:** `AGM-FULL-SPECTRUM-BUDGET-2026-11-08-V1`. AI-specific notice version remains `AGM-ASK-ALPHA-AI-2026-11-08`. The AI section explicitly describes optional use *only after* future AI consent, provider disclosure, storage, retention exception for unresolved financial receipts and human contact route. Existing contact, forms, terms and registration are unchanged. This is a source and public publication action, not legal sign-off.

**Release guard:** Public website CI must pass `task19-public-cta-smoke.mjs` and new Task20 notice-only source tests. Verify Vercel deployment targets only `Coxskull/Bliss_Bot_Chapel` `main`, existing team/project, and is READY before treating it as published. No SQL/Edge migrations or provider requests, no private secrets and no budget changes. If the preview or production build fails, leave `main` unchanged.

**Open:** Provider private key/project/model proof, managed gateway allowed-path assurance, independent privacy/support acceptance and a bounded paid AI inference with durable settlement receipt. Owner approved controlled activation; these gates are not automatically cleared by notice publication.
