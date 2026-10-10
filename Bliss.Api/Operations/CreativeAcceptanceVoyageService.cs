using System.Text.Json;
using Bliss.Api.Runtime;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class CreativeAcceptanceVoyageService(
    BlissDbContext database,
    BlueprintPhaseService phases,
    BlissRuntimeOptions runtime,
    ICreativeImageGenerator generator,
    CreativeGenerationService generation)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CreativeAcceptanceVoyageHistory>> ReadAsync(
        CancellationToken cancellationToken)
    {
        var rows = await database.CreativeAcceptanceVoyages.AsNoTracking()
            .OrderByDescending(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToHistory).ToList();
    }

    public async Task<CreativeAcceptanceVoyageWrite> RunAsync(
        CreativeAcceptanceVoyageRequest? request,
        CancellationToken cancellationToken)
    {
        var campaign = new AcceptanceCampaignBrief(
            (request?.VoyageKey ?? string.Empty).Trim(),
            (request?.AdvertiserName ?? string.Empty).Trim(),
            (request?.City ?? string.Empty).Trim(),
            (request?.Market ?? string.Empty).Trim(),
            (request?.Niche ?? string.Empty).Trim(),
            (request?.Objective ?? string.Empty).Trim(),
            (request?.InventoryProductId ?? string.Empty).Trim());
        var prior = await database.CreativeAcceptanceVoyages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.VoyageKey == campaign.VoyageKey, cancellationToken);
        if (prior is not null)
        {
            return new CreativeAcceptanceVoyageWrite(ToHistory(prior), true, false);
        }

        var reading = await phases.ReadAsync(cancellationToken);
        var product = reading.Catalog.Products.FirstOrDefault(item =>
            item.ProductId.Equals(campaign.InventoryProductId, StringComparison.OrdinalIgnoreCase));
        if (product is null)
        {
            throw new InvalidOperationException("That inventory product is not stored. None was invented.");
        }

        var options = runtime.CreativeGeneration;
        var report = CreativeAcceptance.Run(
            campaign,
            reading.References,
            product,
            reading.Catalog.Slots,
            options.Provider,
            options.Model,
            generator.Configured);
        if (report.ProviderJob.Status == "READY_FOR_GENERATION")
        {
            report = await ContinueGenerationAsync(report, campaign, reading.References, cancellationToken);
        }
        var row = new CreativeAcceptanceVoyageRow
        {
            Id = Guid.NewGuid(),
            VoyageKey = campaign.VoyageKey,
            AdvertiserName = campaign.AdvertiserName,
            Market = campaign.Market,
            Niche = campaign.Niche,
            InventoryProductId = campaign.InventoryProductId,
            Status = report.Status,
            ReportJson = JsonSerializer.Serialize(report, JsonOptions),
            ModelCalls = report.ModelCalls,
            CampaignReady = report.CampaignReady,
            Delivery = report.Delivery,
            RecordedAt = DateTime.UtcNow
        };
        database.CreativeAcceptanceVoyages.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        return new CreativeAcceptanceVoyageWrite(ToHistory(row), false, true);
    }

    public async Task<CreativeAcceptanceVoyageWrite> RecordVisualQualityAsync(
        string voyageKey,
        CreativeAcceptanceVisualReviewRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Visual review evidence is required.");
        }

        var row = await FindVoyageAsync(voyageKey, cancellationToken);
        var report = DeserializeReport(row);
        report = CreativeAcceptance.ApplyVisualQualityEvidence(
            report,
            request.ReportedScore,
            request.DefectCodes,
            request.VisualEvidenceRecorded,
            request.BrandDnaCompliant,
            request.OriginalityConfirmed,
            request.InventoryGeometryVerified,
            request.QrVerified);
        return await SaveReportAsync(row, report, cancellationToken);
    }

    public async Task<CreativeAcceptanceVoyageWrite> RecordHumanReviewAsync(
        string voyageKey,
        string reviewerId,
        CreativeAcceptanceHumanReviewRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new InvalidOperationException("Human review decision and notes are required.");
        }

        var row = await FindVoyageAsync(voyageKey, cancellationToken);
        var report = DeserializeReport(row);
        report = CreativeAcceptance.RecordHumanReview(
            report,
            reviewerId,
            request.Decision ?? string.Empty,
            request.Notes ?? string.Empty,
            DateTimeOffset.UtcNow);
        return await SaveReportAsync(row, report, cancellationToken);
    }

    private async Task<CreativeAcceptanceVoyageRow> FindVoyageAsync(
        string voyageKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voyageKey))
        {
            throw new InvalidOperationException("A voyage key is required.");
        }

        return await database.CreativeAcceptanceVoyages
            .SingleOrDefaultAsync(item => item.VoyageKey == voyageKey.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("The acceptance voyage was not found.");
    }

    private static CreativeAcceptanceVoyage DeserializeReport(CreativeAcceptanceVoyageRow row) =>
        JsonSerializer.Deserialize<CreativeAcceptanceVoyage>(row.ReportJson, JsonOptions)
        ?? throw new InvalidOperationException("The stored voyage report is unreadable. None was invented.");

    private async Task<CreativeAcceptanceVoyageWrite> SaveReportAsync(
        CreativeAcceptanceVoyageRow row,
        CreativeAcceptanceVoyage report,
        CancellationToken cancellationToken)
    {
        row.Status = report.Status;
        row.ReportJson = JsonSerializer.Serialize(report, JsonOptions);
        row.CampaignReady = false;
        row.Delivery = "NOT_SENT";
        await database.SaveChangesAsync(cancellationToken);
        return new CreativeAcceptanceVoyageWrite(ToHistory(row), false, true);
    }

    private async Task<CreativeAcceptanceVoyage> ContinueGenerationAsync(
        CreativeAcceptanceVoyage report,
        AcceptanceCampaignBrief campaign,
        IReadOnlyList<AcademyReferenceRecord> references,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await generation.GenerateAsync(
                campaign.VoyageKey,
                campaign.Niche,
                campaign.AdvertiserName,
                null,
                null,
                null,
                null,
                campaign.Market,
                null,
                campaign.Objective + ". Required inventory product " + campaign.InventoryProductId + ".",
                true,
                false,
                cancellationToken);
            return CreativeAcceptance.WithGeneration(
                report,
                new AcceptanceGenerationOutcome(
                    true,
                    decision.Id.ToString(),
                    decision.ImagePath,
                    decision.ModelCalls,
                    decision.CostStatus,
                    decision.Cost,
                    string.Empty,
                    decision.Notice),
                references);
        }
        catch (InvalidOperationException exception)
        {
            return CreativeAcceptance.WithGeneration(
                report,
                new AcceptanceGenerationOutcome(
                    false,
                    string.Empty,
                    string.Empty,
                    0,
                    "UNRECORDED",
                    null,
                    string.Empty,
                    exception.Message),
                references);
        }
    }

    private static CreativeAcceptanceVoyageHistory ToHistory(CreativeAcceptanceVoyageRow row) =>
        new(
            row.Id,
            row.VoyageKey,
            JsonSerializer.Deserialize<CreativeAcceptanceVoyage>(row.ReportJson, JsonOptions)
                ?? throw new InvalidOperationException("The stored voyage report is unreadable. None was invented."),
            row.RecordedAt);
}

public sealed record CreativeAcceptanceVoyageRequest(
    string? VoyageKey,
    string? AdvertiserName,
    string? City,
    string? Market,
    string? Niche,
    string? Objective,
    string? InventoryProductId);

public sealed record CreativeAcceptanceVisualReviewRequest(
    int ReportedScore,
    IReadOnlyList<string>? DefectCodes,
    bool VisualEvidenceRecorded,
    bool BrandDnaCompliant,
    bool OriginalityConfirmed,
    bool InventoryGeometryVerified,
    bool QrVerified);

public sealed record CreativeAcceptanceHumanReviewRequest(
    string? Decision,
    string? Notes);

public sealed record CreativeAcceptanceVoyageHistory(
    Guid Id,
    string VoyageKey,
    CreativeAcceptanceVoyage Report,
    DateTime RecordedAt);

public sealed record CreativeAcceptanceVoyageWrite(
    CreativeAcceptanceVoyageHistory Voyage,
    bool Duplicate,
    bool Written);
