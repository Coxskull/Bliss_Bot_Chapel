using System.Net;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Microsoft.Extensions.FileProviders;
using Bliss.Infrastructure.DependencyInjection;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
var authentication = builder.Configuration
    .GetSection(BlissAuthenticationOptions.SectionName)
    .Get<BlissAuthenticationOptions>() ?? new BlissAuthenticationOptions();
var runtime = builder.Configuration
    .GetSection(BlissRuntimeOptions.SectionName)
    .Get<BlissRuntimeOptions>() ?? new BlissRuntimeOptions();

if (!authentication.Enabled && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "OIDC authentication must be enabled outside Development. "
        + "Set Authentication__Enabled=true and configure the identity provider.");
}

if (authentication.Enabled
    && (string.IsNullOrWhiteSpace(authentication.Authority)
        || string.IsNullOrWhiteSpace(authentication.ClientId)))
{
    throw new InvalidOperationException(
        "Authentication:Authority and Authentication:ClientId are required when OIDC is enabled.");
}

if (!builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(runtime.DataProtectionKeysPath))
{
    throw new InvalidOperationException(
        "Runtime:DataProtectionKeysPath is required outside Development so OIDC sessions survive restarts and replicas.");
}

if (runtime.WriteRateLimitPermitLimit <= 0
    || runtime.AuthenticationRateLimitPermitLimit <= 0
    || runtime.RateLimitWindowSeconds <= 0)
{
    throw new InvalidOperationException("Runtime rate-limit values must be positive.");
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<BlissDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddBlissInfrastructure(builder.Configuration);
builder.Services.AddSingleton(authentication);
builder.Services.AddSingleton(runtime);
builder.Services.AddSingleton<OperationalEventStore>();
builder.Services.AddScoped<OperatorIdentity>();
builder.Services.AddScoped<WeddingPlannerAccess>();
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var value in runtime.KnownProxies)
    {
        if (!IPAddress.TryParse(value, out var address))
        {
            throw new InvalidOperationException($"Runtime:KnownProxies contains invalid IP address '{value}'.");
        }

        if (!options.KnownProxies.Contains(address))
        {
            options.KnownProxies.Add(address);
        }
    }
});
var dataProtection = builder.Services
    .AddDataProtection()
    .SetApplicationName("BlissBotChapel");
if (!string.IsNullOrWhiteSpace(runtime.DataProtectionKeysPath))
{
    var keyDirectory = Directory.CreateDirectory(runtime.DataProtectionKeysPath);
    dataProtection.PersistKeysToFileSystem(keyDirectory);
}
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>(
        "database",
        tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var http = context.HttpContext;
        http.Response.Headers.RetryAfter = runtime.RateLimitWindowSeconds.ToString();
        http.RequestServices.GetRequiredService<OperationalEventStore>().Record(
            new OperationalEvent(
                DateTime.UtcNow,
                "RateLimited",
                http.Request.Method,
                RequestCorrelation.SafePath(http),
                StatusCodes.Status429TooManyRequests,
                RequestCorrelation.Resolve(http)));
        await http.Response.WriteAsJsonAsync(
            new { error = "Rate limit exceeded. Retry later." },
            cancellationToken);
    };
    options.AddPolicy(
        BlissRateLimitPolicies.Authentication,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = runtime.AuthenticationRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(runtime.RateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy(
        BlissRateLimitPolicies.Writes,
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value
                ?? context.User.Identity?.Name
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = runtime.WriteRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(runtime.RateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-Bliss-Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.HeaderName = "X-CSRF-TOKEN";
});
var authenticationBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-Bliss-Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

if (authentication.Enabled)
{
    authenticationBuilder.AddOpenIdConnect(options =>
    {
        options.Authority = authentication.Authority;
        options.ClientId = authentication.ClientId;
        options.ClientSecret = authentication.ClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = authentication.NameClaimType,
            RoleClaimType = authentication.RoleClaimType
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    var accessPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(context =>
            !authentication.Enabled
            || context.User.Identity?.IsAuthenticated == true)
        .Build();
    options.DefaultPolicy = accessPolicy;
    options.FallbackPolicy = accessPolicy;
    options.AddPolicy(
        BlissAuthorization.WritePolicy,
        policy => policy.RequireAssertion(context =>
            BlissAuthorization.CanWrite(context.User, authentication)));
    options.AddPolicy(
        BlissAuthorization.ReviewPolicy,
        policy => policy.RequireAssertion(context =>
            BlissAuthorization.CanReview(context.User, authentication)));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Bliss Bot Chapel API",
        Version = "v1",
        Description = "Standalone Bliss API: controlled ingestion, matching, human review, campaign placement planning, and Wedding Planner foundation."
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var requestId = RequestCorrelation.Resolve(context);
    context.RequestServices.GetRequiredService<OperationalEventStore>().RememberRequest(requestId);
    context.Response.OnStarting(() =>
    {
        context.Response.Headers[RequestCorrelation.HeaderName] = requestId;
        return Task.CompletedTask;
    });
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; "
            + "img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; "
            + "base-uri 'self'; form-action 'self'";
        return Task.CompletedTask;
    });
    await next();
});

var frontendRoot = FrontendHost.ResolveRoot(app.Environment);
var publicFrontend = Path.Combine(frontendRoot, "public");
var operationsFrontend = Path.Combine(frontendRoot, "operations");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(publicFrontend)
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(operationsFrontend),
    RequestPath = "/operations"
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
    var connectionString = app.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString) && db.Database.IsRelational())
    {
        try
        {
            if (db.Database.CanConnect())
            {
                var phase1 = scope.ServiceProvider.GetRequiredService<Phase1DataSeeder>();
                await phase1.SeedAsync();
                var phase2 = scope.ServiceProvider.GetRequiredService<Phase2DataSeeder>();
                await phase2.SeedAsync();
                var phase3 = scope.ServiceProvider.GetRequiredService<Phase3DataSeeder>();
                await phase3.SeedAsync();
                var weddingPlanner = scope.ServiceProvider.GetRequiredService<WeddingPlannerDataSeeder>();
                await weddingPlanner.SeedAsync();
                var economics = scope.ServiceProvider.GetRequiredService<EconomicsDataSeeder>();
                await economics.SeedAsync();
                var economicsPhase2 = scope.ServiceProvider.GetRequiredService<EconomicsPhase2DataSeeder>();
                await economicsPhase2.SeedAsync();
            }
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Phase 1 seed skipped because the database was not reachable.");
        }
    }
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    await next();
    var status = context.Response.StatusCode;
    if (status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
    {
        context.RequestServices.GetRequiredService<OperationalEventStore>().Record(
            new OperationalEvent(
                DateTime.UtcNow,
                status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Forbidden",
                context.Request.Method,
                RequestCorrelation.SafePath(context),
                status,
                RequestCorrelation.Resolve(context)));
    }
});
app.UseRateLimiter();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var unsafeApiRequest = authentication.Enabled
        && context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method)
        && !HttpMethods.IsTrace(context.Request.Method);

    if (unsafeApiRequest)
    {
        try
        {
            await context.RequestServices
                .GetRequiredService<IAntiforgery>()
                .ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "CSRF validation failed. Refresh the session and retry."
            });
            return;
        }
    }

    await next();
});
app.MapControllers();
app.MapHealthChecks(
        "/health/live",
        new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthCheckResponseWriter.WriteAsync
        })
    .AllowAnonymous()
    .DisableRateLimiting();
app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = HealthCheckResponseWriter.WriteAsync
        })
    .AllowAnonymous()
    .DisableRateLimiting();
app.MapGet(
        "/",
        () => Results.File(Path.Combine(publicFrontend, "index.html"), "text/html"))
    .AllowAnonymous();
app.MapGet(
        "/operations",
        () => Results.File(Path.Combine(operationsFrontend, "index.html"), "text/html"))
    .AllowAnonymous();
app.MapFallbackToFile(
        "/operations/{*path:nonfile}",
        "index.html",
        new StaticFileOptions { FileProvider = new PhysicalFileProvider(operationsFrontend) })
    .AllowAnonymous();
app.Run();

public partial class Program;
