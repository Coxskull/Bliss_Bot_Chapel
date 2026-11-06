# Bliss Bot Chapel — Zero-credit advertising demonstration

**Scope:** code-only engineering change for the original ALPHA reusable-template workflow.
**Additional spending:** $0. No new subscription, image credits, purchased template, or paid provider.
**Release controls:** global STOP unchanged; delivery `NOT_SENT`; `campaignReady=false`.
**Operational state:** repository implementation, not acceptance that the hosted Railway instance is active.

## Existing architecture reused

- `Bliss.Api/Demonstrations/DemonstrationVideoStudio.cs` generates an original synthetic 1280×720 source slice with FFmpeg and a QR image with QRCoder.
- `Bliss.Domain/Demonstrations/RecipeRuntime.cs` prepares and checks `overlay-1`. Its text, QR, disclosure, and source slice are drawn in the player, not flattened into a permanent prospect MP4.
- `ProspectDemonstrationService.ProduceReferenceAsync` writes only the fictional ABC Pharmacy reference; production remains illustrative, with no provider call, purchase, real endorsement, or delivery.
- `RunFactoryBatch` writes QA/evidence metadata including exceptions, AI-call count and a durable batch ID in the existing demonstration store.

**Do not use** the optional paid native image-generation path `/api/operations/academy/generate` in this workflow. The 50 Academy quality references are not reusable ad assets and must not be copied or sent to image providers.

## Operator flow

1. Open the private Creative Academy `/production.html` page after trusted operator authentication in an operational test environment.
2. Select **Create fictional zero-credit demonstration** in the separately labeled demonstration panel.
3. The browser first reuses an existing *qualified original* Panama City studio slice, or creates one through `POST /api/demonstrations/source-media/studio-slice`.
4. It calls `POST /api/demonstrations/abc-pharmacy/produce` with `conceptCount: 1`, then verifies the `overlay-1` recipe, disclosure, QR same-origin destination, `PLAYER` mode, source association, `aiCalls=0`, `campaignReady=false`, and `delivery=NOT_SENT`.
5. It GETs the served source MP4, QR PNG and destination page to confirm all three are present with the expected content types. It then runs the existing factory QA batch and displays its ID, status, exception count, source SHA-256, and a preview link.
6. If wider-library batch exceptions exist, the UI reports **incomplete factory acceptance**, rather than silently declaring the entire library passed. If the flow fails after demonstration creation, it explicitly distinguishes a produced but not fully accepted sample.

The production workspace does not connect the fictional reference to an advertiser account, spend credits, publish a campaign, open contact roads, or perform outreach. This is an **authorized, manually started demonstration action**, not an automatic or recurring production job.

## Security and availability

- The demonstration button starts disabled and is only enabled for `session.canWrite`; all mutation endpoints retain server-side WritePolicy, rate limiting and the existing restricted-preview/STOP controls.
- The production workspace sends same-origin session antiforgery tokens. The legacy Source Media client has also been corrected to send `X-CSRF-TOKEN` on its JSON mutations and require an authorized operator.
- No new public endpoints, auth bypasses, runtime variables, databases, deployments, payments, or migrations.

## Verification / remaining release acceptance

Run the repository's .NET regression and browser-journey workflows on this change's **exact head**. Check `RecipeRuntimeApiTests` (new no-credit UI and response assertions) plus the preexisting source-media, factory-batch, and recipe tests. Review the created PR before merge.

For an actual hosted demonstration, separately verify: Railway safe release posture and backup/restore, correct ingress forwarding, production OIDC access/CSRF, persistent source/QR store, bounded FFmpeg runtime, QA receipt readback, visual playback, and STOP/no-outreach state. Never remove the restricted-preview gate or mark Bliss commercially operational because an offline demonstration worked.

**Status distinction:** the deterministic source-based ad workflow is implemented in code; full hosted production/advertiser campaign fulfillment is **not yet accepted**.
