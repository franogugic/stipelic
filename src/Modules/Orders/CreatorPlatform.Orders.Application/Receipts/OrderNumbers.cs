namespace CreatorPlatform.Orders.Application.Receipts;

/// <summary>The order reference shown to buyers and creators: the first 8 hex digits of the order's public id,
/// upper-case (e.g. "A1B2C3D4"). Stable, never the internal id, and short enough to read out to support.</summary>
public static class OrderNumbers
{
    public static string From(Guid orderPublicId) => orderPublicId.ToString("N")[..8].ToUpperInvariant();
}
