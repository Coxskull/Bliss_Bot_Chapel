# PHASE-1-REQUIREMENTS

Phase 1 must prove the following requirement IDs.

| ID | Description |
| --- | --- |
| BLC-ARCH-001 | Creator supports many ContentItems. |
| BLC-ARCH-002 | ContentItem supports many AdInventorySlots. |
| BLC-ARCH-003 | Creator supports many simultaneous BlissMatches. |
| BLC-ARCH-004 | A new BlissMatch does not overwrite another valid BlissMatch. |
| BLC-ADV-001 | Advertiser ≠ Program ≠ Opportunity. |
| BLC-ACCESS-001 | Network access and individual Program approval are persisted separately. |
| BLC-DATA-001 | Important demographic data preserves source, confidence and collection timestamp. |
| BLC-DATA-002 | UNKNOWN data must not automatically be treated as zero compatibility. |
| BLC-HIST-001 | Historical Match evaluations preserve their Rule Version. |
| BLC-HIST-002 | Changing future rules must not silently alter historical Match Certificates. |
| BLC-MULTI-001 | No ContentItem-level relationship may impose one-advertiser-only behavior. |
| BLC-MULTI-002 | Multiple independent CampaignPlacements must be architecturally possible for one ContentItem. |
| BLC-PLACE-001 | BlissMatch compatibility is separate from placement eligibility. |
| BLC-PROV-001 | Important normalized intelligence retains provenance. |
| BLC-AI-001 | AI must not become the authority for deterministic Match arithmetic. |

## Exclusions (later phases)

- AI / ML matching engines
- Behavioral scoring
- Creator Scout / automated discovery
- n8n and live affiliate/ad APIs
- Tracking, ledger, payouts, payments
- Wedding Planner / production automation
- Advanced authentication
- Automatic campaign placement
