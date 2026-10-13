using System.Text.Json.Serialization;
using Bliss.Api.Financials;
using Bliss.Api.Security;
using Bliss.Api.Runtime;
using Bliss.Domain.Financials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Bliss.Api.Controllers;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrepareFinancialPayment(Guid QuoteId,string IdempotencyKey);
[ApiController][Authorize][Route("api/financials/payment-operations")]
public sealed class FinancialPreparationController(IFinancialPreparation financials,BlissAuthenticationOptions auth):ControllerBase
{
 [HttpPost][EnableRateLimiting(BlissRateLimitPolicies.Writes)]
 public async Task<ActionResult> Prepare(PrepareFinancialPayment request,CancellationToken ct)
 {
  if(!auth.Enabled||User.Identity?.IsAuthenticated!=true||!User.IsInRole(auth.AdvertiserRole))return Forbid();
  try{return Ok(new{operation=await financials.Prepare(User,request.QuoteId,request.IdempotencyKey,ct),status="PREPARED_TEST_ONLY",stripeAccountStatus=FinancialAccountPolicy.StripeAccountStatus,paymentCompleted=false,providerCalled=false});}
  catch(UnauthorizedAccessException){return Forbid();}
  catch(ArgumentException){return BadRequest(new{error="Invalid payment preparation request."});}
  catch(InvalidOperationException){return Conflict(new{error="Payment preparation unavailable or conflicts with existing evidence."});}
 }
}
