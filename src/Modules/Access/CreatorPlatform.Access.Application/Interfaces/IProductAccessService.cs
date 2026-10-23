using CreatorPlatform.Access.Application.Dtos;

namespace CreatorPlatform.Access.Application.Interfaces;

public interface IProductAccessService
{
    /// <summary>404 <c>ACCESS_INVALID</c> for an unknown token, 410 <c>ACCESS_REVOKED</c> after a refund.</summary>
    Task<ProductAccessDto> GetAsync(string? token, CancellationToken ct);

    /// <summary>A fresh read URL for the product's current file, counted as a download. Same errors as <see cref="GetAsync"/>.</summary>
    Task<ProductDownloadDto> DownloadAsync(string? token, CancellationToken ct);
}
