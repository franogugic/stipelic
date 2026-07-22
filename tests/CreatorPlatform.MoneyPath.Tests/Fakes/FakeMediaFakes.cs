using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Domain.Media;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

/// <summary>Never calls real Azure — returns a deterministic fake SAS URI and whatever properties/behavior
/// the test configures.</summary>
public sealed class FakeBlobStorageClient : IBlobStorageClient
{
    public BlobProperties? PropertiesToReturn { get; set; }
    public List<string> DeleteCalls { get; } = [];
    public List<(string BlobPath, string ContentType, TimeSpan Expiry)> GenerateSasCalls { get; } = [];
    public List<string> GetPropertiesCalls { get; } = [];

    public Uri GenerateUploadSasUri(string blobPath, string contentType, TimeSpan expiry)
    {
        GenerateSasCalls.Add((blobPath, contentType, expiry));
        return new Uri($"https://fake.blob.core.windows.net/creator-media/{blobPath}?sas=fake-token");
    }

    public Task<BlobProperties?> GetPropertiesAsync(string blobUrl, CancellationToken ct)
    {
        GetPropertiesCalls.Add(blobUrl);
        return Task.FromResult(PropertiesToReturn);
    }

    public Task DeleteAsync(string blobUrl, CancellationToken ct)
    {
        DeleteCalls.Add(blobUrl);
        return Task.CompletedTask;
    }
}

public sealed class FakeBlobReferenceRepository : IBlobReferenceRepository
{
    public List<BlobReference> References { get; } = [];

    public Task AddAsync(BlobReference reference, CancellationToken ct)
    {
        References.Add(reference);
        return Task.CompletedTask;
    }

    public Task<BlobReference?> GetPendingByCreatorAndUrlAsync(int creatorId, string blobUrl, CancellationToken ct)
        => Task.FromResult(References.FirstOrDefault(
            r => r.CreatorId == creatorId && r.BlobUrl == blobUrl && r.Status == BlobReferenceStatus.Pending));
}

public sealed class FakeMediaUnitOfWork : IMediaUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
