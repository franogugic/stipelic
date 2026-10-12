using CreatorPlatform.Products.Application.Dtos;

namespace CreatorPlatform.Products.Application.Interfaces;

public interface IOrderContextProvider
{
    Task<Dictionary<int, ProductRevenueDto>> GetProductRevenueByCreatorIdAsync(int creatorId, CancellationToken ct);

    /// <summary>Paid orders per month for every product of the creator that sold since <paramref name="firstMonth"/>,
    /// in one grouped query: <see cref="ProductSalesTrend.Months"/> counts per product, oldest first. Orders count by
    /// PaidAt (CreatedAt for orders from before PaidAt was recorded), months are UTC. Products without sales are absent.</summary>
    Task<Dictionary<int, int[]>> GetProductSalesTrendsAsync(int creatorId, DateTimeOffset firstMonth, CancellationToken ct);
}
