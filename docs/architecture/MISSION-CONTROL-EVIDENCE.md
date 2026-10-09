# Mission Control evidence identity

**Amendment:** Mission Control Evidence ID and Autonomous Test Review Protocol  
**Status:** OPEN. Gates 1, 2, 6, and 7 are implemented in Alpha. Gates 3, 4, and 5 are not accepted.

One evidence id is one package. The id looks like `ALPHA-EV-YYYYMMDD-XXXXXX`. The date is for people. The six-character suffix is random and is the identity. An id is not reused. A retest is refused until the parent review is `CORRECTION_REQUIRED` or `RETEST_REQUIRED`. The new id records `RETEST AFTER CORRECTION` and the parent id.

The package folder in the local system of record is `02 — EVIDENCE SUBMITTED/{Evidence ID}/`. The report and the video share that id and are listed in `{Evidence ID}-MANIFEST.json`.

## Gates

| Gate | State |
| --- | --- |
| 1 Identity | A system id is issued. The caller cannot choose it. |
| 2 Packaging | A retrieval probe writes one PDF and one video under the same id. |
| 3 Storage | The package is stored locally. Google Drive upload is `NOT_CONNECTED`. |
| 4 Retrieval | ChatGPT retrieval is `NOT_RUN`. No shared Drive is configured in this environment. |
| 5 Review | A review can be recorded by someone other than the submitter. This probe's result stays `NOT_REVIEWED`. |
| 6 Traceability | A retest is refused until the parent review is `CORRECTION_REQUIRED` or `RETEST_REQUIRED`. The new id links to the parent. The original row stays. |
| 7 Governance | Economics and the other owner categories require an owner decision. A non-owner pass is refused. A numeric cost is refused. |

No new subscription is authorized. Selecting a name from the existing approved catalog returns `NOT_RUN` and does not create a package. Running one approved check executes that existing check, stores the observation as a log under a new evidence id, and leaves the review `NOT_REVIEWED`. A held check is claimed `OBSERVED`. A check that does not hold is claimed `FAIL`. Neither claim is a review, and neither authorizes a price. Drive upload stays `NOT_CONNECTED`.

The owner message for a prepared local package is:

```text
Mission Control evidence ready:
ALPHA-EV-YYYYMMDD-XXXXXX
```

That message is not proof that ChatGPT found the package. Drive retrieval remains the open connection test.

Preparing an upload folder writes `ALPHA — ERWIN ↔ CHATGPT MISSION CONTROL` on this machine. The retrieval package, with its report and video, is placed in `02 — EVIDENCE SUBMITTED/{Evidence ID}/`. A recorded correction is placed in `04 — CORRECTIONS REQUIRED`. The folder is not a Google Drive upload. `uploadPerformed` stays false.
