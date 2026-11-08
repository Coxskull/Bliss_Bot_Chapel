/**
 * ALPHA Task #21 — internal-only incident alert contract (source candidate).
 *
 * PURE: never sends email/webhooks, queries cases, impersonates staff,
 * or marks an alert SENT/DELIVERED/ACKNOWLEDGED. An approved delivery
 * adapter with durable provider receipts is required before launch.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const ROUTE = Object.freeze({
  billing:"BILLING_REVIEW",refund:"BILLING_REVIEW",
  failed_campaign:"BILLING_REVIEW",privacy:"PRIVACY_REVIEW",
  security_incident:"SECURITY_REVIEW"
});
const CRITICAL="security_incident";
function uuid(s){return typeof s==="string"&&UUID.test(s);}
function validTime(s){
  if(typeof s!=="string"||!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/.test(s))return false;
  return Number.isFinite(Date.parse(s))&&/(?:Z|[+-]\d{2}:\d{2})$/.test(s);
}
/** Build only non-customer metadata from an existing HIGH/CRITICAL DB alert. */
export function prepareInternalEscalation(record){
  if(!record||typeof record!=="object"||Array.isArray(record))
    throw Error("TASK21_ALERT_INVALID");
  const {alert_id,case_id,severity,assigned_role,category,delivery_state,created_at}=record;
  if(!uuid(alert_id)||!uuid(case_id)||!(category in ROUTE)||
    assigned_role!==ROUTE[category]||
    severity!==(category===CRITICAL?"CRITICAL":"HIGH")||
    delivery_state!=="UNROUTED"||!validTime(created_at))
    throw Error("TASK21_ALERT_INVALID");
  // Explicit output allowlist. Never forward email, name, customer
  // description, user IDs, Auth tokens or untrusted free text.
  return Object.freeze({
    schema:"agm.task21.internal-alert.v1",environment:"staging",synthetic:true,
    alert_id,case_id,severity,assigned_role,
    created_at:new Date(created_at).toISOString(),
    instruction:"Open the restricted Task 21 operator queue; no customer reply or refund."
  });
}
/**
 * Zero-egress, syntactic launch gate, NOT actual delivery/ACK evidence.
 * Input flags must come from independent audits. Never use as substitute
 * for review of provider receipts, staffing, policy or production approval.
 */
export function assessEscalationAcceptance(evidence){
  if(!evidence||typeof evidence!=="object"||Array.isArray(evidence))
    throw Error("TASK21_EVIDENCE_REQUIRED");
  const required=Object.freeze([
    ["approved_internal_channel","Approved monitored internal contact channel"],
    ["app_generated_delivery_receipt","App-originated delivery receipt for a fictional alert"],
    ["distinct_human_ack_receipt","Independent timestamped operator ACK"],
    ["verified_primary_and_backup","Verified distinct primary and backup staff coverage"],
    ["documented_out_of_office_escalation","Documented offline and escalation route"],
    ["owner_policy_approval","Owner/finance/legal approval of applicable policies"],
    ["highlevel_sandbox_acceptance","HighLevel sandbox consent and opt-out acceptance"],
    ["production_release_review","Separate production deployment and security review"]
  ]);
  const blocked=required.filter(([key])=>evidence[key]!==true)
    .map(([key,description])=>Object.freeze({key,description}));
  return Object.freeze({
    decision:blocked.length===0?"EVIDENCE_READY_FOR_OWNER_REVIEW":"NO_GO",
    blocked:Object.freeze(blocked),
    warning:"Input flags are not evidence by themselves; verify receipts independently."
  });
}
