// HTTPS test double only. No external provider/model calls. Never deploy as an AI provider.
const https = require("node:https");
const fs = require("node:fs");
const pixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
const server = https.createServer({ key: fs.readFileSync(process.env.BLISS_FIXTURE_TLS_KEY), cert: fs.readFileSync(process.env.BLISS_FIXTURE_TLS_CERT) }, (req, res) => {
  if (req.method !== "POST" || req.url !== "/worker" || req.headers.authorization !== "Bearer local-fixture-token") { res.writeHead(404); res.end(); return; }
  let body = "";
  req.on("data", chunk => { body += chunk; if (body.length > 100000) req.destroy(); });
  req.on("end", () => {
    let request;
    try { request = JSON.parse(body); } catch { res.writeHead(400); res.end(); return; }
    const delayed = request.input?.prompt === "FIXTURE_WAIT";
    setTimeout(() => {
      if (res.destroyed) return;
      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ text: "LOCAL TEST FIXTURE: draft suggestion, not a model response.", ...(request.kind === "IMAGE" ? { imageBase64: pixel } : {}), providerRequestId: "fixture-" + request.requestId, inputTokens: 0, outputTokens: 0, actualUsd: 0 }));
    }, delayed ? 3000 : 100);
  });
});
server.listen(Number(process.env.BLISS_FIXTURE_PORT || 9444), "127.0.0.1", () => console.log("Local HTTPS fixture listening; no model calls."));
