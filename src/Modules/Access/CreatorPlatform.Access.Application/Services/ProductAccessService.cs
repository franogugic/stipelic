using CreatorPlatform.Access.Application.Dtos;
using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Orders.Application.Receipts;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Access.Application.Services;

/// <summary>
/// The personal links (<c>products.access_grants</c>) from order and lead-magnet emails. The token is the only secret:
/// anyone holding it gets the file, so it is 32 random bytes and the endpoints are rate limited per IP. A link never
/// expires on its own; a refund turns a buyer's off. Each download is a fresh signed URL, valid for minutes, always to
/// the product's current file.
/// </summary>
public sealed class ProductAccessService : IProductAccessService
{
    public const string InvalidCode = "ACCESS_INVALID";
    public const string RevokedCode = "ACCESS_REVOKED";

    // AccessGrant.NewToken: 32 bytes, base64url without padding.
    private const int TokenLength = 43;

    private readonly IAccessGrantReader _reader;
    private readonly IProductFileStorage _storage;
    private readonly MediaOptions _mediaOptions;

    public ProductAccessService(IAccessGrantReader reader, IProductFileStorage storage, IOptions<MediaOptions> mediaOptions)
    {
        _reader = reader;
        _storage = storage;
        _mediaOptions = mediaOptions.Value;
    }

    public async Task<ProductAccessDto> GetAsync(string? token, CancellationToken ct)
    {
        var grant = await GetGrantAsync(token, ct);

        return new ProductAccessDto(
            grant.ProductName,
            grant.FileName!,
            grant.FileSizeBytes!.Value,
            Brand(grant),
            grant.OrderPublicId is null ? "capture" : "order",
            OrderNumber(grant),
            grant.PaidAt);
    }

    public async Task<ProductDownloadDto> DownloadAsync(string? token, CancellationToken ct)
    {
        if (!IsWellFormed(token))
            throw Invalid();

        var target = await _reader.RecordDownloadAsync(token!, DateTimeOffset.UtcNow, ct);
        if (target is null)
        {
            // Not counted: tell an unknown link from a revoked one (the rare path, so a second query is fine).
            await GetGrantAsync(token, ct);
            throw Invalid();
        }

        var url = _storage.GenerateDownloadSasUri(
            target.BlobPath, target.FileName, target.ContentType, TimeSpan.FromMinutes(_mediaOptions.DownloadUrlExpiryMinutes));
        return new ProductDownloadDto(url.ToString());
    }

    /// <summary>The grant behind a usable link, else the 404 / 410 the download page shows.</summary>
    private async Task<AccessGrantView> GetGrantAsync(string? token, CancellationToken ct)
    {
        // A malformed token cannot exist: answer without a database query.
        if (!IsWellFormed(token))
            throw Invalid();

        var grant = await _reader.GetAsync(token!, ct) ?? throw Invalid();
        if (grant.RevokedAt is { } revokedAt)
        {
            // Only a refund turns a link off, at the refund: the revocation time is the refund time.
            throw new GoneException(
                "This download link was turned off because the order was refunded.",
                RevokedCode,
                new ProductAccessRevokedDto(grant.ProductName, Brand(grant), OrderNumber(grant), revokedAt));
        }
        // A product people hold links to keeps its file (ProductService refuses to remove it); defensive only.
        if (grant.FileName is null || grant.FileSizeBytes is null)
            throw Invalid();

        return grant;
    }

    private static ProductAccessBrandDto Brand(AccessGrantView grant) => new(
        string.IsNullOrWhiteSpace(grant.BrandName) ? grant.CreatorName : grant.BrandName,
        grant.BrandColor,
        grant.LogoUrl);

    private static string? OrderNumber(AccessGrantView grant) =>
        grant.OrderPublicId is { } orderPublicId ? OrderNumbers.From(orderPublicId) : null;

    private static bool IsWellFormed(string? token) =>
        token is { Length: TokenLength } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static NotFoundException Invalid() =>
        new("This download link is not valid. Check that you copied the whole link from your email.", InvalidCode);
}
