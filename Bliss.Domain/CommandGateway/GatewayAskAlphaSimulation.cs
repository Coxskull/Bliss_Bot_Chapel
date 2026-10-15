using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Demonstrations;
namespace Bliss.Domain.CommandGateway;

public sealed record GatewaySimulationPolicy(bool Enabled,Guid TenantId,Guid AgentId,IReadOnlySet<string> AllowedScenarioIds,int MaxScenarios);
public sealed record GatewaySimulationCase(string Id,string Persona,string Signal,bool Passed,IReadOnlyList<string> Findings);
public sealed record GatewaySimulationReport(string Schema,string Mode,string Voice,int ScenarioCount,int PassedCount,bool Passed,
 string DeliveryState,bool CustomerContacted,bool OutreachAuthorized,bool CommitmentAuthorized,bool HumanReviewRequired,
 IReadOnlyList<GatewaySimulationCase> Cases,string EvidenceSha256);

// Runs the existing fixed Ask Alpha laboratory locally; no customer, network, delivery or mutable state.
public static class GatewayAskAlphaSimulation
{
 public static GatewaySimulationReport Run(GatewayPrincipal principal,GatewaySimulationPolicy policy)
 {
  if(!policy.Enabled||!principal.Authenticated||principal.TenantId==Guid.Empty||principal.AgentId==Guid.Empty||principal.TenantId!=policy.TenantId||principal.AgentId!=policy.AgentId)throw new UnauthorizedAccessException("Trusted local simulation grant required.");
  var defined=ConversationLaboratory.Scenarios.Select(x=>x.Id).Order(StringComparer.Ordinal).ToArray();
  var allowed=policy.AllowedScenarioIds.Order(StringComparer.Ordinal).ToArray();
  if(policy.MaxScenarios is<1 or>32||defined.Length>policy.MaxScenarios||allowed.Length!=defined.Length||allowed.Distinct(StringComparer.Ordinal).Count()!=allowed.Length||!allowed.SequenceEqual(defined))throw new UnauthorizedAccessException("Exact fixed synthetic scenario grant required.");
  var source=ConversationLaboratory.Run();if(source.ScenarioCount!=defined.Length||source.Results.Select(x=>x.Id).Order(StringComparer.Ordinal).SequenceEqual(defined) is false)throw new InvalidOperationException("Exact laboratory scenario set required.");
  var cases=source.Results.Select(x=>new GatewaySimulationCase(x.Id,x.Persona,x.Signal,x.Passed,x.Missing.ToArray())).ToArray();
  var evidence=JsonSerializer.Serialize(new{schema="AGM-ASK-ALPHA-SIMULATION/1",mode="LOCAL_SYNTHETIC",voice=source.Voice,scenarioCount=source.ScenarioCount,passedCount=source.PassedCount,passed=source.Passed,deliveryState="NOT_SENT",customerContacted=false,outreachAuthorized=false,commitmentAuthorized=false,humanReviewRequired=true,cases});
  return new("AGM-ASK-ALPHA-SIMULATION/1","LOCAL_SYNTHETIC",source.Voice,source.ScenarioCount,source.PassedCount,source.Passed,"NOT_SENT",false,false,false,true,cases,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))));
 }
}
