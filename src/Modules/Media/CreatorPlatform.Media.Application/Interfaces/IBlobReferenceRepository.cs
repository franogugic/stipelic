using CreatorPlatform.Media.Domain.Media;

namespace CreatorPlatform.Media.Application.Interfaces;

public interface IBlobReferenceRepository
{
    Task AddAsync(BlobReference reference, CancellationToken ct);

    /// <summary>Upload links issued to <paramref name="creatorId"/> since <paramref name="since"/>, any purpose or status
    /// (the (CreatorId, CreatedAt) index).</summary>
    Task<int> CountIssuedSinceAsync(int creatorId, DateTimeOffset since, CancellationToken ct);

    /// <summary>Ownership-scoped lookup for confirm — only ever matches a row that was actually issued
    /// to <paramref name="creatorId"/> and is still <see cref="BlobReferenceStatus.Pending"/> (a second
    /// confirm attempt on an already-Confirmed row deliberately misses here, not just at the entity-guard
    /// level).</summary>
    Task<BlobReference?> GetPendingByCreatorAndUrlAsync(int creatorId, string blobUrl, CancellationToken ct);
}
