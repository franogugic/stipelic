using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Products.Application.Services;

public sealed class ProductService : IProductService
{
    private const int ProductNameMaxLength = 100;
    private const int ProductDescriptionMaxLength = 2000;
    private const int ProductAccessUrlMaxLength = 2000;
    private const int ProductThumbnailUrlMaxLength = 2000;

    private readonly IProductRepository _productRepository;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IProductsUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository productRepository,
        ICreatorContextProvider creatorContextProvider,
        IProductsUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _creatorContextProvider = creatorContextProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductResponseDto> CreateAsync(
        string slug,
        int ownerUserId,
        CreateProductRequestDto request,
        CancellationToken ct)
    {
        var (creatorId, maxProducts, activeProductCount) = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var name = NormalizeName(request.Name);
        var description = NormalizeOptionalText(request.Description, ProductDescriptionMaxLength);
        var priceCents = ValidatePriceCents(request.PriceCents);
        var type = ParseProductType(request.Type);
        var accessUrl = NormalizeOptionalUrl(request.AccessUrl, ProductAccessUrlMaxLength);
        var thumbnailUrl = NormalizeOptionalUrl(request.ThumbnailUrl, ProductThumbnailUrlMaxLength);

        if (maxProducts >= 0 && activeProductCount >= maxProducts)
            throw new BadRequestException(
                $"Your plan allows a maximum of {maxProducts} product(s). Archive existing products or upgrade your plan.");

        var now = DateTimeOffset.UtcNow;
        var product = Product.Create(creatorId, name, description, priceCents, type, accessUrl, thumbnailUrl, now);

        await _productRepository.AddAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(product);
    }

    public async Task<List<ProductResponseDto>> ListAsync(
        string slug,
        int ownerUserId,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var products = await _productRepository.ListByCreatorIdAsync(creatorId, ct);

        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductResponseDto> UpdateAsync(
        string slug,
        Guid productPublicId,
        int ownerUserId,
        UpdateProductRequestDto request,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var product = await _productRepository.GetByPublicIdAndCreatorIdForUpdateAsync(productPublicId, creatorId, ct);
        if (product is null)
            throw new NotFoundException("Product not found.");

        if (product.Status == ProductStatus.Archived)
            throw new BadRequestException("Archived products cannot be updated.");

        var name = NormalizeName(request.Name);
        var description = NormalizeOptionalText(request.Description, ProductDescriptionMaxLength);
        var priceCents = ValidatePriceCents(request.PriceCents);
        var type = ParseProductType(request.Type);
        var accessUrl = NormalizeOptionalUrl(request.AccessUrl, ProductAccessUrlMaxLength);
        var thumbnailUrl = NormalizeOptionalUrl(request.ThumbnailUrl, ProductThumbnailUrlMaxLength);
        var now = DateTimeOffset.UtcNow;

        product.Update(name, description, priceCents, type, accessUrl, thumbnailUrl, now);

        var newStatus = ParseProductStatus(request.Status);
        if (newStatus == ProductStatus.Active && product.Status == ProductStatus.Draft)
            product.Publish(now);
        else if (newStatus == ProductStatus.Draft && product.Status == ProductStatus.Active)
            product.Unpublish(now);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(product);
    }

    public async Task ArchiveAsync(
        string slug,
        Guid productPublicId,
        int ownerUserId,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var product = await _productRepository.GetByPublicIdAndCreatorIdForUpdateAsync(productPublicId, creatorId, ct);
        if (product is null)
            throw new NotFoundException("Product not found.");

        if (product.Status == ProductStatus.Archived)
            return;

        product.Archive(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<CreatorContext> GetCreatorContextAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");
        return context;
    }

    private static string NormalizeName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new BadRequestException("Product name is required.");
        if (name.Length > ProductNameMaxLength)
            throw new BadRequestException($"Product name cannot exceed {ProductNameMaxLength} characters.");
        return name;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        var text = value?.Trim();
        if (text is null || text.Length == 0) return null;
        if (text.Length > maxLength)
            throw new BadRequestException($"Text cannot exceed {maxLength} characters.");
        return text;
    }

    private static string? NormalizeOptionalUrl(string? value, int maxLength)
    {
        var url = value?.Trim();
        if (url is null || url.Length == 0) return null;
        if (url.Length > maxLength)
            throw new BadRequestException($"URL cannot exceed {maxLength} characters.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException("URL must be a valid http or https address.");
        return url;
    }

    private static int ValidatePriceCents(int priceCents)
    {
        if (priceCents < 0)
            throw new BadRequestException("Price cannot be negative.");
        return priceCents;
    }

    private static ProductType ParseProductType(string? value)
    {
        if (!Enum.TryParse<ProductType>(value, ignoreCase: true, out var type))
            throw new BadRequestException($"Invalid product type. Valid values: {string.Join(", ", Enum.GetNames<ProductType>())}.");
        return type;
    }

    private static ProductStatus ParseProductStatus(string? value)
    {
        if (!Enum.TryParse<ProductStatus>(value, ignoreCase: true, out var status))
            throw new BadRequestException($"Invalid product status. Valid values: {string.Join(", ", Enum.GetNames<ProductStatus>())}.");
        return status;
    }

    private static ProductResponseDto MapToDto(Product product) => new()
    {
        PublicId = product.PublicId,
        Name = product.Name,
        Description = product.Description,
        PriceCents = product.PriceCents,
        Type = product.Type.ToString(),
        Status = product.Status.ToString(),
        AccessUrl = product.AccessUrl,
        ThumbnailUrl = product.ThumbnailUrl,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt
    };
}
