using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Products.Application.Services;

public sealed class ProductService : IProductService
{
    public const string FileRequiredCode = "PRODUCT_FILE_REQUIRED";
    public const string FileReferenceInvalidCode = "FILE_REFERENCE_INVALID";
    public const string FileInUseCode = "FILE_IN_USE";
    public const string UsedByLeadPageCode = "PRODUCT_USED_BY_LEAD_PAGE";
    public const string UsedBySalesPageCode = "PRODUCT_USED_BY_SALES_PAGE";
    public const string FileDeliveredCode = "PRODUCT_FILE_DELIVERED";

    private const int ProductNameMaxLength = 100;
    private const int ProductDescriptionMaxLength = 2000;
    private const int ProductThumbnailUrlMaxLength = 2000;

    private readonly IProductRepository _productRepository;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IProductsUnitOfWork _unitOfWork;
    private readonly IOrderContextProvider _orderContextProvider;
    private readonly ILandingPageContextProvider _landingPageContextProvider;
    private readonly IProductFiles _productFiles;

    public ProductService(
        IProductRepository productRepository,
        ICreatorContextProvider creatorContextProvider,
        IProductsUnitOfWork unitOfWork,
        IOrderContextProvider orderContextProvider,
        ILandingPageContextProvider landingPageContextProvider,
        IProductFiles productFiles)
    {
        _productRepository = productRepository;
        _creatorContextProvider = creatorContextProvider;
        _unitOfWork = unitOfWork;
        _orderContextProvider = orderContextProvider;
        _landingPageContextProvider = landingPageContextProvider;
        _productFiles = productFiles;
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
        var thumbnailUrl = NormalizeOptionalUrl(request.ThumbnailUrl, ProductThumbnailUrlMaxLength);
        var requestedStatus = ParseRequestedStatus(request.Status);

        if (maxProducts >= 0 && activeProductCount >= maxProducts)
            throw new BadRequestException(
                $"Your plan allows a maximum of {maxProducts} product(s). Archive existing products or upgrade your plan.");

        var file = request.FileReference is { } reference ? await ResolveFileAsync(creatorId, reference, null, ct) : null;
        if (requestedStatus == ProductStatus.Active && file is null)
            throw new BadRequestException("Upload the file people get before making the product active.", FileRequiredCode);

        var now = DateTimeOffset.UtcNow;
        var product = Product.Create(creatorId, name, description, priceCents, thumbnailUrl, now);
        if (file is not null)
            product.AttachFile(file, now);
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
        var trendByProductId = await _orderContextProvider.GetProductSalesTrendsAsync(
            creatorId, ProductSalesTrend.FirstMonth(DateTimeOffset.UtcNow), ct);
        var signupsByProductId = products.Any(p => p.IsFree)
            ? await _landingPageContextProvider.GetProductSignupsAsync(creatorId, DateTimeOffset.UtcNow, ct)
            : [];

        return products
            .Select(p => WithSignups(
                MapToDto(p, revenueByProductId.GetValueOrDefault(p.Id), trendByProductId.GetValueOrDefault(p.Id)),
                p,
                signupsByProductId.GetValueOrDefault(p.Id)))
            .ToList();
    }

    /// <summary>A free product's sign-ups (zeros when nobody signed up yet); a paid product carries none.</summary>
    private static ProductResponseDto WithSignups(ProductResponseDto dto, Product product, ProductSignups? signups)
    {
        if (!product.IsFree)
            return dto;

        var trend = signups?.Trend ?? new int[ProductSalesTrend.Months];
        return new ProductResponseDto
        {
            PublicId = dto.PublicId,
            Name = dto.Name,
            Description = dto.Description,
            PriceCents = dto.PriceCents,
            Status = dto.Status,
            ThumbnailUrl = dto.ThumbnailUrl,
            FileName = dto.FileName,
            FileSizeBytes = dto.FileSizeBytes,
            FileContentType = dto.FileContentType,
            FileUploadedAt = dto.FileUploadedAt,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            RevenueCents = dto.RevenueCents,
            PaidOrderCount = dto.PaidOrderCount,
            OrderCount = dto.OrderCount,
            SalesTrend = dto.SalesTrend,
            SignupCount = signups?.Total ?? 0,
            SignupsThisMonth = trend[^1],
            SignupsTrend = trend,
        };
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
        var thumbnailUrl = NormalizeOptionalUrl(request.ThumbnailUrl, ProductThumbnailUrlMaxLength);
        // A missing status leaves the product as it is, so callers that only edit fields keep working.
        var newStatus = ParseRequestedStatus(request.Status) ?? product.Status;
        var now = DateTimeOffset.UtcNow;

        if (newStatus == ProductStatus.Draft && product.Status == ProductStatus.Active &&
            await _landingPageContextProvider.IsUsedByPublishedPageAsync(product.Id, ct))
            throw new ConflictException("This product is used by a published page.");

        // A lead page gives its product away and a Sales page sells it, so a product they use keeps its kind.
        if ((priceCents == 0) != product.IsFree)
        {
            var (leadPage, salesPage) = await _landingPageContextProvider.GetPageKindsUsingAsync(product.Id, ct);
            if (priceCents > 0 && leadPage)
                throw new ConflictException("A lead page gives this product away, so it must stay free.", UsedByLeadPageCode);
            if (priceCents == 0 && salesPage)
                throw new ConflictException("A Sales page sells this product, so it needs a price.", UsedBySalesPageCode);
        }

        var replacement = request.FileReference is { } reference
            ? await ResolveFileAsync(creatorId, reference, product.Id, ct)
            : null;
        var removeFile = replacement is null && request.RemoveFile && product.HasFile;
        // Buyers and sign-ups keep their links: the file can be replaced (they get the new one), not taken away.
        if (removeFile && await _productRepository.HasActiveAccessGrantsAsync(product.Id, ct))
            throw new ConflictException("People already have a download link to this file. Replace it instead of removing it.", FileDeliveredCode);
        var willHaveFile = replacement is not null || (product.HasFile && !removeFile);
        if (newStatus == ProductStatus.Active && !willHaveFile)
            throw new BadRequestException("An active product needs its file. Upload one, or save it as a draft.", FileRequiredCode);

        var previousBlobPath = product.FileBlobPath;

        product.Update(name, description, priceCents, thumbnailUrl, now);

        // Unpublish before removing the file, publish after attaching it: the domain keeps "Active ⇒ file".
        if (newStatus == ProductStatus.Draft && product.Status == ProductStatus.Active)
            product.Unpublish(now);
        if (replacement is not null)
            product.AttachFile(replacement, now);
        else if (removeFile)
            product.RemoveFile(now);
        if (newStatus == ProductStatus.Active && product.Status == ProductStatus.Draft)
            product.Publish(now);

        await _unitOfWork.SaveChangesAsync(ct);

        // The old document is no longer anyone's: delete it now that the change is committed (a failure is logged).
        if (previousBlobPath is not null && previousBlobPath != product.FileBlobPath)
            await _productFiles.DeleteAsync(previousBlobPath, CancellationToken.None);

        return MapToDto(product);
    }

    /// <summary>The creator's own confirmed upload, not already another product's document (replacing that product's
    /// file would then delete this one's).</summary>
    private async Task<ProductFile> ResolveFileAsync(int creatorId, Guid reference, int? productId, CancellationToken ct)
    {
        var file = await _productFiles.ResolveAsync(creatorId, reference, ct)
            ?? throw new BadRequestException("That file upload was not found. Upload the file again.", FileReferenceInvalidCode);

        if (await _productRepository.IsFileInUseAsync(file.BlobPath, productId, ct))
            throw new ConflictException("That file already belongs to another product. Upload it again for this one.", FileInUseCode);

        return file;
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

    private static ProductResponseDto MapToDto(Product product, ProductRevenueDto? revenue = null, int[]? salesTrend = null) => new()
    {
        PublicId = product.PublicId,
        Name = product.Name,
        Description = product.Description,
        PriceCents = product.PriceCents,
        Status = product.Status.ToString(),
        ThumbnailUrl = product.ThumbnailUrl,
        FileName = product.FileName,
        FileSizeBytes = product.FileSizeBytes,
        FileContentType = product.FileContentType,
        FileUploadedAt = product.FileUploadedAt,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt,
        RevenueCents = revenue?.RevenueCents ?? 0,
        PaidOrderCount = revenue?.PaidOrderCount ?? 0,
        OrderCount = revenue?.OrderCount ?? 0,
        SalesTrend = salesTrend ?? new int[ProductSalesTrend.Months]
    };
}
