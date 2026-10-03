# Engineering Contract: Player-served picture

**Status:** the priority contract after the subscription ledger.
It does not open discovery, send, or a new subscription. Bliss hosted
acceptance and Economics Phase 9 stay on their own paths.

## Objective

Make recipe `overlay-1` the only served picture. Stop writing a
permanent per-prospect MP4.

## AI capability

None. The overlay text, QR, and disclosure are already stored. Drawing
them on the approved source slice is ordinary software.

## Deterministic capability

QA of the QR destination and the disclosure. The player. The QR file.
The factory manifest.

## Existing reusable assets

Recipe `overlay-1`, the approved source slice, the in-process QR, and
the demonstration page.

## Required provider

None. FFmpeg remains the tool that makes the source slice. It is not
called to flatten a per-prospect composite.

## Subscription impact

`FREE_SELF_HOSTED`. No new row. Nothing is purchased.

## Build-versus-buy review

The page can draw the stored recipe. A video suite is not required.
Generative video is rejected for this routine overlay.

## Estimated usage cost

Zero model calls. Compute cost stays `UNRECORDED`.

## Maximum cost

No new spend. The factory manifest still records zero AI calls and no
dollar amount.

## Inputs

A stored recipe that passes QA, its approved source slice, and its QR.

## Outputs

The demonstration page. No per-prospect MP4. `servedPicture` is
`PLAYER`. The factory manifest does not require a flattened file for
`overlay-1`.

## Cache policy

The recipe is stored and read. The request does not rebuild it. The QR
file stays. A missing older picture, from before `overlay-1`, is still
an exception.

## Failure handling

A recipe that fails QA writes no picture. A missing source slice or a
QR that leaves the prospect page stays a failure. Delivery remains
`NOT_SENT`.

## Authorization

The demonstration page is the reader. This contract cannot send, price,
or invent a person.

## Persistence

The concept row keeps an empty video file name. PostgreSQL is unchanged.
The demonstration store keeps the recipe and the QR file.

## Tests

`RecipeRuntimeTests`, `FactoryBatchTests`, `RecipeRuntimeApiTests`, and
`ProspectDemonstrationApiTests`.

## Evidence

`docs/architecture/PLAYER-SERVED-PICTURE-EVIDENCE.md` and the PDF report
beside it.

## Acceptance criteria

- The player and QA name the same QR destination and disclosure.
- A passing `overlay-1` recipe writes no permanent composite.
- The video route for that concept is not found.
- The source slice and the QR file are served.
- The factory manifest passes without a flattened file.
- An older picture that is not `overlay-1` still reports a missing file.
- AI calls stay zero. Delivery remains `NOT_SENT`.

## Out of scope

Generative video. Per-prospect websites. Hosted Bliss acceptance.
Economics Phase 9. Discovery crawlers. Live send. A new subscription.

## The five questions

Why does this need AI? It does not.

Can software do it cheaper? Yes. The browser draws the stored recipe.

Can we reuse something? Yes. The source slice, the recipe, and the QR.

Do we already pay for this capability? The application is
`FREE_SELF_HOSTED`.

Do we actually need a new subscription? No.
