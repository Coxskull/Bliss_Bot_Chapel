using System.Text.Json.Serialization;
using Bliss.Api.Financials;
using Bliss.Api.Runtime;
using Bliss.Domain.Financials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
namespace Bliss.Api.Controllers;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]public sealed record MockFinancialWebhook(Guid OperationId,string RawJson,string Signature);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]public sealed record MockFinancialRefund(Guid OperationId,long AmountMinor,string IdempotencyKey);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]public sealed record MockFinancialRefundResult(Guid RefundId,string Status);
[ApiController][Authorize][Route("api/financials")]
public sealed class FinancialMockController(FinancialMockWorkflow workflow):ControllerBase
{
 [HttpGet("modules/{module}/operations")]public Task<ActionResult> Read(string module,CancellationToken ct)=>Execute(async()=>{if(!Enum.TryParse<FinancialModule>(module,false,out var parsed)||!Enum.IsDefined(parsed))throw new ArgumentException();return new{items=await workflow.Read(User,parsed,ct),mode="MOCK",stripeAccountStatus=FinancialAccountPolicy.StripeAccountStatus,paymentCompleted=false};});
 [HttpPost("mock-bound-webhooks")][EnableRateLimiting(BlissRateLimitPolicies.Writes)]public Task<ActionResult> BoundWebhook(MockFinancialWebhook request,CancellationToken ct)=>Execute(async()=>new{journalId=await workflow.BoundWebhook(User,request.OperationId,request.RawJson,request.Signature,ct),mode="MOCK",stripeAccountStatus=FinancialAccountPolicy.StripeAccountStatus,paymentCompleted=false,providerCalled=false});
 [HttpPost("mock-webhooks")][EnableRateLimiting(BlissRateLimitPolicies.Writes)]public Task<ActionResult> Webhook(MockFinancialWebhook request,CancellationToken ct)=>Execute(async()=>new{journalId=await workflow.Webhook(User,request.OperationId,request.RawJson,request.Signature,ct),mode="MOCK",paymentCompleted=false,providerCalled=false});
 [HttpPost("mock-refunds")][EnableRateLimiting(BlissRateLimitPolicies.Writes)]public Task<ActionResult> Refund(MockFinancialRefund request,CancellationToken ct)=>Execute(async()=>new{refund=await workflow.Refund(User,request.OperationId,request.AmountMinor,request.IdempotencyKey,ct),mode="MOCK",providerCalled=false});
 [HttpPost("mock-refunds/reconcile")][EnableRateLimiting(BlissRateLimitPolicies.Writes)]public Task<ActionResult> Reconcile(MockFinancialRefundResult request,CancellationToken ct)=>Execute(async()=>new{journalId=await workflow.Reconcile(User,request.RefundId,request.Status,ct),mode="MOCK",providerCalled=false});
 private async Task<ActionResult> Execute(Func<Task<object>> run)
 {try{return Ok(await run());}catch(UnauthorizedAccessException){return Forbid();}catch(Exception e)when(e is ArgumentException or System.Text.Json.JsonException or KeyNotFoundException or FormatException){return BadRequest(new{error="Invalid mock financial request."});}catch(Exception e)when(e is InvalidOperationException or PostgresException){return Conflict(new{error="Mock financial operation conflicts or is unavailable."});}}
}
