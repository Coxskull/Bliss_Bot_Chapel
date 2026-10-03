using Bliss.Api.Demonstrations;
using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed class ContactRouteService(ProspectDemonstrationService demonstrations, BlissDbContext database)
{
    public async Task<RouteBoard> ReadAsync(CancellationToken cancellationToken)
    {
        var library = demonstrations.Library();
        var reading = ContactRouteAudit.Read(library.Demonstrations.Select(item => new StoredRoute(
            item.BusinessName,
            item.Suppressed || ((item.ContactRoads?.Count ?? 0) > 0 && item.ContactRoads!.All(road => road.State == "SUPPRESSED")),
            item.FreshnessStatus,
            item.EvidenceSourceUrl,
            item.ContactRoads?.Count ?? 0,
            (item.DeliveryDecisions ?? []).Any(decision => decision.Eligibility == "ELIGIBLE"))).ToList());
        var audits = await database.ContactRouteAudits.AsNoTracking()
            .OrderBy(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        return new RouteBoard(reading, audits);
    }

    public async Task<TransmissionDecision> AuthorizeAsync(
        string? authorization,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var existing = await database.ContactRouteAudits.AsNoTracking().ToListAsync(cancellationToken);
        var decision = ContactRouteAudit.Authorize(
            authorization,
            idempotencyKey,
            existing.Select(item => new StoredTransmissionAudit(item.IdempotencyKey, item.Transmission)).ToList());
        if (!decision.Accepted || decision.Duplicate || !decision.Written)
        {
            return decision;
        }

        var words = (authorization ?? string.Empty).Trim();
        if (words.Length > 160)
        {
            words = words.Substring(0, 160);
        }

        database.ContactRouteAudits.Add(new ContactRouteAuditRow
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = decision.IdempotencyKey,
            Authorization = words,
            Adapter = decision.Adapter,
            Transmission = decision.Transmission,
            Notice = decision.Notice,
            RecordedAt = DateTime.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return decision;
    }
}

public sealed record RouteBoard(RouteReading Reading, IReadOnlyList<ContactRouteAuditRow> Audits);
