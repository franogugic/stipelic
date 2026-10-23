using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

/// <summary>Product documents are Media uploads (purpose <see cref="MediaUploadPurpose.ProductFile"/>) in the private
/// container; a product holds the confirmed upload's path, never a URL.</summary>
public sealed class ProductFiles : IProductFiles
{
    private readonly CreatorPlatformDbContext _context;
    private readonly IProductFileStorage _storage;

    public ProductFiles(CreatorPlatformDbContext context, IProductFileStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<ProductFile?> ResolveAsync(int creatorId, Guid fileReference, CancellationToken ct)
    {
        var upload = await _context.Set<BlobReference>()
            .AsNoTracking()
            .Where(b => b.PublicId == fileReference
                        && b.CreatorId == creatorId
                        && b.Purpose == MediaUploadPurpose.ProductFile
                        && b.Status == BlobReferenceStatus.Confirmed)
            .Select(b => new { b.BlobPath, b.FileName, b.SizeBytes, b.ContentType, b.ConfirmedAt })
            .FirstOrDefaultAsync(ct);

        if (upload is null || upload.BlobPath is null || upload.FileName is null || upload.SizeBytes is null || upload.ConfirmedAt is null)
            return null;

        // A confirmed upload whose blob was since deleted (it was another save's replaced file) is no file at all.
        if (await _storage.GetPropertiesAsync(upload.BlobPath, ct) is null)
            return null;

        return ProductFile.Create(upload.BlobPath, upload.FileName, upload.SizeBytes.Value, upload.ContentType, upload.ConfirmedAt.Value);
    }

    public Task DeleteAsync(string blobPath, CancellationToken ct) => _storage.DeleteAsync(blobPath, ct);
}
