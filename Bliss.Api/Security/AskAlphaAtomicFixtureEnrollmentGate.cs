using System.Security.Claims;
using Bliss.Infrastructure.Persistence;

namespace Bliss.Api.Security;

// Stage E is an isolated synthetic test orchestrator, NOT a hosted service.
// The ClaimsPrincipal is a fixture input, not cryptographically verified OIDC.
// No API route/DI binding. Production use is prohibited.
public sealed class AskAlphaAtomicFixtureEnrollmentGate(
    AskAlphaAtomicFixtureEnrollment fixture, BlissAuthenticationOptions config)
{
    public async Task<AskAlphaAtomicFixtureResult> EnrollAsync(
        ClaimsPrincipal? principal, Guid workspaceId, string sourceSlug,
        string retryKey, DateTimeOffset now, CancellationToken ct = default)
    {
        var preflight = AskAlphaVisitorEnrollmentGate.Inspect(principal,config);
        if (preflight.Status != "NEW_VISITOR_OIDC_CLAIMS_DECLARED_SERVER_CREATION_PENDING" ||
            !preflight.IssuerSubjectClaimsConsistent)
            throw new UnauthorizedAccessException("Synthetic visitor enrollment prerequisites missing.");

        // The issuer-controlled subject is read from the signed-in test
        // principal rather than caller-supplied JSON.
        var subject = principal!.FindAll("sub").Single().Value;
        return await fixture.EnrollAsync(new AskAlphaAtomicFixtureRequest(
            workspaceId,sourceSlug,config.AskAlphaVisitorIssuer,subject,retryKey),now,ct);
    }
}
