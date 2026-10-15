using Bliss.Domain.CommandGateway;
using Bliss.Domain.Demonstrations;
namespace Bliss.Tests.CommandGateway;
public sealed class GatewayAskAlphaSimulationTests
{
 private static readonly Guid Tenant=Guid.NewGuid(),Agent=Guid.NewGuid();
 private static GatewayPrincipal Principal()=>new(true,Tenant,Agent);
 private static GatewaySimulationPolicy Policy()=>new(true,Tenant,Agent,ConversationLaboratory.Scenarios.Select(x=>x.Id).ToHashSet(),16);
 [Fact]public void Fixed_laboratory_runs_as_local_not_sent_evidence_only()
 {var report=GatewayAskAlphaSimulation.Run(Principal(),Policy());Assert.Equal("AGM-ASK-ALPHA-SIMULATION/1",report.Schema);Assert.Equal("LOCAL_SYNTHETIC",report.Mode);Assert.Equal("Ask Alpha",report.Voice);Assert.Equal(10,report.ScenarioCount);Assert.Equal(10,report.PassedCount);Assert.True(report.Passed);Assert.Equal("NOT_SENT",report.DeliveryState);Assert.False(report.CustomerContacted);Assert.False(report.OutreachAuthorized);Assert.False(report.CommitmentAuthorized);Assert.True(report.HumanReviewRequired);Assert.Equal(64,report.EvidenceSha256.Length);Assert.All(report.Cases,x=>Assert.True(x.Passed));}
 [Fact]public void Evidence_is_deterministic_for_the_fixed_current_laboratory()
 {var first=GatewayAskAlphaSimulation.Run(Principal(),Policy());var second=GatewayAskAlphaSimulation.Run(Principal(),Policy());Assert.Equal(first.EvidenceSha256,second.EvidenceSha256);Assert.Equal(first.Cases,second.Cases);}
 [Fact]public void Authentication_tenant_and_agent_must_match_policy()
 {Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal() with{Authenticated=false},Policy()));Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal() with{TenantId=Guid.NewGuid()},Policy()));Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal(),Policy() with{AgentId=Guid.NewGuid()}));Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal(),Policy() with{Enabled=false}));}
 [Fact]public void Missing_extra_duplicate_or_unbounded_scenario_grants_are_refused()
 {var ids=ConversationLaboratory.Scenarios.Select(x=>x.Id).ToArray();Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal(),Policy() with{AllowedScenarioIds=ids.Skip(1).ToHashSet()}));Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal(),Policy() with{AllowedScenarioIds=ids.Append("live-customer").ToHashSet()}));Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal(),Policy() with{MaxScenarios=9}));Assert.Throws<UnauthorizedAccessException>(()=>GatewayAskAlphaSimulation.Run(Principal(),Policy() with{MaxScenarios=33}));}
 [Fact]public void Report_contains_only_fixed_scenario_metadata_and_no_contact_route()
 {var report=GatewayAskAlphaSimulation.Run(Principal(),Policy());Assert.All(report.Cases,x=>{Assert.DoesNotContain("@",x.Persona);Assert.DoesNotContain("http",x.Persona,StringComparison.OrdinalIgnoreCase);Assert.All(x.Findings,f=>Assert.DoesNotContain("@",f));});Assert.DoesNotContain(report.Cases,x=>x.Id.Contains("customer",StringComparison.OrdinalIgnoreCase));}
}
