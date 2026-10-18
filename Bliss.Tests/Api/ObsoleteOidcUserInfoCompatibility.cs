using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// Security-regression boundary for obsolete, intentionally no-op UserInfo APIs.
// Their public signatures are preserved for compatibility, but must never
// authorize roles or reviewer scopes. Explicitly require the deprecation marker
// instead of hiding it with a broad CS0618 warning suppression.
internal static class ObsoleteOidcUserInfoCompatibility
{
    internal static void Invoke(string methodName, ClaimsIdentity identity,
        JsonElement userInfo, string claimType)
    {
        var method = typeof(OidcRoleClaimMapper).GetMethod(methodName,
            new[] { typeof(ClaimsIdentity), typeof(JsonElement), typeof(string) });
        Assert.NotNull(method);
        Assert.True(method.IsStatic);
        Assert.Equal(typeof(void), method.ReturnType);
        var deprecation = method.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(deprecation);
        Assert.Contains("Untrusted UserInfo", deprecation.Message);
        // This executes the original legacy entry point, not its replacement.
        // Existing callers still assert that it cannot mutate signed claims.
        method.Invoke(null, new object[] { identity, userInfo, claimType });
    }
}
