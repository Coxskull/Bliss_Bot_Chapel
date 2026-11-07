import assert from "node:assert/strict";
import { createPipeline, makeReceiptRef } from "./alpha322-commercial-pipeline.mjs";

const base = { journey_id: "SYNTHETIC-JOURNEY-22-001" };
const inquiry = { ...base, stage: "inquiry", event_key: "intake", source_receipt_ref: "existing-intake:FICTIONAL-001" };
inquiry.receipt_ref = makeReceiptRef(inquiry);
const qualification = { ...base, stage: "qualification", event_key: "qualify", parent_receipt_ref: inquiry.receipt_ref, qualified: true };
qualification.receipt_ref = makeReceiptRef(qualification);
const booking = { ...base, stage: "booking", event_key: "booking", parent_receipt_ref: qualification.receipt_ref, human_approved: true, approval_receipt_ref: "synthetic-approval:FICTIONAL-001", booking_ref: "fictional-calendar-slot:001" };
booking.receipt_ref = makeReceiptRef(booking);
const payment = { ...base, stage: "payment", event_key: "payment", parent_receipt_ref: booking.receipt_ref, payment_state: "sandbox_succeeded", processor_mode: "sandbox", processor_status: "succeeded", processor_transaction_ref: "sandbox:FICTIONAL-001", customer_accepted: true, human_release_approved: false, actual_collected_minor: 0 };
payment.receipt_ref = makeReceiptRef(payment);

const pipeline = createPipeline();
for (const event of [inquiry, qualification, booking, payment]) assert.equal(pipeline.append(event).duplicate, false);
assert.equal(pipeline.report(base.journey_id).complete, true);
assert.equal(pipeline.report(base.journey_id).payment_classification, "sandbox_succeeded");
assert.equal(pipeline.report(base.journey_id).actual_collected_minor, 0);
assert.equal(pipeline.append(payment).duplicate, true, "replay must not duplicate payment/revenue");

const beforePayment = () => {
  const p = createPipeline();
  for (const event of [inquiry, qualification, booking]) p.append(event);
  return p;
};
const nonLiveAmount = { ...payment, event_key: "bad-amount", receipt_ref: "", actual_collected_minor: 25000 };
nonLiveAmount.receipt_ref = makeReceiptRef(nonLiveAmount);
assert.throws(() => beforePayment().append(nonLiveAmount), /cannot report collected revenue/);
const forgedLive = { ...payment, event_key: "forged-live", receipt_ref: "", payment_state: "live_collected", processor_mode: "sandbox", actual_collected_minor: 25000, currency: "USD", customer_accepted: true, human_release_approved: true };
forgedLive.receipt_ref = makeReceiptRef(forgedLive);
assert.throws(() => beforePayment().append(forgedLive), /verified live collection/);
const unapprovedBooking = { ...booking, event_key: "unapproved", receipt_ref: "", human_approved: false };
unapprovedBooking.receipt_ref = makeReceiptRef(unapprovedBooking);
const beforeBooking = createPipeline();
beforeBooking.append(inquiry); beforeBooking.append(qualification);
assert.throws(() => beforeBooking.append(unapprovedBooking), /human approval/);
assert.throws(() => createPipeline().append({ ...base, stage: "payment", event_key: "out-of-order", receipt_ref: "out-of-order" }), /expected inquiry/);
console.log("Task 22 offline pipeline: 12 assertions passed; linked fictional journey; sandbox revenue = 0.");
console.log(JSON.stringify(pipeline.report(base.journey_id)));