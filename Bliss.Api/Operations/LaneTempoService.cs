using Bliss.Domain.Entities;
using Bliss.Domain.Operations;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public sealed record LaneTempoLaneView(
    string Lane,
    string DisplayName,
    string Tempo,
    decimal? CeilingAmount,
    string? CeilingCurrency,
    string Notice);

public sealed record LaneTempoAuditView(
    Guid Id,
    string Lane,
    string Tempo,
    decimal? CeilingAmount,
    string? CeilingCurrency,
    string Reason,
    string Notice,
    DateTime RecordedAt);

public sealed record LaneTempoBoard(
    string Notice,
    string Delivery,
    bool GreenMeansSend,
    IReadOnlyList<LaneTempoLaneView> Lanes,
    IReadOnlyList<LaneTempoAuditView> Audits);

public sealed class LaneTempoService(BlissDbContext database)
{
    public async Task<LaneTempoBoard> ReadAsync(CancellationToken cancellationToken)
    {
        await EnsureAsync(cancellationToken);
        var lanes = await database.LaneTempoStates.AsNoTracking().ToListAsync(cancellationToken);
        var audits = await database.LaneTempoAudits.AsNoTracking()
            .OrderByDescending(item => item.RecordedAt)
            .Take(20)
            .ToListAsync(cancellationToken);
        return Present(lanes, audits);
    }

    public async Task<LaneTempoBoard> ApplyAsync(
        string? lane,
        string? tempo,
        decimal? ceilingAmount,
        string? ceilingCurrency,
        string? reason,
        CancellationToken cancellationToken)
    {
        var decision = LaneTempo.Apply(lane, tempo, ceilingAmount, ceilingCurrency, reason);
        await EnsureAsync(cancellationToken);
        var row = await database.LaneTempoStates.SingleAsync(item => item.Lane == decision.Lane, cancellationToken);
        var now = DateTime.UtcNow;
        row.Tempo = decision.Tempo;
        row.CeilingAmount = decision.CeilingAmount;
        row.CeilingCurrency = decision.CeilingCurrency;
        row.Notice = decision.Notice;
        row.UpdatedAt = now;
        database.LaneTempoAudits.Add(new LaneTempoAudit
        {
            Id = Guid.NewGuid(),
            Lane = decision.Lane,
            Tempo = decision.Tempo,
            CeilingAmount = decision.CeilingAmount,
            CeilingCurrency = decision.CeilingCurrency,
            Reason = decision.Reason,
            Notice = decision.Notice,
            RecordedAt = now
        });
        await database.SaveChangesAsync(cancellationToken);
        return await ReadAsync(cancellationToken);
    }

    private async Task EnsureAsync(CancellationToken cancellationToken)
    {
        var existing = await database.LaneTempoStates.Select(item => item.Lane).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var lane in LaneTempo.Lanes)
        {
            if (existing.Contains(lane, StringComparer.Ordinal))
            {
                continue;
            }

            database.LaneTempoStates.Add(new LaneTempoState
            {
                Id = Guid.NewGuid(),
                Lane = lane,
                Tempo = LaneTempo.Full,
                Notice = LaneTempo.Describe(lane, LaneTempo.Full, null, null),
                UpdatedAt = now
            });
        }

        if (database.ChangeTracker.HasChanges())
        {
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    private static LaneTempoBoard Present(IReadOnlyList<LaneTempoState> lanes, IReadOnlyList<LaneTempoAudit> audits)
    {
        var ordered = LaneTempo.Lanes
            .Select(name => lanes.Single(item => item.Lane == name))
            .Select(item => new LaneTempoLaneView(
                item.Lane,
                LaneTempo.Display(item.Lane),
                item.Tempo,
                item.CeilingAmount,
                item.CeilingCurrency,
                item.Notice))
            .ToList();
        var history = audits.Select(item => new LaneTempoAuditView(
            item.Id,
            item.Lane,
            item.Tempo,
            item.CeilingAmount,
            item.CeilingCurrency,
            item.Reason,
            item.Notice,
            item.RecordedAt)).ToList();
        return new LaneTempoBoard(LaneTempo.BoardNotice, "NOT_SENT", false, ordered, history);
    }
}
