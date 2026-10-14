using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.CommandGateway;
namespace Bliss.Infrastructure.CommandGateway;

// Local presentation only. Trusted scope is supplied by the fixture adapter, not imported JSON.
public static class GatewayMissionPresentation
{
 private static readonly string[] Stages = ["DETECTED", "ASSIGNED", "CORRECTION_PROPOSED", "TEST_REQUESTED",
  "TEST_REPORTED", "EVIDENCE_SUBMITTED", "REVIEWED", "APPROVAL_REQUESTED"];
 public static string Render(GatewayMissionPacket packet, GatewayPrincipal principal, GatewayTaskPolicy policy,
  string expectedTask, Guid expectedCorrelation)
 {
  GatewayTaskLoop.Authorize(principal, policy);
  if (string.IsNullOrWhiteSpace(expectedTask) || expectedTask.Length > 80 || expectedTask.Any(char.IsControl) || expectedCorrelation == Guid.Empty)
   throw new ArgumentException("Bounded expected scope required.");
  if (packet.ArtifactJson is null || Encoding.UTF8.GetByteCount(packet.ArtifactJson) > 32768 ||
   packet.Sha256 != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet.ArtifactJson))))
   throw new ArgumentException("Exact bounded artifact checksum required.");
  using var doc = JsonDocument.Parse(packet.ArtifactJson, new JsonDocumentOptions { MaxDepth = 16 });
  var root = doc.RootElement;
  if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 7 ||
   root.EnumerateObject().Select(p => p.Name).Distinct().Count() != 7 ||
   root.GetProperty("schema").GetString() != "AGM-MISSION-STATUS/1" || root.GetProperty("mode").GetString() != "LOCAL_MOCK" ||
   new[] { "independentlyVerified", "approvalGranted", "executionAuthorized", "deliveredToOwner" }
    .Any(name => root.GetProperty(name).ValueKind != JsonValueKind.False))
   throw new ArgumentException("Explicit unverified mock artifact required.");
  var status = root.GetProperty("status").Deserialize<GatewayMissionStatus>() ?? throw new ArgumentException("Status required.");
  if (status.TenantId != principal.TenantId || status.TaskId != expectedTask || status.CorrelationId != expectedCorrelation)
   throw new UnauthorizedAccessException("Artifact must match trusted tenant/task/run scope.");
  var s = status.Snapshot;
  if (s is null || s.Turns < 1 || s.Turns > 8 || status.Events is null || status.Events.Count != s.Turns ||
   status.Events.Where((e, i) => e is null || e.MessageId == Guid.Empty || e.Snapshot is null ||
    e.Snapshot.Turns != i + 1 || e.Snapshot.Stage != Stages[i] || e.Snapshot.LastMessageId != e.MessageId ||
    e.ActorAgentId != (i is 0 or 1 or 6 or 7 ? policy.ExecutiveAgentId : policy.EngineeringAgentId)).Any() ||
   status.Events.Select(e => e.MessageId).Distinct().Count() != s.Turns || status.Events[^1].Snapshot != s)
   throw new ArgumentException("Bounded ordered attributed history required.");
  GatewayOwnerReviewRequest? expectedRequest = null;
  if (!status.TenantStopped && s.Stage == "APPROVAL_REQUESTED" && s.Turns == 8 && s.Verdict == "PASS" &&
   s.Failed == 0 && s.Passed is > 0 and <= 10000 && s.LastMessageId is Guid message &&
   s.CommitHash is string commit && Hash(commit, 40, false) && s.EvidenceHash is string evidence && Hash(evidence, 64, true))
   expectedRequest = new("AGM-OWNER-REVIEW-REQUEST/1", "LOCAL_MOCK", expectedTask, expectedCorrelation, message,
    commit, evidence, s.Passed.Value, s.Failed.Value);
  if (status.OwnerReviewRequest != expectedRequest) throw new ArgumentException("Consistent non-authoritative review request required.");
  var text = new StringBuilder();
  text.AppendLine("LOCAL MOCK — NOT OWNER APPROVAL");
  text.AppendLine("Reported snapshot; refresh from the scoped reader for current state.");
  text.AppendLine("SHA256 correlation is not signature or independent evidence verification.");
  text.AppendLine($"Task: {status.TaskId}\nTenant: {status.TenantId}\nRun: {status.CorrelationId}\nStage: {s.Stage}\nTenant stopped: {status.TenantStopped}");
  text.AppendLine($"Reported commit: {s.CommitHash ?? "not reported"}\nReported evidence: {s.EvidenceHash ?? "not reported"}\nReported tests: {s.Passed?.ToString() ?? "unknown"} passed / {s.Failed?.ToString() ?? "unknown"} failed");
  text.AppendLine(expectedRequest is null ? "No ready owner-review request in this snapshot." : "Owner review requested in ledger; not delivered or accepted.");
  foreach (var e in status.Events) text.AppendLine($"{e.Snapshot.Turns}. {e.Snapshot.Stage} / message {e.MessageId} / actor {e.ActorAgentId}");
  text.AppendLine("Approval granted: false\nExecution authorized: false\nDelivered to owner: false");
  return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; base-uri 'none'; form-action 'none'\"><title>Local Mission Control report</title></head><body><h1>Local Mission Control</h1><pre>" +
   WebUtility.HtmlEncode(text.ToString()) + "</pre></body></html>";
 }
 private static bool Hash(string text, int length, bool upper) => text.Length == length &&
  text.All(c => c is >= '0' and <= '9' || (upper ? c is >= 'A' and <= 'F' : c is >= 'a' and <= 'f'));
}
