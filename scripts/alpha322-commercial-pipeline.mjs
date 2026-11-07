/**
 * Task 22's bounded, offline commercial-pipeline evidence model.
 * No network, CRM, calendar, database, Stripe, secrets, or external action.
 * It consumes an existing intake receipt reference; it does not create CRM or
 * payment records. Only a verified live processor success can count as revenue.
 */
import { createHash } from "node:crypto";

const STAGES = ["inquiry", "qualification", "booking", "payment"];
const PAYMENT_STATES = new Set([
  "fictional", "pending", "failed", "sandbox_succeeded", "live_collected"
]);
const text = value => typeof value === "string" && value.trim().length > 0;

export function receiptDigest(value) {
  return createHash("sha256").update(JSON.stringify(value)).digest("hex");
}

export function createPipeline() {
  const events = new Map();
  const idempotency = new Map();

  function append(event) {
    if (!event || !text(event.journey_id) || !text(event.event_key) ||
        !text(event.stage) || !text(event.receipt_ref)) {
      throw new Error("journey, event key, stage, and receipt reference are required");
    }
    if (events.has(event.receipt_ref)) {
      const prior = events.get(event.receipt_ref);
      if (JSON.stringify(prior) === JSON.stringify(event)) return { duplicate: true, event: prior };
      throw new Error("receipt reference conflict");
    }
    const priorRef = idempotency.get(\`\${event.journey_id}:\${event.event_key}\`);
    if (priorRef) {
      const prior = events.get(priorRef);
      if (JSON.stringify(prior) === JSON.stringify(event)) return { duplicate: true, event: prior };
      throw new Error("idempotency key reused with different event");
    }

    const journey = [...events.values()].filter(item => item.journey_id === event.journey_id);
    const expected = STAGES[journey.length];
    if (!expected || event.stage !== expected) throw new Error(\`expected \${expected ?? "complete"} stage\`);
    if (journey.length && event.parent_receipt_ref !== journey.at(-1).receipt_ref)
      throw new Error("receipt chain does not reference the prior stage");
    if (!journey.length && (event.stage !== "inquiry" || !text(event.source_receipt_ref)))
      throw new Error("first event must reference the existing intake receipt");
    if (event.stage === "qualification" && event.qualified !== true)
      throw new Error("qualification requires explicit human review");
    if (event.stage === "booking" && (event.human_approved !== true || !text(event.approval_receipt_ref)))
      throw new Error("booking requires recorded human approval");
    if (event.stage === "payment") {
      if (!PAYMENT_STATES.has(event.payment_state)) throw new Error("unknown payment classification");
      if (event.payment_state === "live_collected" &&
          (event.processor_mode !== "live" || event.processor_status !== "succeeded" ||
           !text(event.processor_transaction_ref) || event.customer_accepted !== true ||
           event.human_release_approved !== true))
        throw new Error("live revenue requires verified live collection and approvals");
      if (event.payment_state !== "live_collected" && Number(event.actual_collected_minor ?? 0) !== 0)
        throw new Error("non-live payment classifications cannot report collected revenue");
    }

    const stored = structuredClone(event);
    events.set(stored.receipt_ref, stored);
    idempotency.set(\`\${stored.journey_id}:\${stored.event_key}\`, stored.receipt_ref);
    return { duplicate: false, event: stored };
  }

  function report(journeyId) {
    const chain = [...events.values()].filter(event => event.journey_id === journeyId);
    const payment = chain.find(event => event.stage === "payment");
    const actualCollectedMinor = payment?.payment_state === "live_collected"
      ? Number(payment.actual_collected_minor) : 0;
    return {
      journey_id: journeyId,
      stages: chain.map(({ stage, receipt_ref, parent_receipt_ref, source_receipt_ref }) =>
        ({ stage, receipt_ref, parent_receipt_ref, source_receipt_ref })),
      payment_classification: payment?.payment_state ?? "not_recorded",
      actual_collected_minor: actualCollectedMinor,
      currency: actualCollectedMinor ? payment.currency : null,
      complete: chain.length === 4
    };
  }

  return Object.freeze({ append, report });
}

export function makeReceiptRef(event) {
  const { receipt_ref: _ignored, ...body } = event;
  return \`sha256:\${receiptDigest(body)}\`;
}