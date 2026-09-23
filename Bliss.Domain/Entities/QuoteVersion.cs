namespace Bliss.Domain.Entities;

/// <summary>An immutable commercial snapshot. Revisions insert a new row.</summary>
public class QuoteVersion
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Guid? ParentVersionId { get; set; }
    public int VersionNumber { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal SubtotalAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string RevisionReason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Quote Quote { get; set; } = null!;
    public QuoteVersion? ParentVersion { get; set; }
    public ICollection<QuoteVersion> Revisions { get; set; } = new List<QuoteVersion>();
    public ICollection<QuoteLineItem> LineItems { get; set; } = new List<QuoteLineItem>();
}
