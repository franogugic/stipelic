using CreatorPlatform.Products.Application.Dtos;

namespace CreatorPlatform.Products.Application.Interfaces;

public interface IProductService
{
    Task<ProductResponseDto> CreateAsync(
        string slug,
        int ownerUserId,
        CreateProductRequestDto request,
        CancellationToken ct);

    Task<List<ProductResponseDto>> ListAsync(
        string slug,
        int ownerUserId,
        CancellationToken ct);

    Task<ProductResponseDto> UpdateAsync(
        string slug,
        Guid productPublicId,
        int ownerUserId,
        UpdateProductRequestDto request,
        CancellationToken ct);

    Task ArchiveAsync(
        string slug,
        Guid productPublicId,
        int ownerUserId,
        CancellationToken ct);
}
