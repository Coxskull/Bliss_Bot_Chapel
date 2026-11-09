using Bliss.Infrastructure.Financials;
using Npgsql;

// Independent staging-only host, never registered in the production Bliss API.
// Default disabled. The existing ALPHA-346 verified sandbox Payment Link is
// the ONLY accepted payment source. No customer, campaign, journal or fulfillment.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 1_000_000;
});
var enabled = builder.Configuration["ALPHA346_STRIPE_SANDBOX_WEBHOOK_ENABLED"] == "true";
string? signingSecret = null;
if (enabled)
{
    if (!builder.Environment.IsEnvironment("AlphaStripeSandbox") ||
        builder.Configuration["ALPHA346_STRIPE_ACCOUNT_ID"] != "acct_1UOn83H23maIy3rz")
        throw new InvalidOperationException("ALPHA-346 sandbox environment/account mismatch.");
    signingSecret = builder.Configuration["ALPHA346_STRIPE_WEBHOOK_SECRET"];
    if (signingSecret is null ||
        !signingSecret.StartsWith("whsec_", StringComparison.Ordinal) ||
        signingSecret.Length < 16 ||
        signingSecret.Any(char.IsWhiteSpace))
        throw new InvalidOperationException("A valid sandbox webhook signing secret is required.");

    var connection = builder.Configuration["ALPHA346_STRIPE_RECEIPTS_DATABASE"];
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("Dedicated sandbox receipt database is required.");
    var db = new NpgsqlConnectionStringBuilder(connection);
    var localFixture = db.Database == "financial_test" &&
        db.Host is "127.0.0.1" or "localhost";
    var isolatedHosted = db.Database == "alpha346_stripe_sandbox" &&
        db.Username == "alpha346_webhook_ingest" &&
        db.SslMode == SslMode.VerifyFull &&
        db.Host is not ("127.0.0.1" or "localhost" or "::1") &&
        !string.IsNullOrWhiteSpace(db.Host) &&
        db.Host.Contains('.', StringComparison.Ordinal);
    if (!localFixture && !isolatedHosted)
        throw new InvalidOperationException("Forbidden shared/production database or insecure test database.");
    builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
    builder.Services.AddSingleton<PlanningSandboxStripeReceiptStore>();
}
var app = builder.Build();
app.MapGet("/internal/alpha346/sandbox-health", () =>
    enabled ? Results.Json(new { state = "SANDBOX_RECEIPT_GATE_ENABLED", payments = "TEST_ONLY" })
            : Results.NotFound());
app.MapPost("/internal/alpha346/stripe-sandbox-webhook", async (HttpContext context) =>
{
    // Fast fail closed even if someone deploys this project without the
    // separate explicit test-mode environment and account configuration.
    if (!enabled) return Results.NotFound();
    if (context.Request.ContentLength is > 1_000_000 || 
        !context.Request.HasJsonContentType()) return Results.BadRequest();
    var header = context.Request.Headers["Stripe-Signature"].ToString();
    if (header.Length is 0 or > 4096) return Results.BadRequest();
    try
    {
        // Read the RAW body, never parsed/re-encoded prior to HMAC verification.
        await using var body = new MemoryStream();
        var buffer = new byte[16384];
        while (true)
        {
            var n = await context.Request.Body.ReadAsync(buffer, context.RequestAborted);
            if (n == 0) break;
            if (body.Length + n > 1_000_000) return Results.StatusCode(413);
            await body.WriteAsync(buffer.AsMemory(0, n), context.RequestAborted);
        }
        var store = context.RequestServices.GetRequiredService<PlanningSandboxStripeReceiptStore>();
        var receipt = await store.ObserveSigned(
            body.ToArray(), header, signingSecret!, DateTimeOffset.UtcNow,
            context.RequestAborted);
        // Only acknowledged, durable and non-conflicting test events get 2xx.
        // NO customer action, accounting revenue or capture is ever triggered.
        return receipt.Decision switch
        {
            "RECORD_SANDBOX_TEST_EVIDENCE_ONLY" or
            "DUPLICATE_ALREADY_RECORDED" or
            "SAME_PAYMENT_NO_DOUBLE_RECEIPT" or
            "STALE_PENDING_IGNORED" => Results.NoContent(),
            _ => Results.Conflict()
        };
    }
    catch (Exception exception) when (exception is ArgumentException
        or System.Text.Json.JsonException or KeyNotFoundException
        or FormatException)
    {
        return Results.BadRequest(); // Never disclose payload, secret or PII.
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(503);
    }
    catch (Exception)
    {
        // Do not ACK a Stripe webhook on an unknown database/receipt failure:
        // allowing Stripe to retry preserves evidence without double capture.
        return Results.StatusCode(503);
    }
});
await app.RunAsync();
