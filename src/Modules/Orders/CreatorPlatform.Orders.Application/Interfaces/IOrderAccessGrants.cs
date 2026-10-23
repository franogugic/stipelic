namespace CreatorPlatform.Orders.Application.Interfaces;

/// <summary>The buyer's personal access link (<c>products.access_grants</c>). Both calls run inside the payment webhook's
/// transaction, so the link exists exactly when the order is paid and is off exactly when it is refunded.</summary>
public interface IOrderAccessGrants
{
    /// <summary>Adds the order's grant (flushed with the order) and returns its token. One per order (unique index).</summary>
    string CreateForPaidOrder(int creatorId, int productId, int orderId, string buyerEmail, DateTimeOffset createdAt);

    /// <summary>Turns the order's link off; the first revocation time is kept.</summary>
    Task RevokeForOrderAsync(int orderId, DateTimeOffset revokedAt, CancellationToken ct);
}
