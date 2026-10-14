# AGM-LAUNCH-010 R14 — mock recovery failure acceptance

Extends merged PR182. Local mock-only checks, no payment/approval/release activation.

Corrects two frontend guards:401/403 during uncertain lookup clears visible evidence and allows explicit session refresh while retaining the tab locator; restored same-actor context reloads the unresolved locator before controls resume. Successful note response must match actor/operation/version/digest, and refreshed note page must belong to the operation before recovery is cleared. Historical lookup remains read-only; missing result is not proof of failure. No automatic POST retry or new business authority.

Additional local real-API/browser scenarios: absent committed lookup after abort-before-send remains unknown across reload; malformed stored metadata stops startup; changed note digest blocks otherwise valid retry; changed stored historical note blocks clearing;401 during lookup preserves locator and recovers after explicit same-actor refresh; metadata deletion failure blocks continued writes; wrong refreshed note scope holds pending; wrong response actor holds pending. Prior11 browser scenarios retained.

Interceptor/fault cases are synthetic browser transport/storage/response faults atop a loopback .NET TestServer bridge, real antiforgery and disposable financial PostgreSQL. They do not prove real OIDC/provider/hosted identity, provider status retrieval, tab-closure persistence, OS image audit or Stripe account acceptance. sessionStorage is untrusted and digest is not encryption/permission. Source tests and exact-head reports remain evidence for independent review, not self-acceptance.

Stripe UNVERIFIED / OWNER DECISION REQUIRED. USD confirmed; legal collecting merchant/payees/fees, exposed-secret replacement, verified account and approved isolated sandbox target/identity remain pending. PR175 full-container acceptance still separate/pending registry policy. No new subscriptions, actual provider calls, production access, credential changes, live transfers, account creation, paid activation, merge or deployment. Cancellation decision/quorum/hold release policy unchanged.
