using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.CommandGateway;
using Bliss.Infrastructure.CommandGateway;
using Npgsql;
namespace Bliss.Tests.CommandGateway;
[Collection("Gateway inbox isolation")]
public sealed class GatewayTaskLoopTests
{
 private const string Commit="0123456789abcdef0123456789abcdef01234567";
 private static readonly string Evidence=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("local fixture tests: corrected addition 2+2=4")));
 private sealed class Fixture:IDisposable
 {
  public Guid Tenant=Guid.NewGuid(),Executive=Guid.NewGuid(),Engineering=Guid.NewGuid(),Gateway=Guid.NewGuid(),Correlation=Guid.NewGuid();public RSA ExecutiveKey=RSA.Create(2048),EngineeringKey=RSA.Create(2048),GatewayKey=RSA.Create(2048);public GatewayEnvelope? Last;public Guid Root;
  public GatewayPrincipal Principal(bool executive)=>new(true,Tenant,executive?Executive:Engineering);
  public GatewayTaskPolicy TaskPolicy(bool executive)=>new(true,Tenant,executive?Executive:Engineering,Executive,Engineering);
  public GatewayPolicy MessagePolicy(bool executive)=>new(true,Tenant,executive?Executive:Engineering,"task-fixture",new HashSet<Guid>{executive?Engineering:Executive},Enum.GetValues<GatewayMessageType>().ToHashSet());
  public GatewayEnvelope Message(GatewayMessageType type,bool executive,string payload="{}",Guid? cause=null){var now=DateTimeOffset.UtcNow;return new("AGM-A2A/1",Guid.NewGuid(),Correlation,cause??Last?.MessageId,"AGM-LAUNCH-011",Tenant,executive?Executive:Engineering,executive?Engineering:Executive,type,"LOCAL_MOCK","task-fixture",Guid.NewGuid().ToString(),now,now.AddMinutes(10),payload);}
  public async Task Accept(NpgsqlDataSource data,GatewayEnvelope m,bool executive)
  {
   var key=executive?ExecutiveKey:EngineeringKey;var policy=MessagePolicy(executive);var valid=GatewayEnvelopeValidator.Validate(m,Principal(executive),policy,DateTimeOffset.UtcNow);var now=DateTimeOffset.UtcNow;var request=new GatewaySignedRequest(m,executive?"qa":"engineering",now,now.AddMinutes(1),"");request=request with{Signature=Convert.ToBase64String(key.SignData(GatewayTransportCodec.RequestBytes(request,valid.ContentHash,"fixture-gateway"),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))};var verify=new GatewayRequestVerifier("fixture-gateway",id=>id==request.KeyId?new(id,key.ExportSubjectPublicKeyInfoPem(),policy):null);var receipt=await new LocalGatewayTransport(new(data),verify,GatewayKey,"gateway","fixture-client",Tenant,Gateway).Accept(request);GatewayReceiptVerifier.Verify(receipt,new("gateway",GatewayKey.ExportSubjectPublicKeyInfoPem(),Tenant,Gateway,true),"fixture-client",valid,DateTimeOffset.UtcNow);
  }
  public async Task<GatewayTaskTransition> Step(NpgsqlDataSource data,GatewayMessageType type,bool executive,string payload="{}") {var m=Message(type,executive,payload);await Accept(data,m,executive);var result=await new LocalGatewayTaskLoop(data).Apply(m.MessageId,Principal(executive),TaskPolicy(executive));Last=m;if(type==GatewayMessageType.DefectReport)Root=m.MessageId;return result;}
  public void Dispose(){ExecutiveKey.Dispose();EngineeringKey.Dispose();GatewayKey.Dispose();}
 }
 private static string Identity(string? extra=null)=>"{\"commit\":\""+Commit+"\",\"evidenceHash\":\""+Evidence+"\""+(extra is null?"":","+extra)+"}";
 private static NpgsqlDataSource Data()=>NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_GATEWAY_TEST_POSTGRES")!);
 private static async Task<Fixture> Prepare(NpgsqlDataSource data){var f=new Fixture();await using var seed=data.CreateCommand("INSERT INTO command_gateway.agents VALUES(@tenant,@qa),(@tenant,@engineering);INSERT INTO command_gateway.execution_control(tenant_id,owner_agent_id,capacity) VALUES(@tenant,@qa,100)");seed.Parameters.AddWithValue("tenant",f.Tenant);seed.Parameters.AddWithValue("qa",f.Executive);seed.Parameters.AddWithValue("engineering",f.Engineering);await seed.ExecuteNonQueryAsync();return f;}
 private static async Task<long> Turns(NpgsqlDataSource data,Fixture f){await using var cmd=data.CreateCommand("SELECT COALESCE(max(turns),0)::bigint FROM command_gateway.task_runs WHERE tenant_id=@tenant");cmd.Parameters.AddWithValue("tenant",f.Tenant);return (long)(await cmd.ExecuteScalarAsync())!;}
 private static async Task ToEvidence(NpgsqlDataSource data,Fixture f,int failed=0){await f.Step(data,GatewayMessageType.DefectReport,true);await f.Step(data,GatewayMessageType.TaskAssignment,true);await f.Step(data,GatewayMessageType.FixProposal,false,Identity());await f.Step(data,GatewayMessageType.TestRequest,false,Identity());await f.Step(data,GatewayMessageType.TestResult,false,Identity("\"passed\":1,\"failed\":"+failed));await f.Step(data,GatewayMessageType.EvidenceSubmission,false,Identity());}
 [Fact]public void Domain_rejects_skipped_stage_self_review_and_unbounded_turns()
 {
  using var f=new Fixture();using var p=JsonDocument.Parse(Identity("\"verdict\":\"PASS\""));var state=new GatewayTaskSnapshot("EVIDENCE_SUBMITTED",6,Guid.NewGuid(),Commit,Evidence,1,0,null);Assert.Throws<UnauthorizedAccessException>(()=>GatewayTaskLoop.Advance(state,GatewayMessageType.ReviewVerdict,Guid.NewGuid(),state.LastMessageId,f.Engineering,f.Executive,f.TaskPolicy(false),p.RootElement));Assert.Throws<InvalidOperationException>(()=>GatewayTaskLoop.Advance(state,GatewayMessageType.ApprovalRequest,Guid.NewGuid(),state.LastMessageId,f.Executive,f.Engineering,f.TaskPolicy(true),p.RootElement));Assert.Throws<InvalidOperationException>(()=>GatewayTaskLoop.Advance(state with{Turns=8},GatewayMessageType.ReviewVerdict,Guid.NewGuid(),state.LastMessageId,f.Executive,f.Engineering,f.TaskPolicy(true),p.RootElement));
 }
 [Fact]public void Domain_rejects_missing_hashes_empty_tests_and_failed_pass_verdict()
 {
  using var f=new Fixture();var state=new GatewayTaskSnapshot("TEST_REQUESTED",4,Guid.NewGuid(),Commit,null,null,null,null);
  foreach(var json in new[]{Identity("\"passed\":0,\"failed\":0"),Identity("\"passed\":-1,\"failed\":0"),Identity("\"passed\":10001,\"failed\":0"),"{\"commit\":\"bad\"}",Identity("\"passed\":\"one\",\"failed\":0")}){using var p=JsonDocument.Parse(json);Assert.Throws<ArgumentException>(()=>GatewayTaskLoop.Advance(state,GatewayMessageType.TestResult,Guid.NewGuid(),state.LastMessageId,f.Engineering,f.Executive,f.TaskPolicy(false),p.RootElement));}
  using var verdict=JsonDocument.Parse(Identity("\"verdict\":\"PASS\""));Assert.Throws<InvalidOperationException>(()=>GatewayTaskLoop.Advance(state with{Stage="EVIDENCE_SUBMITTED",Turns=6,EvidenceHash=Evidence,Passed=1,Failed=1},GatewayMessageType.ReviewVerdict,Guid.NewGuid(),state.LastMessageId,f.Executive,f.Engineering,f.TaskPolicy(true),verdict.RootElement));Assert.Throws<UnauthorizedAccessException>(()=>GatewayTaskLoop.Authorize(f.Principal(true),f.TaskPolicy(true) with{EngineeringAgentId=f.Executive}));
 }
 [GatewayPostgresFact]public async Task Signed_mock_loop_records_eight_bounded_turns_and_never_grants_owner_approval()
 {
  await using var data=Data();using var f=await Prepare(data);Assert.NotEqual(4,2+1);Assert.Equal(4,2+2);await ToEvidence(data,f);await f.Step(data,GatewayMessageType.ReviewVerdict,true,Identity("\"verdict\":\"PASS\""));var result=await f.Step(data,GatewayMessageType.ApprovalRequest,true,Identity());Assert.Equal("APPROVAL_REQUESTED",result.Snapshot.Stage);Assert.Equal(8,result.Snapshot.Turns);Assert.Equal(Commit,result.Snapshot.CommitHash);Assert.Equal(Evidence,result.Snapshot.EvidenceHash);
  await using var reopened=Data();var replay=await new LocalGatewayTaskLoop(reopened).Apply(f.Last!.MessageId,f.Principal(true),f.TaskPolicy(true));Assert.True(replay.Replay);Assert.Equal(result.Snapshot,replay.Snapshot);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Accept(data,f.Message(GatewayMessageType.ApprovalDecision,true),true));var ninth=f.Message(GatewayMessageType.ApprovalRequest,true,Identity());await f.Accept(data,ninth,true);await Assert.ThrowsAsync<InvalidOperationException>(()=>new LocalGatewayTaskLoop(data).Apply(ninth.MessageId,f.Principal(true),f.TaskPolicy(true)));Assert.Equal(8,await Turns(data,f));
 }
 [GatewayPostgresFact]public async Task Concurrent_exact_transition_has_one_winner_and_policy_and_tenant_changes_are_denied()
 {
  await using var data=Data();using var f=await Prepare(data);var root=f.Message(GatewayMessageType.DefectReport,true);await f.Accept(data,root,true);var store=new LocalGatewayTaskLoop(data);var all=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>store.Apply(root.MessageId,f.Principal(true),f.TaskPolicy(true))));Assert.Single(all,x=>!x.Replay);Assert.Equal(1,await Turns(data,f));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Apply(root.MessageId,f.Principal(true) with{Authenticated=false},f.TaskPolicy(true)));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Apply(root.MessageId,f.Principal(true),f.TaskPolicy(true) with{EngineeringAgentId=Guid.NewGuid()}));using var foreign=await Prepare(data);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Apply(root.MessageId,foreign.Principal(true),foreign.TaskPolicy(true)));
 }
 [GatewayPostgresFact]public async Task Wrong_cause_commit_evidence_and_self_review_leave_task_unchanged()
 {
  await using var data=Data();using var f=await Prepare(data);await ToEvidence(data,f);var store=new LocalGatewayTaskLoop(data);var wrongActor=f.Message(GatewayMessageType.ReviewVerdict,false,Identity("\"verdict\":\"PASS\""));await f.Accept(data,wrongActor,false);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Apply(wrongActor.MessageId,f.Principal(false),f.TaskPolicy(false)));
  var wrongCause=f.Message(GatewayMessageType.ReviewVerdict,true,Identity("\"verdict\":\"PASS\""),f.Root);await f.Accept(data,wrongCause,true);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Apply(wrongCause.MessageId,f.Principal(true),f.TaskPolicy(true)));
  foreach(var payload in new[]{Identity("\"verdict\":\"PASS\"").Replace(Commit,new string('a',40)),Identity("\"verdict\":\"PASS\"").Replace(Evidence,new string('B',64))}){var m=f.Message(GatewayMessageType.ReviewVerdict,true,payload);await f.Accept(data,m,true);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Apply(m.MessageId,f.Principal(true),f.TaskPolicy(true)));}Assert.Equal(6,await Turns(data,f));
 }
 [GatewayPostgresFact]public async Task Failed_review_cannot_request_acceptance_and_stop_blocks_advancement()
 {
  await using var data=Data();using var f=await Prepare(data);await ToEvidence(data,f,1);await f.Step(data,GatewayMessageType.ReviewVerdict,true,Identity("\"verdict\":\"FAIL\""));var request=f.Message(GatewayMessageType.ApprovalRequest,true,Identity());await f.Accept(data,request,true);await Assert.ThrowsAsync<InvalidOperationException>(()=>new LocalGatewayTaskLoop(data).Apply(request.MessageId,f.Principal(true),f.TaskPolicy(true)));Assert.Equal(7,await Turns(data,f));using var stopped=await Prepare(data);var root=stopped.Message(GatewayMessageType.DefectReport,true);await stopped.Accept(data,root,true);Assert.True(await new LocalGatewayBudget(data).Stop(stopped.Principal(true),new(true,stopped.Tenant,stopped.Executive,true,false)));await Assert.ThrowsAsync<InvalidOperationException>(()=>new LocalGatewayTaskLoop(data).Apply(root.MessageId,stopped.Principal(true),stopped.TaskPolicy(true)));Assert.Equal(0,await Turns(data,stopped));
 }
 [GatewayPostgresFact]public async Task Task_audit_failure_rolls_back_state_and_history_rejects_mutation()
 {
  await using var data=Data();using var f=await Prepare(data);var root=f.Message(GatewayMessageType.DefectReport,true);await f.Accept(data,root,true);var store=new LocalGatewayTaskLoop(data);await using(var cmd=data.CreateCommand("ALTER TABLE command_gateway.task_events ADD CONSTRAINT fixture_task_fault CHECK(message_id<>'"+root.MessageId+"') NOT VALID"))await cmd.ExecuteNonQueryAsync();try{await Assert.ThrowsAsync<PostgresException>(()=>store.Apply(root.MessageId,f.Principal(true),f.TaskPolicy(true)));Assert.Equal(0,await Turns(data,f));}finally{await using var cmd=data.CreateCommand("ALTER TABLE command_gateway.task_events DROP CONSTRAINT fixture_task_fault");await cmd.ExecuteNonQueryAsync();}await store.Apply(root.MessageId,f.Principal(true),f.TaskPolicy(true));foreach(var sql in new[]{"UPDATE command_gateway.task_events SET turn=turn","DELETE FROM command_gateway.task_events","TRUNCATE command_gateway.task_events"}){await using var cmd=data.CreateCommand(sql);await Assert.ThrowsAsync<PostgresException>(async()=>await cmd.ExecuteNonQueryAsync());}
 }
 private sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
 [GatewayPostgresFact]public async Task Expired_or_unaccepted_message_and_engineering_root_do_not_start_task()
 {
  await using var data=Data();using var f=await Prepare(data);var store=new LocalGatewayTaskLoop(data);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Apply(Guid.NewGuid(),f.Principal(true),f.TaskPolicy(true)));var wrong=f.Message(GatewayMessageType.DefectReport,false);await f.Accept(data,wrong,false);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Apply(wrong.MessageId,f.Principal(false),f.TaskPolicy(false)));Assert.Equal(0,await Turns(data,f));
  var prior=DateTimeOffset.UtcNow.AddMinutes(-2);var expired=f.Message(GatewayMessageType.DefectReport,true) with{CreatedAt=prior,ExpiresAt=prior.AddMinutes(1)};await new LocalGatewayInbox(data,new FixedClock(prior)).Accept(expired,f.Principal(true),f.MessagePolicy(true));await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Apply(expired.MessageId,f.Principal(true),f.TaskPolicy(true)));Assert.Equal(0,await Turns(data,f));
 }
 [GatewayPostgresFact]public async Task Mission_status_is_scoped_read_only_and_request_is_not_owner_acceptance()
 {
  await using var data=Data();using var f=await Prepare(data);await ToEvidence(data,f);
  var reader=new LocalGatewayMissionReview(data);var before=await reader.Read("AGM-LAUNCH-011",f.Correlation,f.Principal(true),f.TaskPolicy(true));
  Assert.Equal(6,before.Status.Events.Count);Assert.Null(before.Status.OwnerReviewRequest);
  await f.Step(data,GatewayMessageType.ReviewVerdict,true,Identity("\"verdict\":\"PASS\""));await f.Step(data,GatewayMessageType.ApprovalRequest,true,Identity());
  var packet=await reader.Read("AGM-LAUNCH-011",f.Correlation,f.Principal(false),f.TaskPolicy(false));
  var request=Assert.IsType<GatewayOwnerReviewRequest>(packet.Status.OwnerReviewRequest);
  Assert.Equal(Commit,request.CommitHash);Assert.Equal(Evidence,request.EvidenceHash);Assert.Equal(f.Last!.MessageId,request.RequestMessageId);
  Assert.Equal("LOCAL_MOCK",request.Mode);Assert.False(request.ApprovalGranted);Assert.False(request.ExecutionAuthorized);Assert.False(request.DeliveredToOwner);
  Assert.Equal(8,packet.Status.Events.Count);Assert.Equal(8,await Turns(data,f));
  Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packet.ArtifactJson))),packet.Sha256);
  await using var reopened=Data();var repeat=await new LocalGatewayMissionReview(reopened).Read("AGM-LAUNCH-011",f.Correlation,f.Principal(true),f.TaskPolicy(true));
  Assert.Equal(packet.ArtifactJson,repeat.ArtifactJson);Assert.Equal(packet.Sha256,repeat.Sha256);
  using var json=JsonDocument.Parse(packet.ArtifactJson);Assert.False(json.RootElement.GetProperty("independentlyVerified").GetBoolean());
  Assert.DoesNotContain("payload",packet.ArtifactJson,StringComparison.OrdinalIgnoreCase);
  if(Environment.GetEnvironmentVariable("AGM_MISSION_PACKET_PATH") is string path)await File.WriteAllTextAsync(path,JsonSerializer.Serialize(packet));
 }
 [GatewayPostgresFact]public async Task Mission_status_denies_wrong_tenant_actor_roles_task_correlation_and_invalid_inputs()
 {
  await using var data=Data();using var f=await Prepare(data);await f.Step(data,GatewayMessageType.DefectReport,true);
  var reader=new LocalGatewayMissionReview(data);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read("AGM-LAUNCH-011",f.Correlation,f.Principal(true) with{Authenticated=false},f.TaskPolicy(true)));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read("AGM-LAUNCH-011",f.Correlation,f.Principal(true) with{AgentId=Guid.NewGuid()},f.TaskPolicy(true)));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read("AGM-LAUNCH-011",f.Correlation,f.Principal(true),f.TaskPolicy(true) with{EngineeringAgentId=Guid.NewGuid()}));
  using var other=await Prepare(data);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read("AGM-LAUNCH-011",f.Correlation,other.Principal(true),other.TaskPolicy(true)));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read("OTHER-TASK",f.Correlation,f.Principal(true),f.TaskPolicy(true)));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read("AGM-LAUNCH-011",Guid.NewGuid(),f.Principal(true),f.TaskPolicy(true)));
  foreach(var task in new[]{"",new string('a',81),"bad\ntask"})await Assert.ThrowsAsync<ArgumentException>(()=>reader.Read(task,f.Correlation,f.Principal(true),f.TaskPolicy(true)));
  await Assert.ThrowsAsync<ArgumentException>(()=>reader.Read("AGM-LAUNCH-011",Guid.Empty,f.Principal(true),f.TaskPolicy(true)));
  Assert.Equal(1,await Turns(data,f));
 }
 [GatewayPostgresFact]public async Task Mission_status_preserves_stopped_history_and_rejects_mutable_snapshot_disagreement()
 {
  await using var data=Data();using var f=await Prepare(data);await ToEvidence(data,f);await f.Step(data,GatewayMessageType.ReviewVerdict,true,Identity("\"verdict\":\"PASS\""));await f.Step(data,GatewayMessageType.ApprovalRequest,true,Identity());
  Assert.True(await new LocalGatewayBudget(data).Stop(f.Principal(true),new(true,f.Tenant,f.Executive,true,false)));
  var reader=new LocalGatewayMissionReview(data);var stopped=await reader.Read("AGM-LAUNCH-011",f.Correlation,f.Principal(true),f.TaskPolicy(true));
  Assert.True(stopped.Status.TenantStopped);Assert.Equal(8,stopped.Status.Events.Count);Assert.Null(stopped.Status.OwnerReviewRequest);
  await using(var cmd=data.CreateCommand("UPDATE command_gateway.task_runs SET passed=2 WHERE tenant_id=@tenant")){cmd.Parameters.AddWithValue("tenant",f.Tenant);await cmd.ExecuteNonQueryAsync();}
  await Assert.ThrowsAsync<InvalidOperationException>(()=>reader.Read("AGM-LAUNCH-011",f.Correlation,f.Principal(true),f.TaskPolicy(true)));
 }
 [Fact]public void Task_store_rejects_hosted_connection(){using var data=NpgsqlDataSource.Create("Host=database.example.test;Database=gateway_test");Assert.Throws<InvalidOperationException>(()=>new LocalGatewayTaskLoop(data));Assert.Throws<InvalidOperationException>(()=>new LocalGatewayMissionReview(data));using var wrong=NpgsqlDataSource.Create("Host=127.0.0.1;Database=postgres");Assert.Throws<InvalidOperationException>(()=>new LocalGatewayMissionReview(wrong));}
}
