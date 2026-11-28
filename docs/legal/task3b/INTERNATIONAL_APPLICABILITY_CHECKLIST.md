# Task #3B — jurisdiction-scoped registration privacy requirements

**Directive:** ALPHA-P0-TASK3B-FINAL-RESOLUTION-20261114
**Prepared:** 2026-11-15. **State:** source-backed engineering/legal applicability checklist; **not** a statement of universal compliance, external legal advice or live acceptance.
**Scope:** ALPHA's planned international advertiser-account application. Processing location, establishment, market targeting, personal-data subjects and statutory thresholds must be checked *before a market is enabled*. A business country-code field does not establish territorial applicability.

| Market / conditional framework | Specific source and trigger to assess | Technical/policy acceptance to demonstrate |
|---|---|---|
| Philippines — Data Privacy Act (RA 10173) and NPC IRR | Actual covered processing and controller/processor duties; transparency, purpose limitation, legitimate processing, data-subject rights and bounded retention | Accurate controller contact/purpose/recipient/retention notice; rights intake and lawful deletion/exceptions; evidence of security controls |
| Malaysia — Personal Data Protection Act 2010 (Act 709), as amended | Applicability to commercial transactions and notice-language/notice-choice requirements, including any current amendment commencement | Accurate data-subject notice and choice; language requirements where applicable; access/correction request process, secure retention and disclosure controls |
| Indonesia — Law No. 27/2022 on Personal Data Protection | Territorial reach, controller/processor duty and cross-border requirements of any actual Indonesian processing | Lawful processing notice, purpose and deletion controls, actual requests route and international transfer basis |
| Colombia — Law 1581/2012 | Processing within Colombia or other applicable scope; authorization, rights and data-controller information | Inform data subjects of purposes and mandatory disclosures, retain authorization evidence, provide access/correction/complaints contact |
| Panama — Law 81/2019 | Applicable Panamanian personal data processing, including ARCO rights and local implementing requirements | Scoped notice, identity-checked access/rectification/cancellation/objection handling and lawful transfer/retention |
| Dominican Republic — Law 172-13 | Confirm scope and whether the specific application database falls under covered personal-data records | Validate disclosure, rights and records handling; do not assume the statute covers all operations uniformly |
| Thailand — PDPA B.E. 2562 (2019) | Determine actual territorial scope and operative notice, lawful basis, rights and transfer rules at launch | Market-specific disclosure/language, request channel, consent control where necessary, retention/security |
| EEA / EU — GDPR Regulation (EU) 2016/679 | Check Art 3 territorial reach; Arts 6, 12–14, 15–22, 44+ where triggered | Controller contact, basis per purpose, retention period or criteria, processor/transfer disclosures; generally one-month rights response under Art 12, subject to exceptions |
| New Jersey, USA — NJ Data Privacy Law (P.L. 2023 c.266) | Effective 2025-01-15; assess applicable thresholds, controller status and territorial scope, **not** automatically applicable merely due to LLC formation | If triggered, ensure rights handling, notice and required controls per applicable law |

## Non-negotiable engineering validation

1. Record actual selected markets, applicant eligibility and accepted languages; restrict or hold regions that do not pass their required checks.
2. Maintain one versioned consent disclosure per released registration version, with separate unchecked marketing and auditable changes to the choice.
3. Determine a documented lawful processing basis **per purpose**; do not treat contractual processing and direct marketing as automatically equivalent.
4. Verify actual identity/DB/frontend processor, region, roles, transfer safeguards and retention settings against vendor-account evidence.
5. Before onboarding, establish a working account-specific privacy/support route with identity verification, jurisdiction deadline calculation, recorded acknowledgment, owner escalation and deletion/withdrawal outcomes.
6. Document retention criteria or periods and a lawful exception strategy for append-only receipts, logs, Auth identities and backups. Do not promise deletion that cannot be delivered.
7. Require hosted non-production 201/200/409 registration tests, authorized/unauthorized access checks, pending-review isolation, exact consent version/receipt consistency, and backup/rollback evidence.
8. Only then make an owner-authorized production release decision while global STOP is independently controlled. No blanket external-counsel review gate exists; **a particular mandatory legal obligation** must be evidenced and satisfied.

## Public primary references (verify current amendments/commencements before market launch)

- Philippines NPC — RA 10173: https://privacy.gov.ph/Data-Privacy-Act/
- Philippines NPC — Implementing Rules: https://privacy.gov.ph/implementing-rules-regulations-data-privacy-act-2012/
- Malaysia Commissioner — Act 709 and amendments: https://www.pdp.gov.my/en/akta/pdp-act-2010-en/
- Indonesia — Law 27/2022 official PDF: https://www.postel.go.id/downloads/12/20250208104615-UU_2022-27_PDP.pdf
- Colombia — Law 1581/2012 official legal compilation: https://www.cancilleria.gov.co/normograma/compilacion/docs/ley_1581_2012.htm
- Panama ANTAI — Law 81/2019: https://www.antai.gob.pa/preguntas-frecuentes-proteccion-de-datos-personales/
- Dominican Republic — Law 172-13: https://www.sb.gob.do/regulacion/leyes/ley-no-172-13-proteccion-de-los-datos/
- EU — GDPR (EUR-Lex): https://eur-lex.europa.eu/eli/reg/2016/679/
- New Jersey Division of Consumer Affairs — NJDPL: https://www.njconsumeraffairs.gov/ocp/Pages/NJ-Data-Privacy-Law-FAQ.aspx

**Evidence limit:** This is a law-and-controls crosswalk; it does not prove compliance in any jurisdiction, identify the actual app database or constitute hosted acceptance.
