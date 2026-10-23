using CreatorPlatform.Products.Domain.Products;

namespace CreatorPlatform.Products.Application.Interfaces;

/// <summary>Products' side of the uploaded documents: resolving the opaque reference a confirmed upload returned, and
/// deleting a document that no product uses any more.</summary>
public interface IProductFiles
{
    /// <summary>The document behind <paramref name="fileReference"/> if it is a confirmed <c>ProductFile</c> upload of this
    /// creator; null otherwise (another creator's reference resolves to nothing).</summary>
    Task<ProductFile?> ResolveAsync(int creatorId, Guid fileReference, CancellationToken ct);

    /// <summary>Best effort, after the change is committed: a failure is logged and the blob stays.</summary>
    Task DeleteAsync(string blobPath, CancellationToken ct);
}
