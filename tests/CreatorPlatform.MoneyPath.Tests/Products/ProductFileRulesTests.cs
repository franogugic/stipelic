using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Products;

/// <summary>F8.1: every product is a file. An Active product needs one; a file comes only from the creator's own confirmed
/// upload; a replaced file's blob is deleted after the save; people holding links keep a file; a product used by a lead
/// page stays free and one used by a Sales page stays paid.</summary>
public class ProductFileRulesTests
{
    private const int CreatorId = 1;
    private const int OtherCreatorId = 2;
    private const int OwnerUserId = 7;
    private const string CreatorSlug = "acme";

    private readonly FakeProductRepository _products = new();
    private readonly FakeProductFiles _files = new();
    private readonly FakeLandingPageContextProvider _pages = new();
    private readonly FakeProductsUnitOfWork _unitOfWork = new();
    private readonly ProductService _service;

    public ProductFileRulesTests()
    {
        var creators = new FakeProductsCreatorContextProvider { Context = new CreatorContext(CreatorId, MaxProducts: 10, ActiveProductCount: 0) };
        _service = new ProductService(_products, creators, _unitOfWork, new FakeOrderContextProvider(), _pages, _files);
    }

    private Task<ProductResponseDto> CreateAsync(Guid? file, string? status = "Draft", int priceCents = 1900) =>
        _service.CreateAsync(CreatorSlug, OwnerUserId,
            new CreateProductRequestDto { Name = "Guide", PriceCents = priceCents, FileReference = file, Status = status }, CancellationToken.None);

    private Task<ProductResponseDto> UpdateAsync(Guid productId, Guid? file = null, bool removeFile = false, string? status = null, int priceCents = 1900) =>
        _service.UpdateAsync(CreatorSlug, productId, OwnerUserId,
            new UpdateProductRequestDto { Name = "Guide", PriceCents = priceCents, FileReference = file, RemoveFile = removeFile, Status = status },
            CancellationToken.None);

    private Product Stored(Guid publicId) => _products.Products.Single(p => p.PublicId == publicId);

    [Fact]
    public async Task CreatingAnActiveProductWithoutAFile_IsRefused_AndADraftIsFine()
    {
        var error = await Assert.ThrowsAsync<BadRequestException>(() => CreateAsync(file: null, status: "Active"));
        Assert.Equal(ProductService.FileRequiredCode, error.Code);
        Assert.Empty(_products.Products);

        var draft = await CreateAsync(file: null, status: "Draft");
        Assert.Equal("Draft", draft.Status);
        Assert.Null(draft.File);
    }

    [Fact]
    public async Task PublishingADraftWithoutAFile_IsRefused()
    {
        var draft = await CreateAsync(file: null);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => UpdateAsync(draft.PublicId, status: "Active"));

        Assert.Equal(ProductService.FileRequiredCode, error.Code);
        Assert.Equal(ProductStatus.Draft, Stored(draft.PublicId).Status);
    }

    [Fact]
    public async Task AnActiveProduct_GetsItsFileFromTheConfirmedUpload()
    {
        var reference = _files.Upload(CreatorId, "Color Grading Guide.pdf", 8_600_000);

        var product = await CreateAsync(reference, status: "Active");

        Assert.Equal("Active", product.Status);
        Assert.False(product.IsFree);
        Assert.Equal("Color Grading Guide.pdf", product.File!.FileName);
        Assert.Equal(8_600_000, product.File.SizeBytes);
        Assert.Equal("application/pdf", product.File.ContentType);
        Assert.Equal(_files.Uploads[(CreatorId, reference)].BlobPath, Stored(product.PublicId).FileBlobPath);
    }

    [Fact]
    public async Task AnotherCreatorsFileReference_IsRefused()
    {
        var theirs = _files.Upload(OtherCreatorId);

        var onCreate = await Assert.ThrowsAsync<BadRequestException>(() => CreateAsync(theirs, status: "Draft"));
        var draft = await CreateAsync(file: null);
        var onUpdate = await Assert.ThrowsAsync<BadRequestException>(() => UpdateAsync(draft.PublicId, file: theirs));

        Assert.Equal(ProductService.FileReferenceInvalidCode, onCreate.Code);
        Assert.Equal(ProductService.FileReferenceInvalidCode, onUpdate.Code);
        Assert.False(Stored(draft.PublicId).HasFile);
    }

    [Fact]
    public async Task OneUpload_CannotBecomeTwoProductsFiles()
    {
        var reference = _files.Upload(CreatorId);
        await CreateAsync(reference);

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(reference));

        Assert.Equal(ProductService.FileInUseCode, error.Code);
    }

    [Fact]
    public async Task ReplacingTheFile_DeletesTheOldBlobAfterTheSave_AndKeepingItDeletesNothing()
    {
        var first = _files.Upload(CreatorId, "v1.pdf");
        var product = await CreateAsync(first, status: "Active");
        var oldPath = Stored(product.PublicId).FileBlobPath!;

        await UpdateAsync(product.PublicId);                       // no reference: the file stays
        Assert.Empty(_files.DeletedBlobPaths);

        var savesBefore = _unitOfWork.SaveChangesCallCount;
        var updated = await UpdateAsync(product.PublicId, file: _files.Upload(CreatorId, "v2.pdf"));

        Assert.Equal("v2.pdf", updated.File!.FileName);
        Assert.Equal("Active", updated.Status);
        Assert.Equal([oldPath], _files.DeletedBlobPaths);
        Assert.Equal(savesBefore + 1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AnActiveProductsFile_CannotBeRemoved_ButADraftsCan()
    {
        var product = await CreateAsync(_files.Upload(CreatorId), status: "Active");

        var error = await Assert.ThrowsAsync<BadRequestException>(() => UpdateAsync(product.PublicId, removeFile: true));
        Assert.Equal(ProductService.FileRequiredCode, error.Code);
        Assert.Empty(_files.DeletedBlobPaths);

        var path = Stored(product.PublicId).FileBlobPath!;
        var draft = await UpdateAsync(product.PublicId, removeFile: true, status: "Draft");
        Assert.Null(draft.File);
        Assert.Equal([path], _files.DeletedBlobPaths);
    }

    [Fact]
    public async Task AFileSomeoneHasALinkTo_CannotBeRemoved_OnlyReplaced()
    {
        var product = await CreateAsync(_files.Upload(CreatorId));
        _products.ProductsWithActiveGrants.Add(Stored(product.PublicId).Id);

        var error = await Assert.ThrowsAsync<ConflictException>(() => UpdateAsync(product.PublicId, removeFile: true));
        Assert.Equal(ProductService.FileDeliveredCode, error.Code);
        Assert.True(Stored(product.PublicId).HasFile);

        await UpdateAsync(product.PublicId, file: _files.Upload(CreatorId, "v2.pdf"));
        Assert.Equal("v2.pdf", Stored(product.PublicId).FileName);
    }

    [Fact]
    public async Task AProductALeadPageGivesAway_StaysFree_AndOneASalesPageSells_StaysPaid()
    {
        var free = await CreateAsync(_files.Upload(CreatorId), status: "Active", priceCents: 0);
        Assert.True(free.IsFree);
        _pages.UsedByLeadPage = true;
        var leadError = await Assert.ThrowsAsync<ConflictException>(() => UpdateAsync(free.PublicId, priceCents: 900));
        Assert.Equal(ProductService.UsedByLeadPageCode, leadError.Code);
        Assert.Equal(0, Stored(free.PublicId).PriceCents);

        _pages.UsedByLeadPage = false;
        _pages.UsedBySalesPage = true;
        var paid = await CreateAsync(_files.Upload(CreatorId), status: "Active", priceCents: 2900);
        var salesError = await Assert.ThrowsAsync<ConflictException>(() => UpdateAsync(paid.PublicId, priceCents: 0));
        Assert.Equal(ProductService.UsedBySalesPageCode, salesError.Code);

        // A new price of the same kind is fine.
        Assert.Equal(3900, (await UpdateAsync(paid.PublicId, priceCents: 3900)).PriceCents);
    }
}
