using System.Security.Claims;
using Bliss.Infrastructure.Persistence;

namespace Bliss.Api.Security;

// Composition only; no HTTP route, OIDC listener or production DI registration.
// Request-supplied historic session identifiers do not establish ownership.
public static class AskAlphaVisitorOwnershipPreflight
{
    public static AskAlphaVisitorIdentityReadiness Inspect(
        ClaimsPrincipal? user, BlissAuthenticationOptions authentication,
        AskAlphaOwnershipFixtureScope expectedStoredScope,
        AskAlphaOwnershipFixtureReceipt? persistedSyntheticReceipt,
        DateTimeOffset now, ReadOnlySpan<byte> fixtureOnlyKey)
    {
        var claims=AskAlphaVisitorClaimGate.Inspect(
            user,authentication,expectedStoredScope.SessionId);
        if (!claims.ClaimsMatchIssuerAndSession)
            return claims;
        if (expectedStoredScope.Issuer != authentication.AskAlphaVisitorIssuer)
            return claims with { Status="STORED_SESSION_ISSUER_MISMATCH" };

        var subject=user!.FindAll("sub").Single().Value;
        if (!AskAlphaOwnershipFixtureSignature.Verify(
            persistedSyntheticReceipt,expectedStoredScope,subject,now,fixtureOnlyKey))
            return claims with { Status="NO_VALID_AT_CREATION_FIXTURE_RECEIPT" };

        // This only correlates cryptographically signed LOCAL TEST records.
        // In particular HistoricalSessionOwnershipVerified NEVER becomes true.
        return claims with { Status="SYNTHETIC_AT_CREATION_RECEIPT_MATCHES_HISTORY_UNVERIFIED" };
    }
}
