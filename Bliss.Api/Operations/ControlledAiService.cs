using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Runtime;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Api.Operations;

public sealed record SubmitAiRun(string Kind, Guid? SessionId, Guid? BriefId, Guid? PreviousRunId,
    string Prompt, string IdempotencyKey, bool ConsentToShare);
public sealed class AiQuotaException(string message) : Exception(message);

public sealed class ControlledAiService(BlissDbContext db, BlissRuntimeOptions runtime, IControlledAiProvider provider)
{
    private ControlledAiOptions Options => runtime.ControlledAi;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private string ConfigHash(string kind) => Hash(Serialize(new { Options.Protocol, Options.Provider,
        model = kind == "TEXT" ? Options.TextModel : Options.ImageModel, endpoint = kind == "TEXT" ? Options.TextEndpoint : Options.ImageEndpoint,
        Options.TimeoutSeconds, Options.MaxOutputTokens, Options.MaxResponseBytes }));
    public bool Ready(string kind) => Options.Configured(kind) && provider.Configured(kind);
    public async Task<object> Readiness(CancellationToken ct)
    {
        var day = DateTime.UtcNow.Date;
        var daily = await db.ControlledAiDailyUsage.AsNoTracking().SingleOrDefaultAsync(x => x.Day == day, ct);
        var total = await db.ControlledAiBudgetLedgers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
        var anomaly = await db.ControlledAiRuns.AnyAsync(x => x.CostStatus == "OVER_RESERVATION", ct);
        bool Capacity(decimal reservation) => (daily?.Calls ?? 0) < Options.MaxCallsPerDay
            && (daily?.ReservedUsd ?? 0) + reservation <= Options.DailyBudgetUsd
            && (total?.ReservedUsd ?? 0) + reservation <= Options.TotalBudgetUsd;
        return new { textReady = Ready("TEXT") && !anomaly && Capacity(Options.TextReservationUsd),
            imageReady = Ready("IMAGE") && !anomaly && Capacity(Options.ImageReservationUsd),
            provider = Options.Provider, textModel = Options.TextModel, imageModel = Options.ImageModel,
            callsReservedToday = daily?.Calls ?? 0, reservedTodayUsd = daily?.ReservedUsd ?? 0,
            reservedTotalUsd = total?.ReservedUsd ?? 0, Options.MaxCallsPerDay, Options.DailyBudgetUsd, Options.TotalBudgetUsd,
            Options.TextReservationUsd, Options.ImageReservationUsd, billingAnomaly = anomaly,
            promptVersion = "bliss-workers-v1", liveCallsEnabled = Options.Enabled,
            notice = "AI output is a draft pending human review. Reserved USD is not a receipt. No paid calls are enabled by default." };
    }
    public Task<ControlledAiRun> Run(Guid workspace, Guid id, CancellationToken ct) => RunCore(workspace, id, ct);
    private async Task<ControlledAiRun> RunCore(Guid workspace, Guid id, CancellationToken ct) =>
        await db.ControlledAiRuns.SingleOrDefaultAsync(x => x.WorkspaceId == workspace && x.Id == id, ct)
        ?? throw new WeddingPlannerNotFoundException("AI run was not found.");
    public Task<List<ControlledAiRun>> List(Guid workspace, CancellationToken ct) => db.ControlledAiRuns.AsNoTracking()
        .Where(x => x.WorkspaceId == workspace).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
    public Task<List<ControlledAiEvent>> Events(Guid run, CancellationToken ct) => db.ControlledAiEvents.AsNoTracking()
        .Where(x => x.RunId == run).OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<(ControlledAiRun Run, bool Replay)> Submit(WeddingPlannerWorkspaceResult workspace, SubmitAiRun request,
        string actorType, string actorLabel, string? requestId, CancellationToken ct)
    {
        var kind = (request.Kind ?? "").Trim().ToUpperInvariant(); var prompt = (request.Prompt ?? "").Trim();
        var key = (request.IdempotencyKey ?? "").Trim();
        if (kind is not ("TEXT" or "IMAGE") || key.Length is 0 or > 128 || !request.ConsentToShare || prompt.Length > 2000)
            throw new InvalidOperationException("A supported role, request key, bounded message, and explicit data-sharing consent are required.");
        if (kind == "TEXT")
        {
            if (request.SessionId is null || request.BriefId is not null || prompt.Length == 0)
                throw new InvalidOperationException("Text suggestions need a planning session and a message.");
            if (!await db.WeddingPlannerPlanningSessions.AnyAsync(x => x.WorkspaceId == workspace.WorkspaceId && x.Id == request.SessionId, ct))
                throw new WeddingPlannerNotFoundException("Planning session was not found.");
        }
        else if (request.BriefId is null || request.SessionId is not null || request.PreviousRunId is not null || prompt.Length != 0)
            throw new InvalidOperationException("Image generation takes a saved brief only; prompt overrides are not allowed.");
        var previous = request.PreviousRunId.HasValue ? await db.ControlledAiRuns.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == request.PreviousRunId && x.WorkspaceId == workspace.WorkspaceId && x.SessionId == request.SessionId
            && x.Kind == "TEXT" && x.Status == "SUCCEEDED_PENDING_REVIEW", ct) : null;
        if (request.PreviousRunId.HasValue && previous is null) throw new WeddingPlannerNotFoundException("Previous suggestion was not found in this session.");
        var brief = kind == "IMAGE" ? await db.AcademyProductionBriefs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.BriefId && x.WorkspaceId == workspace.WorkspaceId, ct) : null;
        if (kind == "IMAGE" && brief is null) throw new WeddingPlannerNotFoundException("Production brief was not found.");
        var fingerprint = Hash(Serialize(new { kind, request.SessionId, request.BriefId, request.PreviousRunId, prompt, consent = true }));
        async Task<ControlledAiRun?> Replay() => await db.ControlledAiRuns.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == workspace.WorkspaceId && x.IdempotencyKey == key, ct);
        var existing = await Replay();
        if (existing is not null) { Same(existing.RequestSha256, fingerprint); return (existing, true); }
        if (!Ready(kind)) throw new InvalidOperationException("AI is disabled or provider, model, credentials, and usage budget are not configured. No call was made.");
        var brand = kind == "TEXT" ? await db.WeddingPlannerBrandDna.AsNoTracking().Where(x => x.WorkspaceId == workspace.WorkspaceId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) : null;
        if (kind == "TEXT" && brand?.Status != "CONFIRMED") throw new WeddingPlannerConflictException("Confirm current Brand DNA before requesting AI suggestions.");
        using var savedBrief = brief is null ? null : JsonDocument.Parse(brief.ProductionBriefJson);
        var snapshot = kind == "TEXT" ? Serialize(new { prompt, brandDnaId = brand!.Id, brandVersion = brand.Version,
            brand.BrandName, brand.Niche, brand.Market, brand.Language, brand.Colors, brand.Font, brand.Goals, brand.Restrictions,
            previousReply = previous?.OutputText }) : Serialize(new { briefId = brief!.Id, recipeSha256 = brief.RecipeSha256,
                productionRecipe = savedBrief!.RootElement.GetProperty("generationRecipe").GetString() });
        if (snapshot.Length > 24000) throw new InvalidOperationException("Selected context exceeds the worker input limit.");
        var reserve = kind == "TEXT" ? Options.TextReservationUsd : Options.ImageReservationUsd;
        if (decimal.Round(reserve, 6) != reserve) throw new InvalidOperationException("Configure reservation amounts to at most six decimal places.");
        IDbContextTransaction? tx = null;
        try
        {
            if (db.Database.IsRelational()) tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.ControlledAiRuns.AnyAsync(x => x.CostStatus == "OVER_RESERVATION", ct)) throw new AiQuotaException("A reported charge exceeded its reservation. Billing reconciliation is required before more calls.");
            var day = DateTime.UtcNow.Date;
            var daily = await db.ControlledAiDailyUsage.SingleOrDefaultAsync(x => x.Day == day, ct);
            var ledger = await db.ControlledAiBudgetLedgers.SingleOrDefaultAsync(x => x.Id == 1, ct);
            if ((daily?.Calls ?? 0) >= Options.MaxCallsPerDay || (daily?.ReservedUsd ?? 0) + reserve > Options.DailyBudgetUsd
                || (ledger?.ReservedUsd ?? 0) + reserve > Options.TotalBudgetUsd)
                throw new AiQuotaException("The configured call quota or USD reservation budget is exhausted. No call was queued.");
            if (daily is null) { daily = new ControlledAiDailyUsage { Day = day }; db.Add(daily); }
            if (ledger is null) { ledger = new ControlledAiBudgetLedger(); db.Add(ledger); }
            daily.Calls++; daily.ReservedUsd += reserve; daily.Revision++;
            ledger.Calls++; ledger.ReservedUsd += reserve; ledger.Revision++;
            var run = new ControlledAiRun
            {
                Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, SessionId = request.SessionId, BriefId = request.BriefId,
                Kind = kind, Provider = Options.Provider, Model = kind == "TEXT" ? Options.TextModel : Options.ImageModel,
                InputSnapshotJson = snapshot, InputSha256 = Hash(snapshot), RequestSha256 = fingerprint, ExecutionConfigSha256 = ConfigHash(kind),
                IdempotencyKey = key, ActorLabel = actorLabel, ReservedUsd = reserve, CreatedAt = DateTime.UtcNow
            };
            db.Add(run); AddEvent(run, "QUEUED", actorLabel);
            db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent { Id = Guid.NewGuid(), AdvertiserId = workspace.AdvertiserId,
                WorkspaceId = workspace.WorkspaceId, SessionId = request.SessionId, Action = "AI_RUN_QUEUED", ActorType = actorType,
                ActorLabel = actorLabel, Outcome = "RESERVED", RequestId = requestId, Detail = $"AI run {run.Id}; role {kind}.", OccurredAt = run.CreatedAt });
            await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return (run, false);
        }
        catch (Exception ex) when (ReservationConflict(ex))
        {
            if (tx is not null) await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear();
            var winner = await Replay(); if (winner is not null) { Same(winner.RequestSha256, fingerprint); return (winner, true); }
            throw new WeddingPlannerConflictException("Usage reservation changed concurrently. Retry this same request key after refreshing.");
        }
        finally { if (tx is not null) await tx.DisposeAsync(); }
    }
    private static void Same(string original, string current)
    { if (original != current) throw new WeddingPlannerConflictException("This request key belongs to different AI input. Use a new key."); }
    private static bool ReservationConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure }) return true;
        return false;
    }
    private void AddEvent(ControlledAiRun run, string action, string actor) => db.ControlledAiEvents.Add(new ControlledAiEvent
        { Id = Guid.NewGuid(), RunId = run.Id, Action = action, ActorLabel = actor, CreatedAt = DateTime.UtcNow });
    public async Task<ControlledAiRun> Cancel(Guid workspace, Guid id, string actor, CancellationToken ct)
    {
        var run = await Run(workspace, id, ct);
        if (run.Status is "CANCEL_REQUESTED" or "CANCELLED") return run;
        if (run.Status is not ("QUEUED" or "RUNNING")) throw new WeddingPlannerConflictException("This run is already terminal and cannot be cancelled.");
        run.Status = run.Status == "QUEUED" ? "CANCELLED" : "CANCEL_REQUESTED";
        if (run.Status == "CANCELLED") run.FinishedAt = DateTime.UtcNow;
        AddEvent(run, run.Status, actor);
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) when (ReservationConflict(ex)) { throw new WeddingPlannerConflictException("Run state changed. Refresh before cancelling."); }
        return run;
    }
    public async Task<bool> RunOne(CancellationToken stopping)
    {
        var now = DateTime.UtcNow;
        var expired = await db.ControlledAiRuns.FirstOrDefaultAsync(x => (x.Status == "RUNNING" || x.Status == "CANCEL_REQUESTED") && x.DeadlineAt < now, stopping);
        if (expired is not null)
        {
            expired.Status = "UNKNOWN"; expired.FailureCode = "EXECUTION_DEADLINE_EXPIRED"; expired.FinishedAt = now;
            AddEvent(expired, "UNKNOWN", "AI_RUNNER");
            try { await db.SaveChangesAsync(stopping); } catch (Exception ex) when (ReservationConflict(ex)) { db.ChangeTracker.Clear(); }
            return true;
        }
        if (!Options.Enabled) return false;
        var run = await db.ControlledAiRuns.Where(x => x.Status == "QUEUED").OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(stopping);
        if (run is null) return false;
        run.Status = "RUNNING"; run.StartedAt = now; run.DeadlineAt = now.AddSeconds(Options.TimeoutSeconds + 5);
        AddEvent(run, "RUNNING", "AI_RUNNER");
        try { await db.SaveChangesAsync(stopping); } catch (Exception ex) when (ReservationConflict(ex)) { db.ChangeTracker.Clear(); return true; }
        AiProviderResult? result = null; string status = "SUCCEEDED_PENDING_REVIEW", failure = "";
        var billingAnomaly = await db.ControlledAiRuns.AnyAsync(x => x.CostStatus == "OVER_RESERVATION", stopping);
        if (billingAnomaly) { status = "FAILED"; failure = "BILLING_RECONCILIATION_REQUIRED"; }
        else if (!Ready(run.Kind) || run.ExecutionConfigSha256 != ConfigHash(run.Kind)) { status = "FAILED"; failure = "EXECUTION_CONFIG_CHANGED"; }
        else
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping); timeout.CancelAfter(TimeSpan.FromSeconds(Options.TimeoutSeconds));
            async Task Monitor()
            {
                while (!timeout.IsCancellationRequested)
                {
                    await Task.Delay(100, timeout.Token);
                    var state = await db.ControlledAiRuns.AsNoTracking().Where(x => x.Id == run.Id).Select(x => x.Status).SingleAsync(timeout.Token);
                    if (state == "CANCEL_REQUESTED") { timeout.Cancel(); break; }
                }
            }
            var monitor = Monitor();
            try
            {
                result = await provider.ExecuteAsync(new AiProviderRequest(run.Id, run.Kind, run.Model, run.InputSnapshotJson, Options.MaxOutputTokens), timeout.Token).WaitAsync(timeout.Token);
                if (run.Kind == "TEXT" && (string.IsNullOrWhiteSpace(result.Text) || result.Text.Length > 8000)) throw new AiProviderException("INVALID_TEXT_OUTPUT");
                if (run.Kind == "IMAGE") CandidatePng.Validate(result.Image ?? []);
                if (result.RequestId.Length > 200 || result.InputTokens < 0 || result.OutputTokens < 0 || result.ActualUsd < 0) throw new AiProviderException("INVALID_USAGE_RECEIPT");
            }
            catch (OperationCanceledException) { status = stopping.IsCancellationRequested ? "UNKNOWN" : "TIMED_OUT"; failure = stopping.IsCancellationRequested ? "HOST_INTERRUPTED" : "CALL_INTERRUPTED"; result = null; }
            catch (AiProviderException ex) { status = "FAILED"; failure = ex.Code; result = null; }
            catch (Exception) { status = "FAILED"; failure = "PROVIDER_FAILURE"; result = null; }
            finally
            {
                timeout.Cancel();
                try { await monitor; } catch (OperationCanceledException) { }
            }
        }
        var receipt = result;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await db.Entry(run).ReloadAsync(CancellationToken.None);
            if (run.Status is not ("RUNNING" or "CANCEL_REQUESTED")) return true;
            if (run.Status == "CANCEL_REQUESTED") { status = "CANCELLED"; failure = "CANCEL_REQUESTED"; result = null; }
            run.Status = status; run.FailureCode = failure; run.FinishedAt = DateTime.UtcNow;
            if (result is not null)
            {
                run.OutputText = run.Kind == "TEXT" ? result.Text : ""; run.OutputImage = run.Kind == "IMAGE" ? result.Image : null;
                run.OutputSha256 = run.Kind == "TEXT" ? Hash(run.OutputText) : Convert.ToHexString(SHA256.HashData(result.Image!)).ToLowerInvariant();
            }
            if (receipt is not null)
            {
                run.ProviderRequestId = receipt.RequestId; run.InputTokens = receipt.InputTokens; run.OutputTokens = receipt.OutputTokens;
                run.ActualUsd = receipt.ActualUsd.HasValue ? decimal.Round(receipt.ActualUsd.Value, 6) : null;
                run.CostStatus = receipt.ActualUsd > run.ReservedUsd ? "OVER_RESERVATION" : receipt.ActualUsd.HasValue ? "PROVIDER_REPORTED" : "UNRECORDED";
            }
            var outcome = new ControlledAiEvent { Id = Guid.NewGuid(), RunId = run.Id, Action = status, ActorLabel = "AI_RUNNER", CreatedAt = DateTime.UtcNow }; db.Add(outcome);
            try { await db.SaveChangesAsync(CancellationToken.None); return true; }
            catch (Exception ex) when (ReservationConflict(ex)) { db.Entry(outcome).State = EntityState.Detached; }
        }
        return true;
    }
}

public sealed class ControlledAiWorker(IServiceScopeFactory scopes, ILogger<ControlledAiWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
                try
                {
                    using var scope = scopes.CreateScope();
                    if (await scope.ServiceProvider.GetRequiredService<ControlledAiService>().RunOne(stoppingToken)) continue;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception) { logger.LogWarning("Controlled AI worker could not persist a transition. Expired runs require recovery; provider calls are not retried."); }
            try { await Task.Delay(500, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
