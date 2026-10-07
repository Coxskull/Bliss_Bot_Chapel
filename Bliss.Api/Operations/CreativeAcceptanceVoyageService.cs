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
    ICreativeImageGenerator generator)
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

public sealed record CreativeAcceptanceVoyageHistory(
    Guid Id,
    string VoyageKey,
    CreativeAcceptanceVoyage Report,
    DateTime RecordedAt);

public sealed record CreativeAcceptanceVoyageWrite(
    CreativeAcceptanceVoyageHistory Voyage,
    bool Duplicate,
    bool Written);
