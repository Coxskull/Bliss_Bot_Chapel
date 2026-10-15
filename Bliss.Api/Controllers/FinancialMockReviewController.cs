using System.Text.Json.Serialization;
using Bliss.Api.Financials;
using Bliss.Api.Runtime;
using Bliss.Domain.Financials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
namespace Bliss.Api.Controllers;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MockReviewResponse([property:JsonIgnore(Condition=JsonIgnoreCondition.Never)]object? Result,string Mode,string StripeAccountStatus,bool PaymentCompleted,bool ApprovalGranted,bool HoldReleased);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddMockReviewNote(Guid OperationId,string EvidenceFingerprint,string Note,string IdempotencyKey);
[ApiController][Authorize][Route("api/financials/mock-review")]
public sealed class FinancialMockReviewController(FinancialMockReviewWorkflow workflow):ControllerBase
{
 [HttpGet("recovery-context")]public Task<ActionResult> RecoveryContext()=>Execute(async()=>await workflow.RecoveryContext(User));
 [HttpGet("queue")]public Task<ActionResult> Queue(string? module=null,int limit=25,Guid? after=null,CancellationToken ct=default)=>Execute(async()=>await workflow.Queue(User,module,limit,after,ct));
 [HttpGet("cases/{operationId:guid}")]public Task<ActionResult> Case(Guid operationId,CancellationToken ct)=>Execute(async()=>await workflow.Read(User,operationId,ct));
 [HttpGet("cases/{operationId:guid}/campaign-evidence")]
 public Task<ActionResult> CampaignEvidence(Guid operationId,CancellationToken ct)=>
     Execute(async()=>await workflow.CampaignEvidence(User,operationId,ct));
 [HttpGet("cases/{operationId:guid}/notes")]public Task<ActionResult> Notes(Guid operationId,int limit=25,Guid? after=null,CancellationToken ct=default)=>Execute(async()=>await workflow.Notes(User,operationId,limit,after,ct));
 [HttpGet("cases/{operationId:guid}/evidence-export")]public Task<ActionResult> ExportEvidence(Guid operationId,string evidenceFingerprint,CancellationToken ct)=>Execute(async()=>await workflow.ExportEvidence(User,operationId,evidenceFingerprint,ct));
 [HttpGet("notes/{noteId:guid}")]public Task<ActionResult> Note(Guid noteId,CancellationToken ct)=>Execute(async()=>await workflow.NoteEvidence(User,noteId,ct));
 [HttpGet("cases/{operationId:guid}/note-retries/{retryKey:guid}")]public Task<ActionResult> Retry(Guid operationId,Guid retryKey,CancellationToken ct)=>Execute(async()=>await workflow.FindRetry(User,operationId,retryKey,ct));
 [HttpPost("notes")][EnableRateLimiting(BlissRateLimitPolicies.Writes)]public Task<ActionResult> AddNote(AddMockReviewNote request,CancellationToken ct)=>Execute(async()=>await workflow.AddNote(User,request.OperationId,request.EvidenceFingerprint,request.Note,request.IdempotencyKey,ct));
 private async Task<ActionResult> Execute(Func<Task<object?>> run)
 {try{return Ok(new MockReviewResponse(await run(),"MOCK",FinancialAccountPolicy.StripeAccountStatus,false,false,false));}catch(UnauthorizedAccessException){return Forbid();}catch(ArgumentException){return BadRequest(new{error="Invalid mock review request."});}catch(Exception e)when(e is InvalidOperationException or PostgresException){return Conflict(new{error="Review evidence changed, is busy or unavailable; refresh the case."});}}
}
