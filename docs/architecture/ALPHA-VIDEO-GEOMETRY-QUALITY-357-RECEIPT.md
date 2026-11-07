# ALPHA GLOBAL MEDIA LLC — VIDEO GEOMETRY QUALITY 357

**DIRECTIVE_ID:** `ALPHA-VIDEO-GEOMETRY-QUALITY-357-M2M`  
**Date:** 2026-11-07  
**Execution:** offline source refinement; no GitHub merge, deployment, payment, external outreach or STOP transition.

## Evidence basis and source identity

- **SOURCE_VIDEO_INSPECTED: YES (supplemental direct review, 2026-11-07).** The owner subsequently uploaded `ALPHA-Creative-Academy-System-Screen-Recording (1).mp4`; it was probed, decoded and sampled at multiple timestamps. SHA-256 `462669cf3bf2ca3d52af44c5cdee446fa4bc7a0d7b44a1e01389e4f79b081bb3`. It is a **1440×1000 screen recording**, 36.416667 seconds, 24 fps, 874 frames (H.264), **not** an original encoded 1920×1080 ALPHA video export. The fullscreen embedded demonstration approximates a **1440×810 displayed 16:9 area** at screenshot Y≈95–905; original output slot coordinates are inferred from the screen capture and independently cross-checked against the existing source. No pixels are claimed to be native render-space pixels.
- **EXISTING_RENDERER_IDENTIFIED:** Yes: the earlier standalone `ALPHA-Advertising-Real-Estate-Try-Offline.html` retrieved from the connected file library. It contains `AlphaTemplateComposer.render`, one animated fictional podcast canvas, and `AlphaAdvertisingRealEstate` placement/compositing logic. This is a real existing offline browser Composer, not the unrelated `DemonstrationVideoStudio.CompositeAsync` (which uses an older 1280×720 marketing-card video design).
- The repaired version of the **same existing offline Composer** is checked into `assets/alpha-prototypes/advertising-real-estate/ALPHA-Composer-Quality-357-Offline.html`. It is not wired to production; no new renderer is introduced.
- The original offline source accepted the three layouts, but its six-ad side panels occupied only Y=270–810 while the center band is Y=180–900: four unintended outer 270×90 strips, **97,200 blank pixels**. It also stretched 1280×720 landscape artwork directly into 270×540 vertical ad placements and used inset per-panel borders.

## Actual offline refinement

- Full canvas **1920×1080** with exactly **one continuous fictional animated podcast**, not replicated in ad panels.
- **2 ads:** bottom left (0,810,960,270), bottom right (960,810,960,270). Protected creator: (0,0,1920,810).
- **4 ads:** top (0,0,960,180) and (960,0,960,180), bottom (0,900,960,180) and (960,900,960,180). Protected creator: (0,180,1920,720).
- **6 ads:** four horizontal as above; left (0,180,270,720), right (1650,180,270,720). Protected creator: (270,180,1380,720). Side ads now abut top and bottom bands. This intentionally replaces the **superseded 270×540 six-side V2 geometry** from the earlier conceptual illustration.
- **Unintentional gaps:** zero pixels in the revised exact canvas/slot mask.
- **Overlaps:** zero pixels. **Out-of-bounds:** zero. **Creator-safe intrusions:** zero.
- **Purpose-based size mismatches:** zero (all paired top/bottom banners match; two sides equal).
- **Shared divider thickness:** four pixels, rendered only once at each advertiser/advertiser boundary; no ad/creator divider crosses into the protected podcast. Original doubled inset outlines removed.
- **Artwork sizing:** the Composer keeps the original underlying `AlphaTemplateComposer.render`, uses aspect-preserving containment for texture, and blurs it behind separately rendered legible brand/category text. No stretched/cropped landscape art in side panels.
- **Text fit:** per-slot visible brand, category, and headline labels use `measureText` and decrease font size until the text fits, otherwise fail rather than squeeze/crop. Fixture brand text was visually checked in sampled rendered frames. This is **not** a proof for arbitrary future text, logos or video inputs.
- The offline Composer's demo-disclosure strip was moved out of the creator viewing region; each advertiser panel retains a `FICTIONAL DEMO` notice.

## Recorded output evidence (local, fictional, not public)

| Layout | WebM | Stream pixels | Decoded frames | SHA-256 |
| --- | --- | --- | ---: | --- |
| 2 | `ALPHA-357-Demo-2-rectangles.webm` | 1920×1080 | 29 | `10d9cdeb1fabab3becd20f4c7c4df586fbcdb907779bede731cd8e6e41a268d3` |
| 4 | `ALPHA-357-Demo-4-rectangles.webm` | 1920×1080 | 31 | `bb092acb3845498abb0e412813e7ed8337c4ac9debb29f487e25e3c8c69f9f4d` |
| 6 | `ALPHA-357-Demo-6-rectangles.webm` | 1920×1080 | 28 | `0be21c59c8eb66763ff9cbe07f1dc7dbe99b7d7fcd0909d39d11881edfe42387` |

Actual Chromium canvas `MediaRecorder` VP9 WebM outputs were captured offline and independently decoded by FFmpeg. Three distinct frames sampled at ~0.3/1.1/1.9 seconds for each video. Measured creator-region per-pixel frame-change signal confirms motion; the video is not one repeated still. In the six-layout video, decoded pixel samples in **all previously blank edge strips** show the expected colored advertiser side area.

Actual uncompressed frame PNGs: `ALPHA-357-Frame-2-rectangles.png`, `ALPHA-357-Frame-4-rectangles.png`, `ALPHA-357-Frame-6-rectangles.png`. An actual full-resolution divider-pixel check passed for top-band seams in the 4- and 6-ad layouts.

The outputs and detailed `ALPHA-357-geometry-evidence.json` are delivered in the conversation attachment bundle, **not checked into the repo**. Do not invent GitHub artifact URLs for those local media files.

## Test methods

1. Existing Composer source executed in **headless Chromium** by Playwright (loaded directly in memory, no external resources).
2. Exact pixel-occupancy masks with NumPy / Pillow: every pixel belongs to exactly one ad slot or the protected creator region, for all three layouts; **zero gaps, overlaps and protected content violations**.
3. `ffprobe -v error -count_frames -show_entries stream=width,height,codec_name,nb_read_frames -of json <file.webm>` for each actual video: 1920×1080 VP9, 29/31/28 decoded frames.
4. `ffmpeg -ss 0.3|1.1|1.9 -i <file.webm> -frames:v 1 <frame.png>`, direct decoded pixel-sample verification and creator-region motion checks.
5. Node focused geometry test: `node --test scripts/alpha357-composer-geometry.test.mjs`; C# focused tests: `dotnet test Bliss.Tests/Bliss.Tests.csproj --filter FullyQualifiedName~PodcastAdvertisingGeometryTests` in CI.

## Direct inspection of the Captain-reviewed screen recording (new evidence)

**Input:** `ALPHA-Creative-Academy-System-Screen-Recording (1).mp4`, supplied by the operator in this conversation. SHA-256 `462669cf3bf2ca3d52af44c5cdee446fa4bc7a0d7b44a1e01389e4f79b081bb3`. It shows the existing ALPHA Composer interface and a subsequent fullscreen presentation of the original fictional rendered output. No transcript, credentials or external private data were exported.

- `ffprobe -v error -show_entries format=duration,size:stream=codec_name,width,height,avg_frame_rate,nb_frames -of json <uploaded-file>` → **H.264 / 1440×1000 screen recording / 36.416667 s / 24 fps / 874 frames**.
- `ffmpeg -ss 27|31|35 -i <uploaded-file> -frames:v 1 ...` → fullscreen **2 / 4 / 6** ad layout reference frames. Earlier 3 / 7 / 11 second screenshots show the same original configurations inside the Composer web page.
- The fullscreen on-screen 16:9 demonstration spans approximately **X=0..1439, Y=95..904** screen-capture pixels, a **0.75× display of a 1920×1080 16:9 canvas**. The 1440×1000 recording size is not an instruction to change the output compositor canvas.
- **Two ads:** bottom split occupies the lower approximately 203px of the displayed 810px high region; advertiser boundary lies at displayed X≈720. **No material horizontal seam gap observed**.
- **Four ads:** two uniform top ads and two uniform bottom ads, each displayed band approximately 135px high and horizontally split at X≈720. The center podcast is one continuous field.
- **Six ads:** the top and bottom bands remain continuous, but the left/right side panels appear only at approximately **displayed X=0..202 and X=1238..1439; Y≈298..700**, while the entire side advertising span should reach **Y≈230..770**. Thus **four unused side/perimeter strips** remain visible in the original recording. They correspond to **four 270×90 gaps = 97,200 native-canvas pixels** using the original 1920×1080 source geometry. The source-video pixels are compressed and scaled; the 97,200 figure is derived from the source geometry and visually corroborated, not obtained by treating screen pixels as original native pixels.
- The original also contains faint duplicate/ghosted words from the landscape source artwork inside several advertiser bands and stretched/low-contrast text in its portrait placements.
- **Repaired comparison:** each of the three independent offline H.264 MP4s was `ffprobe`-checked at **1920×1080** (34 / 33 / 33 decoded frames respectively), and actual decoded images were inspected. The six-ad side panels now continuously span **Y=180..900**, directly adjoining the 180px top and bottom rows. Fixture artwork text is separately fitted and the previously visible ghost/stretched copy is not present in checked frames.
- An operator review aid, `ALPHA-357-Captain-Reviewed-Video-Before-After.png`, is available in this conversation. It juxtaposes the **actual original six-ad screen-recording frame**, cropped to its 16:9 player region, and an **actual repaired six-ad MP4 frame**, resized by 0.75× for fair comparison. Four red boxes on the source highlight the four unused strips. It is **evidence assembled from decoded frames**, not an invented substitute reference image.

**Comparison scope:** source-specific geometry and obvious visual defects corrected in the tested fictional videos. It does **not** establish merchant-readiness, arbitrary dynamic template readability, rights to real creator footage, or any deployed production integration. Current `RendererIntegrated=false` remains accurate.

## Remaining blockers and acceptance

- **SOURCE VIDEO INSPECTED = YES.** Original full-screen examples at approximately 27s (two), 31s (four), and 35s (six) were directly decoded and compared with the repaired synthetic MP4 exports. The Captain's revised-video **human visual signoff** is still separately unrecorded.
- Production video composition, real creator footage, arbitrary artwork readability, permission verification and live 2/4/6 generation **not yet validated**. The repaired module is a reproducible **offline original Composer**, not production deployment.
- Real browser visual judgment for arbitrary advertisers and screen sizes remains owner QA, not replaced by source/CI assertions.
- `PodcastAdvertisingGeometry` is updated on this **unmerged branch** to complete six-ad coverage; source reports `RendererIntegrated=false` until actual production binding is verified.
- **FINAL_ACCEPTANCE_STATUS:** `OFFLINE_FICTIONAL_GEOMETRY_AND_RENDER_QA_PASS; CAPTAIN_REVIEWED_SOURCE_VIDEO_COMPARISON_COMPLETED; REPAIRED_VIDEO_OWNER_SIGNOFF_UNRECORDED; PRODUCTION_INTEGRATION_UNVERIFIED`.

**HARD BOUNDARIES:** $0 incremental spend. Global STOP stays ENGAGED. No merge, deployment, publishing, outreach or payment activation.
