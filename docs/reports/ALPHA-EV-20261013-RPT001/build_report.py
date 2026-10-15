#!/usr/bin/env python3
"""Render the client report from its retained, versioned evidence package."""
from pathlib import Path
import csv, json, hashlib, math
from html import escape
from reportlab.pdfgen import canvas
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Image, KeepTogether
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.graphics.shapes import Drawing, Rect, String, Line

ROOT=Path(__file__).resolve().parent
META=json.loads((ROOT/'report-metadata.json').read_text())
ID=META['report_id']; PDF=ROOT/(ID+'.pdf')
FONT=Path('/usr/share/fonts/truetype/dejavu')
for name,file in [('Body','DejaVuSans.ttf'),('Bold','DejaVuSans-Bold.ttf'),('Mono','DejaVuSansMono.ttf')]:pdfmetrics.registerFont(TTFont(name,str(FONT/file)))
pdfmetrics.registerFontFamily('Body',normal='Body',bold='Bold',italic='Body',boldItalic='Bold')
PLUM=colors.HexColor('#27183D'); VIOLET=colors.HexColor('#6C42AA'); GOLD=colors.HexColor('#B8924B'); INK=colors.HexColor('#283443'); MUTED=colors.HexColor('#627082'); PALE=colors.HexColor('#F3EFF8'); LINE=colors.HexColor('#DED7E8'); GREEN=colors.HexColor('#216B59')
W=511
styles={
 'body':ParagraphStyle('body',fontName='Body',fontSize=9.5,leading=14,textColor=INK,spaceAfter=8),
 'small':ParagraphStyle('small',fontName='Body',fontSize=8,leading=11.4,textColor=MUTED,spaceAfter=6),
 'h1':ParagraphStyle('h1',fontName='Bold',fontSize=25,leading=30,textColor=PLUM,spaceAfter=14),
 'h2':ParagraphStyle('h2',fontName='Bold',fontSize=12.3,leading=17,textColor=PLUM,spaceBefore=12,spaceAfter=7),
 'eyebrow':ParagraphStyle('eyebrow',fontName='Bold',fontSize=8.6,leading=12,textColor=VIOLET,spaceAfter=9),
 'title':ParagraphStyle('title',fontName='Bold',fontSize=36,leading=41,textColor=PLUM,spaceAfter=15),
 'table':ParagraphStyle('table',fontName='Body',fontSize=8.2,leading=11.6,textColor=INK),
 'th':ParagraphStyle('th',fontName='Bold',fontSize=8.1,leading=11.5,textColor=colors.white),
 'mono':ParagraphStyle('mono',fontName='Mono',fontSize=7.6,leading=11,textColor=INK,wordWrap='CJK'),
 'path':ParagraphStyle('path',fontName='Mono',fontSize=7,leading=10.2,textColor=INK,wordWrap='CJK'),
}
story=[]
def p(t,style='body'):return Paragraph(t,styles[style])
def add(t,style='body'):story.append(p(t,style))
def heading(num,title,ref=None):
 add(f'{num} / DELIVERY &amp; EVIDENCE', 'eyebrow');add(title,'h1')
 if ref:add(ref,'small')
def page():story.append(PageBreak())
def table(rows,widths=None,small=False):
 data=[[p(escape(str(v)), 'th' if i==0 else ('path' if small else 'table')) for v in row] for i,row in enumerate(rows)]
 t=Table(data,colWidths=widths or [W/len(rows[0])]*len(rows[0]),hAlign='LEFT',repeatRows=1)
 t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),PLUM),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),4 if small else 7),('BOTTOMPADDING',(0,0),(-1,-1),4 if small else 7),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,PALE]),('LINEBELOW',(0,0),(-1,0),.7,GOLD),('LINEBELOW',(0,1),(-1,-1),.25,LINE)]))
 story.append(t);story.append(Spacer(1,10))
def box(title,text):
 t=Table([[p(title,'h2')],[p(text)]],colWidths=[W]);t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,-1),PALE),('BOX',(0,0),(-1,-1),.6,LINE),('LEFTPADDING',(0,0),(-1,-1),13),('RIGHTPADDING',(0,0),(-1,-1),13),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),8)]));story.append(t);story.append(Spacer(1,10))
def screenshot(name,caption):
 image=Image(str(ROOT/'assets'/f'{name}.png'),width=W,height=W*800/1280)
 story.append(image);story.append(Spacer(1,5));add(caption,'small')
def bullets(items):
 for item in items:add('• '+item)
def footer(c,doc):
 c.setStrokeColor(LINE);c.line(42,803,553,803);c.setFillColor(PLUM);c.setFont('Bold',8);c.drawString(42,813,'ALPHA  /  BLISS CHAPEL')
 c.setFont('Body',7.2);c.setFillColor(MUTED);c.drawRightString(553,813,ID)
 c.line(42,37,553,37);c.drawString(42,24,'Work: 12 Oct 2026  •  Issued: 13 Oct 2026  •  Asia/Taipei');c.drawRightString(553,24,f'{doc.page}')

add('CLIENT DELIVERY REPORT  /  DEVELOPMENT PHASES 1–6','eyebrow')
story.append(Spacer(1,20))
add('From brand intake<br/>to campaign evidence','title')
add('Bliss Bot · Creative Academy · Wedding Planner','h2')
add('Consolidated creation, updates, testing and recovery evidence for the development work completed on 12 October 2026. Issued on 13 October 2026 for client reporting.')
box('Report ID: '+ID,'All six development phases are implemented and locally validated. The completed workflow connects confirmed brand direction, creative production, human approval, planned placements and reviewed research. Live AI and hosted deployment acceptance remain outstanding.')
metrics=[['6 phases','673 tests','81 added cases'],['Implemented locally','Final regression passed','Above the 592-test baseline']]
t=Table([[p(v,'h2') for v in metrics[0]],[p(v,'small') for v in metrics[1]]],colWidths=[W/3]*3);t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,-1),PALE),('LEFTPADDING',(0,0),(-1,-1),12),('BOTTOMPADDING',(0,-1),(-1,-1),10)]));story.append(t);story.append(Spacer(1,20))
add('Delivery at a glance','h2')
bullets(['Five new workspace pages, five database migrations and 13 new tables support the end-to-end workflow.','100 product, test, script and documentation files created or updated; the full inventory appears in the appendix.','Final Release build: zero warnings and zero errors. Local recovery reproduced evidence across 15 tables.','External model calls: zero. Recorded external model spend: $0. Campaigns remain planned and delivery remains NOT_SENT.'])
add('Prepared from repository source, phase evidence, retained execution logs and local interface captures. This report records development evidence; it does not represent client sign-off or production certification.','small')
page()
heading('01','Delivery status and scope','Evidence references E01–E13 resolve to the retained register on page 13.')
table([['Phase','Delivered outcome','Validation / open item'],['1 · Foundation','Reliable message retry handling and preserved audit history.','596 tests; real local HTTP/database check.'],['2 · Brand DNA','Resumable advertiser intake and explicit versioned confirmation.','602 tests; browser and migration checks.'],['3 · Production','Saved briefs, private candidate revisions and human assessments.','612 tests; browser, PNG and concurrency checks.'],['4 · Controlled AI','Provider adapters, bounded workers, budgets and durable draft history.','633 tests; local HTTPS fixture. Live provider deferred.'],['5 · Handoff','Independent approvals, inventory fit and accepted-quote planning.','654 tests; browser, migration and atomic planning checks.'],['6 · Measurement','Frozen outcome lineage, reviewed research and readiness reporting.','673 tests; browser and local restore. Hosted acceptance pending.']],[85,223,203])
add('How the modules now connect','h2')
add('<b>Wedding Planner</b> captures advertiser context and confirmed Brand DNA. <b>Creative Academy</b> produces versioned briefs and candidate assets with human assessments. <b>Bliss Bot</b> supplies approved matching and existing placement authority. <b>Economics</b> remains the source of accepted quote amounts. Measurement preserves the exact evidence used at planning time.')
box('Connected workflow','Intake → confirmed Brand DNA → brief → candidate → instructor assessment → advertiser approval + independent QA + inventory fit → accepted quote → planned handoff → sourced observation → reviewed research.')
add('Scope clarity','h2')
add('“Creator Academy” was confirmed to mean the existing Creative Academy module. The six phases in this report are delivery increments, not a renumbering of the repository’s older Bliss, Economics or Wedding Planner phases. Existing matching, pricing and production-posture safeguards were reused and extended where described.')
page()
heading('02','Foundation and cloud environment','E01 · E02  |  Phase 1 — reliability and repeatable development')
add('The cloud workspace was prepared to build, test and run the existing application with local PostgreSQL and browser validation. Reusable activation/install/start helpers and saved setup instructions were established during onboarding. Repository source edits then proceeded under the user’s phase authorizations.')
table([['Capability','Prepared / observed'],['Application build','.NET SDK 8.0.425; Release compilation and EF migrations.'],['Database','Local PostgreSQL 17.11 with separate development/test databases.'],['Browser','Installed Chromium and Playwright; desktop and mobile checks.'],['Media and setup','FFmpeg/fonts; retained package caches; repeatable service startup.']],[145,366])
add('Message integrity corrected','h2')
add('Previously, reusing a message request key with different text or actor type could return the old message as a successful replay. Phase 1 returns HTTP 409 for changed input and keeps exact normalized retries successful. A key from another session remains inaccessible.')
bullets(['Rejected retries preserve the original message, session timestamp and append-audit count.','Existing ownership and actor checks run before retry handling.','A real local API/database exercise verified create 201, changed-text/actor 409, exact replay 200, and preserved stored content.'])
box('Validation result','596 passed, 0 failed, 0 skipped. Four new cases above the 592-test baseline. No Phase 1 schema migration was required.')
add('Environment setup is a local capability proof. It does not establish hosted identity access, production database transport, deployment capacity, or a running paid AI service.','small')
page()
heading('03','Resumable advertiser intake','E03  |  Phase 2 — Brand DNA')
bullets(['A dedicated intake page saves partial drafts and resumes them from the authorized workspace. Required fields are checked before explicit human confirmation.','Every draft or confirmation is a new revision. Later edits preserve earlier confirmed direction, with immutable application history and corresponding audit records.','Version checks protect against stale tabs; exact retry keys replay, changed payloads conflict, and PostgreSQL constraints arbitrate simultaneous saves.'])
add('<b>Validation:</b> 602 tests passed. Browser checks covered save/resume, confirmation refusal, version history, safe text rendering, stale-tab conflicts and mobile layout. Migration added one Brand DNA table.')
screenshot('brand-intake','Local application capture during report preparation. Fictional development advertiser; upper portion of the working page. Functional proof is E03, not the screenshot alone.')
add('Brand confirmation approves direction. Creative approval remains a separate later decision.','small')
page()
heading('04','Creative production workspace','E04  |  Phase 3 — Creative Academy')
bullets(['Confirmed Brand DNA becomes a saved production brief with exact DNA/reference versions, reference file hash, instructions and recipe provenance. Unsupported reference families are refused.','Private PNG candidates retain bytes, hashes, labels and revision history. Authorized image routes use private/no-store caching. PNG structure checks do not substitute for visual assessment.','Human PASS/REVISE/FAIL assessments preserve four quality gates, critical defects and repair instructions. Advertisers cannot act as instructors.'])
add('<b>Validation:</b> 612 tests passed. Browser checks verified provenance, private image loading, failed-gate refusal, REVISE history, fresh candidate revisions and concurrent/stale-tab behavior. Three new tables preserve briefs, candidates and reviews.')
screenshot('production','Local development page with a fictional saved brief. The screenshot is a UI record; synthetic candidate tests do not establish commercial creative quality.')
page()
heading('05','Controlled AI integration','E05  |  Phase 4 — implemented; live-provider validation deferred')
bullets(['Gateway and OpenAI adapters support bounded text/image work with HTTPS, safe errors, timeouts and disabled redirects. No automatic provider retries occur.','Durable run/event ledgers track daily and lifetime reservations, execution claims, cancellation, unknown cost and recovery. Concurrent requests cannot duplicate an authorized execution.','The UI requires explicit context-sharing consent and keeps retry keys across reload. Images become unreviewed Academy candidates only through a human attachment action.'])
add('<b>Validation:</b> 633 tests passed, plus a local HTTPS gateway fixture with TLS verification preserved. Four tables hold worker records and usage ledgers. External model requests and spend remained zero.')
screenshot('ai-workspace','The actual local page shows AI disabled/configuration incomplete. Fixture validation is not a live OpenAI result.')
add('Future OpenAI preference: $200 total reservation cap, saved as an inactive configuration draft. Credentials, model choices, per-call/daily limits, provider billing controls and a live check are still needed. Cursor IDE access is not a callable runtime provider.','small')
page()
heading('06','Approved creative handoff','E06  |  Phase 5 — explicit campaign planning')
bullets(['Candidate-specific advertiser approval, separate Alpha QA and slot-specific inventory-fit decisions must reference the latest eligible assessment and Brand DNA.','The handoff requires an approved Bliss match, compatible available overlay inventory, a draft campaign and a current accepted Economics quote for the exact placement.','Placement, run, creative lineage and audit commit atomically. Exact retries replay, quote lines cannot be reused, and revocations block new planning while preserving history.'])
add('<b>Validation:</b> 654 tests passed. Real PostgreSQL races produced one complete handoff without orphan placements. The browser checked all three approvals, accepted quote amounts, reload and revocation. Two new tables preserve approvals and handoffs.')
screenshot('campaign-handoff','Local upper-page capture. Successful planning is evidenced by E06’s fictional browser/API fixture; it does not reserve inventory, schedule dates or send media.')
page()
heading('07','Measurement and reviewed learning','E07  |  Phase 6 — evidence stays attached to its creative')
bullets(['Observations retain the frozen handoff snapshot and SHA-256, including asset, Brand DNA, approvals, matching and accepted quote lineage. Later brand edits preserve that historical attribution.','Local validation and advertiser feedback accept no traffic counts. External reports require a source, UTC reporting window, valid counts and acknowledgment, and remain UNVERIFIED_REPORT.','Staff propose research; independent authorized reviewers accept it for research, request revision or reject it. Reviews never apply matching, pricing or creative-reference changes.'])
add('<b>Validation:</b> 673 tests passed. The UI, role boundaries, replay/conflicts, immutable histories and PostgreSQL concurrency were checked. Three new tables store observations, proposals and research reviews.')
screenshot('campaign-measurement','Local development page. Staff readiness distinguishes actual database/schema checks from configuration declarations and pending hosted acceptance.')
page()
heading('08','Automated validation summary','E08  |  Latest retained complete regression: 673 passed / 0 failed / 0 skipped')
add('The suite grew from 592 baseline cases to 673, an increase of 81 cases (13.7%). These are cumulative suite totals at each milestone; they must not be added together as distinct tests. PostgreSQL connection settings were present, so concurrency cases executed their real database paths.')
chart=Drawing(W,190)
labels=['Baseline','Phase 1','Phase 2','Phase 3','Phase 4','Phase 5','Phase 6']; values=[592,596,602,612,633,654,673]
for i,(label,value) in enumerate(zip(labels,values)):
 y=168-i*25;chart.add(String(0,y+3,label,fontName='Body',fontSize=9,fillColor=INK));chart.add(Rect(78,y,370*value/700,13,fillColor=VIOLET if i==6 else colors.HexColor('#B5A3CE'),strokeColor=None));chart.add(String(455,y+3,str(value),fontName='Bold',fontSize=9,fillColor=PLUM))
chart.add(String(78,0,'Bar scale: 0–700 passing cases',fontName='Body',fontSize=7.5,fillColor=MUTED));story.append(chart)
table([['Milestone','Added cases','Full suite','Failures / skipped'],['Phase 1','4','596','0 / 0'],['Phase 2','6','602','0 / 0'],['Phase 3','10','612','0 / 0'],['Phase 4','21','633','0 / 0'],['Phase 5','21','654','0 / 0'],['Phase 6','19','673','0 / 0']],[160,100,110,141])
box('Final retained build and test output','Release build: 0 Warning(s), 0 Error(s).<br/>Passed! Failed: 0, Passed: 673, Skipped: 0, Total: 673.<br/>Reported final suite duration: 45 seconds. Sources: phase6-build.log and phase6-regression.log.')
add('Browser proofs used real Chromium and local PostgreSQL with fictional data. Hosted role boundaries used authenticated test identities; this is not proof of real Entra sign-in. Final successful results are shown; earlier diagnosed failures and corrections are retained in the phase narratives.','small')
page()
heading('09','Schema, integrity and corrections','E09  |  Five additive migrations / 13 new tables')
rows=[['Migration','Tables','Purpose'],['20261012032330_WeddingPlannerBrandDna','1','Versioned advertiser direction'],['20261012034137_AcademyProductionWorkspace','3','Briefs, candidates and reviews'],['20261012040219_ControlledAiWorkers','4','Worker runs, events and usage'],['20261012071804_CreativeCampaignHandoff','2','Candidate approvals and handoffs'],['20261012075444_CampaignMeasurementLineage','3','Observations and research history']]
table(rows,[301,45,165],small=True)
add('Database verification','h2')
add('Migrations were applied to the existing local development database and fresh fictional databases. Reapplication reported an up-to-date database. EF reported no pending model changes. Restricted foreign keys and unique request/version indexes preserve lineage and arbitrate concurrent writes. No production migration is claimed.')
add('Defects found and corrected during validation','h2')
bullets(['Message retries no longer silently discard changed text or actor intent.','An identical Brand DNA writer committing between retry lookup and version lookup now reconciles as replay. Related Academy and approval paths use the same reconciliation pattern.','AI reservation/claim races reconcile wrapped serialization failures and avoid duplicate execution/event insertion.','PostgreSQL fixtures create their known-empty schemas explicitly, avoiding cross-schema EnsureCreated shortcuts. Test identities/data were isolated where shared quotas or fixtures interfered.'])
add('Authority and integrity boundaries','h2')
add('Matching and pricing remain under their existing services. Human review does not launch a campaign. Application writes cannot edit/delete the protected revision, handoff and research histories; this is not a restriction on privileged database administrators. Incomplete or unknown costs/metrics remain unknown.')
page()
heading('10','Backup and recovery evidence','E10  |  Real local PostgreSQL dump, restore and resumed application')
add('A fresh fictional application database completed the handoff and measurement flow. Writes were stopped, a private custom-format dump was taken, and the data was restored to a separately created database. The restored API and browser resumed the same observation, frozen lineage, proposal and ACCEPT_FOR_RESEARCH decision.')
source=(ROOT/'evidence/source-manifest.txt').read_text(); restored=(ROOT/'evidence/restored-manifest.txt').read_text();assert source==restored
rows=[['Restored table','Rows','Digest comparison']]
for line in source.splitlines():
 name,count,digest=line.split('|');rows.append([name,count,'MATCH'])
table(rows,[326,50,135])
box('Observed result','15 table manifests matched. Local fixture recovery: <b>10 seconds</b>, measured from restore-target creation through the restored browser check. Temporary databases and dump were removed; safe logs/manifests were retained.')
add('This small local drill had no writes after the source snapshot. It does not establish production-volume recovery, managed point-in-time recovery, session-key recovery, hosted backup retention, or the proposed hosted RPO/RTO. The script also refused a remote host before database operations.','small')
page()
heading('11','Hosted readiness and next steps','E11  |  Recommended deployment target selected by the user')
table([['Area','Recommendation / state'],['Application','Azure App Service for Linux containers; deployment remains pending.'],['Identity','Microsoft Entra External ID; real tenant/app/claim bindings and sign-in test pending.'],['Database','Azure Database for PostgreSQL Flexible Server; VerifyFull TLS required.'],['Operations','Key Vault for secrets; Azure Monitor/Log Analytics for durable logs and alert tests.'],['Recovery targets','14-day retention; RPO ≤ 1 hour; RTO ≤ 4 hours. Targets await a hosted drill.']],[124,387])
add('Required to complete hosted acceptance','h2')
bullets(['Bind the actual Azure subscription/application, OIDC authority and trusted advertiser/role claims, hosted PostgreSQL credentials, key certificate, persistent storage and trusted proxy topology.','Inspect real backup retention, verify TLS/certificate rejection, test session persistence, and confirm durable logs and alert delivery.','Restore an isolated hosted backup and measure lost committed data and time to functional readiness against the recovery targets.','For live AI, choose compatible OpenAI models, securely bind credentials and billing limits, then perform the deferred live validation.'])
box('Current completion boundary','Development and local validation are complete across all six increments. Production deployment, actual client/QA acceptance of real creative, real overlay pricing anchors, live AI and hosted acceptance remain separate outstanding work. No media was sent and no Azure resources were provisioned by these phases.')
add('The future $200 OpenAI limit is an AI reservation setting, not an Azure infrastructure budget or a statement of actual invoiced charges. The full hosted runbook is retained with this report.','small')
page()
heading('12','Evidence register','Master evidence ID: '+ID+'  |  E01–E13 are references within this report')
register=[
('E01','Environment and baseline','report-metadata.json; evidence/tests.log; environment-summary.json'),
('E02','Phase 1 integrity','evidence/INCREMENT-1-EVIDENCE.md; phase1-final.log; phase1-live.json'),
('E03','Phase 2 Brand DNA','evidence/PHASE-2-EVIDENCE.md; phase2-regression.log'),
('E04','Phase 3 production','evidence/PHASE-3-EVIDENCE.md; phase3-regression.log'),
('E05','Phase 4 controlled AI','evidence/PHASE-4-EVIDENCE.md; PHASE-4-PROVIDER-SETUP.md; phase4-regression.log'),
('E06','Phase 5 handoff','evidence/PHASE-5-EVIDENCE.md; phase5-regression-final.log; phase5-browser-final.log'),
('E07','Phase 6 measurement','evidence/PHASE-6-EVIDENCE.md; phase6-focused.log; phase6-regression.log'),
('E08','Final build and regression','evidence/phase6-build.log; phase6-regression.log'),
('E09','Migration consistency','evidence/phase6-model-check.log; phase6-migrate-repeat.log; migration source in repository'),
('E10','Database recovery','evidence/phase6-recovery.log; source-manifest.txt; restored-manifest.txt'),
('E11','Hosted deployment guide','evidence/PHASE-6-HOSTED-RUNBOOK.md; PHASE-6-CONTRACT.md'),
('E12','Actual local UI captures','assets/: brand-intake, production, ai-workspace, campaign-handoff, campaign-measurement PNGs'),
('E13','Complete change inventory','change-inventory.csv; report-metadata.json; SHA256SUMS.txt')]
table([['Ref.','Evidence','Retained artifacts']]+register,[36,148,327])
add('Files listed after the first evidence/ prefix share that directory unless otherwise stated. Phase contracts and the roadmap are also retained. SHA256SUMS.txt records artifact digests for integrity checking; it is not an external attestation. Raw credentials, private keys, database dumps and broad runtime logs are excluded from the report package.','small')
add('Source provenance','h2')
add('Repository: Coxskull/Bliss_Bot_Chapel.<br/>Base commit: '+META['base_commit']+'.<br/>The inventory describes the 100 development files changed from that base, before the report/package commit. Use the Git commit containing this report as the publication reference; the report does not embed its own commit hash.','small')

inventory=list(csv.DictReader((ROOT/'change-inventory.csv').open()))
for chunk in range(math.ceil(len(inventory)/25)):
 page();heading('APPENDIX',f'Complete change inventory · {chunk+1}/4','E13  |  84 created files / 16 updated files; report-package files excluded')
 if chunk==0:add('Every development file captured in the working-tree snapshot is listed below. Full SHA-256 values and byte sizes are available in change-inventory.csv. “Created” means new relative to the source baseline; “Updated” means a tracked baseline file changed.','small')
 rows=[['Change','Repository-relative path','SHA-256 prefix']]
 for row in inventory[chunk*25:(chunk+1)*25]:rows.append([row['change'],row['path'],row['sha256'][:12]])
 table(rows,[59,361,91],small=True)

pdf=SimpleDocTemplate(str(PDF),pagesize=(595.28,841.89),rightMargin=42,leftMargin=42,topMargin=55,bottomMargin=50,title='Bliss Chapel — Six-phase development and evidence report',author='ALPHA · Bliss Chapel',subject=ID,creator='Client evidence report generated from retained project artifacts')
pdf.build(story,onFirstPage=footer,onLaterPages=footer)
print(PDF)
