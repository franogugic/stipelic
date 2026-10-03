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
    private readonly IOrderContextProvider _orderContextProvider;
    private readonly ILandingPageContextProvider _landingPageContextProvider;
    private readonly IAnalyticsContextProvider _analyticsContextProvider;

    // Range key → bucket unit and bucket count (the current day / month is the last bucket).
    private static readonly Dictionary<string, (string Unit, int Buckets)> AnalyticsRanges = new()
    {
        ["30d"] = ("day", 30),
        ["3m"] = ("month", 3),
        ["6m"] = ("month", 6),
        ["1y"] = ("month", 12),
    };

    public ProductService(
        IProductRepository productRepository,
        ICreatorContextProvider creatorContextProvider,
        IProductsUnitOfWork unitOfWork,
        IOrderContextProvider orderContextProvider,
        ILandingPageContextProvider landingPageContextProvider,
        IAnalyticsContextProvider analyticsContextProvider)
    {
        _productRepository = productRepository;
        _creatorContextProvider = creatorContextProvider;
        _unitOfWork = unitOfWork;
        _orderContextProvider = orderContextProvider;
        _landingPageContextProvider = landingPageContextProvider;
        _analyticsContextProvider = analyticsContextProvider;
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
        var requestedStatus = ParseRequestedStatus(request.Status);

        if (maxProducts >= 0 && activeProductCount >= maxProducts)
            throw new BadRequestException(
                $"Your plan allows a maximum of {maxProducts} product(s). Archive existing products or upgrade your plan.");

        var now = DateTimeOffset.UtcNow;
        var product = Product.Create(creatorId, name, description, priceCents, type, accessUrl, thumbnailUrl, now);
        if (requestedStatus == ProductStatus.Active)
            product.Publish(now);

        await _productRepository.AddAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(product);
    }

    public async Task<List<ProductResponseDto>> ListAsync(
        string slug,
        int ownerUserId,
        bool includeArchived,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var products = await _productRepository.ListByCreatorIdAsync(creatorId, includeArchived, ct);
        var revenueByProductId = await _orderContextProvider.GetProductRevenueByCreatorIdAsync(creatorId, ct);

        return products.Select(p => MapToDto(p, revenueByProductId.GetValueOrDefault(p.Id))).ToList();
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
        // A missing status leaves the product as it is, so callers that only edit fields keep working.
        var newStatus = ParseRequestedStatus(request.Status) ?? product.Status;
        var now = DateTimeOffset.UtcNow;

        if (newStatus == ProductStatus.Draft && product.Status == ProductStatus.Active &&
            await _landingPageContextProvider.IsUsedByPublishedPageAsync(product.Id, ct))
            throw new ConflictException("This product is used by a published page.");

        product.Update(name, description, priceCents, type, accessUrl, thumbnailUrl, now);

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

    public async Task<ProductResponseDto> RestoreAsync(
        string slug,
        Guid productPublicId,
        int ownerUserId,
        CancellationToken ct)
    {
        var (creatorId, maxProducts, activeProductCount) = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var product = await _productRepository.GetByPublicIdAndCreatorIdForUpdateAsync(productPublicId, creatorId, ct);
        if (product is null)
            throw new NotFoundException("Product not found.");

        if (product.Status != ProductStatus.Archived)
            throw new BadRequestException("Only archived products can be restored.");

        if (maxProducts >= 0 && activeProductCount >= maxProducts)
            throw new ConflictException(
                $"Your plan allows a maximum of {maxProducts} product(s). Free up {activeProductCount - maxProducts + 1} product(s) before restoring.");

        product.Restore(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(product);
    }

    public async Task<ProductAnalyticsDto> GetAnalyticsAsync(
        string slug,
        Guid productPublicId,
        int ownerUserId,
        string? range,
        CancellationToken ct)
    {
        var rangeKey = string.IsNullOrWhiteSpace(range) ? "1y" : range.Trim().ToLowerInvariant();
        if (!AnalyticsRanges.TryGetValue(rangeKey, out var analyticsRange))
            throw new BadRequestException("Invalid range. Valid values: 30d, 3m, 6m, 1y.");

        // Ownership first: the product is looked up inside the caller's own workspace, so another creator's product
        // is a plain 404. Archived products keep their history.
        var (creatorId, _, _) = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var product = await _productRepository.GetByPublicIdAndCreatorIdForUpdateAsync(productPublicId, creatorId, ct);
        if (product is null)
            throw new NotFoundException("Product not found.");

        var now = DateTimeOffset.UtcNow;
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var lastBucket = analyticsRange.Unit == "day" ? today : monthStart;
        var firstBucket = analyticsRange.Unit == "day"
            ? lastBucket.AddDays(-(analyticsRange.Buckets - 1))
            : lastBucket.AddMonths(-(analyticsRange.Buckets - 1));

        // One query per source, run one after the other: they share the request's DbContext.
        var orders = await _orderContextProvider.GetProductOrderStatsAsync(
            creatorId, product.Id, analyticsRange.Unit, firstBucket, lastBucket, monthStart, ct);
        var contactCount = await _analyticsContextProvider.CountContactsAsync(product.Id, ct);
        var sellingPages = await _landingPageContextProvider.GetSellingPagesAsync(product.Id, ct);

        return new ProductAnalyticsDto(
            orders.RevenueCents,
            orders.SalesCount,
            orders.ThisMonthRevenueCents,
            contactCount,
            analyticsRange.Unit,
            orders.Points,
            sellingPages);
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

    /// <summary>Null for a missing or empty status; Active or Draft otherwise. Archived is not a writable status.</summary>
    private static ProductStatus? ParseRequestedStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Enum.TryParse<ProductStatus>(value.Trim(), ignoreCase: true, out var status) || !Enum.IsDefined(status))
            throw new BadRequestException(
                $"Invalid product status. Valid values: {nameof(ProductStatus.Active)}, {nameof(ProductStatus.Draft)}.");
        if (status == ProductStatus.Archived)
            throw new BadRequestException("Products cannot be archived by changing their status. Use the archive endpoint instead.");
        return status;
    }

    private static ProductResponseDto MapToDto(Product product, ProductRevenueDto? revenue = null) => new()
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
        UpdatedAt = product.UpdatedAt,
        RevenueCents = revenue?.RevenueCents ?? 0,
        PaidOrderCount = revenue?.PaidOrderCount ?? 0
    };
}
