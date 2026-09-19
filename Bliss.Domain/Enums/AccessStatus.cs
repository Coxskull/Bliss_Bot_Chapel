namespace Bliss.Domain.Enums;

/// <summary>
/// Phase 1 stores status values as strings so later engines can introduce new codes
/// without a schema rewrite. These enums document the known Phase 1 vocabulary only.
/// </summary>
public enum AccessStatus
{
    Unknown = 0,
    Approved = 1
}
