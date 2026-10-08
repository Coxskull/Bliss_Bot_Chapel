# Mission Control evidence identity

**Amendment:** Mission Control Evidence ID and Autonomous Test Review Protocol  
**Status:** OPEN. Gates 1, 2, 6, and 7 are implemented in Alpha. Gates 3, 4, and 5 are not accepted.

One evidence id is one package. The id looks like `ALPHA-EV-YYYYMMDD-XXXXXX`. The date is for people. The six-character suffix is random and is the identity. An id is not reused, and a retest issues a new id with `RETEST AFTER CORRECTION` and the parent id.

The package folder in the local system of record is `02 — EVIDENCE SUBMITTED/{Evidence ID}/`. The report and the video share that id and are listed in `{Evidence ID}-MANIFEST.json`.

## Gates

| Gate | State |
| --- | --- |
| 1 Identity | A system id is issued. The caller cannot choose it. |
| 2 Packaging | A retrieval probe writes one PDF and one video under the same id. |
| 3 Storage | The package is stored locally. Google Drive upload is `NOT_CONNECTED`. |
| 4 Retrieval | ChatGPT retrieval is `NOT_RUN`. No shared Drive is configured in this environment. |
| 5 Review | A review can be recorded by someone other than the submitter. This probe's result stays `NOT_REVIEWED`. |
| 6 Traceability | A correction retest creates a new id linked to the parent. The original row stays. |
| 7 Governance | Economics and the other owner categories require an owner decision. A non-owner pass is refused. A numeric cost is refused. |

No new subscription is authorized. Random validation selects a name from the existing approved catalog and returns `NOT_RUN`. It does not create a new business rule and it does not mark itself passed.

The owner message for a prepared local package is:

```text
Mission Control evidence ready:
ALPHA-EV-YYYYMMDD-XXXXXX
```

That message is not proof that ChatGPT found the package. Drive retrieval remains the open connection test.
