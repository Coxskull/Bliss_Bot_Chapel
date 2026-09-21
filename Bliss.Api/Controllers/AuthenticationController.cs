using System.Security.Claims;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(
    BlissAuthenticationOptions options,
    IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("session")]
    public ActionResult<SessionDto> GetSession()
    {
        var authenticated = options.Enabled && User.Identity?.IsAuthenticated == true;
        var csrfToken = authenticated
            ? antiforgery.GetAndStoreTokens(HttpContext).RequestToken
            : null;

        var roles = authenticated
            ? User.Claims
                .Where(claim => claim.Type == options.RoleClaimType || claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(role => role)
                .ToArray()
            : [];

        return Ok(new SessionDto(
            options.Enabled,
            !options.Enabled || authenticated,
            authenticated,
            authenticated ? ResolveDisplayName(User) : null,
            authenticated ? ResolveEmail(User) : null,
            roles,
            BlissAuthorization.CanWrite(User, options),
            BlissAuthorization.CanReview(User, options),
            csrfToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(BlissRateLimitPolicies.Authentication)]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        if (!options.Enabled)
        {
            return BadRequest(new { error = "OIDC authentication is not enabled." });
        }

        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        return Challenge(
            new AuthenticationProperties { RedirectUri = safeReturnUrl },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    private string? ResolveDisplayName(ClaimsPrincipal user) =>
        user.FindFirst(options.NameClaimType)?.Value
        ?? user.FindFirst("preferred_username")?.Value
        ?? ResolveEmail(user)
        ?? user.FindFirst("sub")?.Value;

    private static string? ResolveEmail(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value
        ?? user.FindFirst("email")?.Value;
}

public sealed record SessionDto(
    bool AuthenticationEnabled,
    bool AccessAllowed,
    bool IsAuthenticated,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles,
    bool CanWrite,
    bool CanReview,
    string? CsrfToken);
