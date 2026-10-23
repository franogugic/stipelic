using CreatorPlatform.Access.Application.Dtos;

namespace CreatorPlatform.Access.Application.Interfaces;

public interface IProductAccessService
{
    /// <summary>404 <c>ACCESS_INVALID</c> for an unknown token (nothing else in the body); 410 <c>ACCESS_REVOKED</c> after a
    /// refund, with <see cref="ProductAccessRevokedDto"/> in the body.</summary>
    Task<ProductAccessDto> GetAsync(string? token, CancellationToken ct);

    /// <summary>A fresh read URL for the product's current file, counted as a download. Same errors as <see cref="GetAsync"/>.</summary>
    Task<ProductDownloadDto> DownloadAsync(string? token, CancellationToken ct);
}
