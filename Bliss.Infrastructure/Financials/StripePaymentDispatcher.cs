using System.Security.Claims;

namespace Bliss.Infrastructure.Financials;

// Not wired into application runtime. Tests supply a synthetic-secret HTTP handler.
// The trusted adapter must revalidate current quote approval, roles and legal bindings before dispatch.
public sealed class StripePaymentDispatcher(PaymentDispatchStore store, StripeSandboxClient provider,
    Func<ClaimsPrincipal, PreparedPaymentOperation, CancellationToken, Task> authorizeCurrent)
{
    public async Task<string> Dispatch(ClaimsPrincipal user, Guid operationId, CancellationToken ct = default)
    {
        var claim = await store.Claim(user, operationId, ct);
        if (claim.State != "CLAIMED") return claim.State;
        try
        {
            await authorizeCurrent(user, claim.Operation, ct);
        }
        catch
        {
            // Authorization failure makes no provider call. Release the reservation with conservative UNKNOWN evidence.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await store.Complete(claim, null, cleanup.Token);
            throw;
        }
        SandboxPaymentIntent? intent;
        try
        {
            intent = await provider.CreatePaymentIntent(new(claim.Operation.Scope, claim.Operation.QuoteId.ToString("N"),
                claim.Operation.Currency, claim.Operation.AmountMinor), claim.Operation.ProviderKey, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException or System.Text.Json.JsonException or KeyNotFoundException or FormatException)
        {
            // A response error or timeout does not prove the provider did nothing. Same key on retry, never a new operation.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await store.Complete(claim, null, cleanup.Token) ? "UNKNOWN" : "STALE";
        }
        using var completion = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await store.Complete(claim, intent, completion.Token) ? "BOUND" : "STALE";
        // BOUND is provider identity only. No cash, payout, refund or payment-completed journal is written here.
    }
}
