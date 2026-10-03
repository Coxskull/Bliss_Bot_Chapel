namespace Bliss.Domain.WeddingPlanner;

public sealed record CreativeRecord(Guid WorkspaceId, string IdempotencyKey, string Status, string Title);

public sealed record CreativeDecision(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string Status,
    string Title,
    string ActorType,
    bool Duplicate,
    bool CampaignReady,
    bool MatchWritten,
    bool PriceInvented,
    int ModelCalls);

/// <summary>
/// A human records one creative decision inside an open workspace.
/// Campaign ready is refused. No price is invented. No model is called.
/// </summary>
public static class CreativeApproval
{
    public const string HumanApproved = "HUMAN_APPROVED";
    public const string Withheld = "WITHHELD";

    public const string Notice =
        "A human records one creative decision inside an open workspace. A discovered business is not opened. Campaign ready is refused. A price is not invented. Six roles are not called. A match is not written. Green does not send. Delivery remains NOT_SENT.";

    public const string DuplicateNotice =
        "That creative decision is already recorded. The human was not asked again. Campaign ready stays refused. Delivery remains NOT_SENT.";

    public static CreativeDecision Decide(
        Guid workspaceId,
        string? title,
        string? decision,
        string? actorType,
        string? idempotencyKey,
        IReadOnlyList<CreativeRecord>? existing)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new InvalidOperationException("An open workspace is required. A discovered business is not opened.");
        }

        if (existing is null)
        {
            throw new InvalidOperationException("A creative decision needs the stored rows. None is invented.");
        }

        var name = (title ?? string.Empty).Trim();
        if (name.Length < 3 || name.Length > 120)
        {
            throw new InvalidOperationException("A human title is required. None was generated.");
        }

        RequireKey(idempotencyKey);
        var key = idempotencyKey!.Trim();
        var actor = (actorType ?? string.Empty).Trim().ToUpperInvariant();
        if (actor is not "OPERATOR" and not "ADVERTISER")
        {
            throw new InvalidOperationException("A human must decide. A model was not called.");
        }

        var choice = (decision ?? string.Empty).Trim().ToUpperInvariant();
        if (choice is "CAMPAIGN_READY" or "WON")
        {
            throw new InvalidOperationException("Campaign ready is refused. This is not a placement.");
        }

        if (choice is not "APPROVE" and not "WITHHOLD")
        {
            throw new InvalidOperationException("The human decision is approve or withhold. None was invented.");
        }

        var prior = existing.FirstOrDefault(item =>
            item.WorkspaceId == workspaceId
            && string.Equals(item.IdempotencyKey, key, StringComparison.Ordinal));
        if (prior is not null)
        {
            return new CreativeDecision(
                DuplicateNotice,
                false,
                "NOT_SENT",
                prior.Status,
                prior.Title,
                actor,
                true,
                false,
                false,
                false,
                0);
        }

        var status = choice == "APPROVE" ? HumanApproved : Withheld;
        return new CreativeDecision(
            "A human recorded " + status + ". Campaign ready was refused. No price was invented. Six roles were not called. A match was not written. Delivery remains NOT_SENT.",
            false,
            "NOT_SENT",
            status,
            name,
            actor,
            false,
            false,
            false,
            false,
            0);
    }

    public static IReadOnlyList<CreativeRecord> Visible(Guid workspaceId, IReadOnlyList<CreativeRecord>? rows)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new InvalidOperationException("An open workspace is required. A discovered business is not opened.");
        }

        if (rows is null)
        {
            throw new InvalidOperationException("A creative reading needs the stored rows. None is invented.");
        }

        return rows.Where(item => item.WorkspaceId == workspaceId).ToList();
    }

    public static void RequireKey(string? idempotencyKey)
    {
        var key = (idempotencyKey ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("An idempotency key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                throw new InvalidOperationException("An idempotency key is required. None is invented.");
            }
        }
    }
}
