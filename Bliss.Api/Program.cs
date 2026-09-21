using System.Text.Json.Serialization;
using Bliss.Api.Security;
using Bliss.Infrastructure.DependencyInjection;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var authentication = builder.Configuration
    .GetSection(BlissAuthenticationOptions.SectionName)
    .Get<BlissAuthenticationOptions>() ?? new BlissAuthenticationOptions();

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
builder.Services.AddScoped<OperatorIdentity>();
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
        options.DefaultChallengeScheme = authentication.Enabled
            ? OpenIdConnectDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
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
        Description = "Standalone Bliss API: controlled ingestion, matching, human review, and campaign placement planning."
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
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

app.UseDefaultFiles();
app.UseStaticFiles();

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
app.MapFallbackToFile("index.html").AllowAnonymous();
app.Run();

public partial class Program;
