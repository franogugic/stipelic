using CreatorPlatform.Products.Application.Dtos;

namespace CreatorPlatform.Products.Application.Interfaces;

public interface IOrderContextProvider
{
    Task<Dictionary<int, ProductRevenueDto>> GetProductRevenueByCreatorIdAsync(int creatorId, CancellationToken ct);
}
