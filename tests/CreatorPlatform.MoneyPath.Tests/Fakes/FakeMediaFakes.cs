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

    public Task DeleteIfExistsAsync(string blobUrl, CancellationToken ct)
    {
        DeleteCalls.Add(blobUrl);
        return Task.CompletedTask;
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

    public Task<int> CountIssuedSinceAsync(int creatorId, DateTimeOffset since, CancellationToken ct)
        => Task.FromResult(References.Count(r => r.CreatorId == creatorId && r.CreatedAt >= since));

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

/// <summary>The private product-file container in memory: blobs are (content type, bytes); deletes and signed download
/// URLs are recorded.</summary>
public sealed class FakeProductFileStorage : IProductFileStorage
{
    public Dictionary<string, (string ContentType, byte[] Bytes)> Blobs { get; } = [];
    public List<string> DeleteCalls { get; } = [];
    public List<(string BlobPath, string FileName, string ContentType, TimeSpan Expiry)> DownloadSasCalls { get; } = [];

    /// <summary>Simulates the browser's direct upload.</summary>
    public void Put(string blobPath, string contentType, byte[] bytes) => Blobs[blobPath] = (contentType, bytes);

    public Uri GenerateUploadSasUri(string blobPath, TimeSpan expiry) =>
        new($"https://fake.blob.core.windows.net/product-files/{blobPath}?sas=write-only");

    /// <summary>A size Azure reports instead of the stored bytes' length (an oversize file without allocating it).</summary>
    public Dictionary<string, long> ReportedLengths { get; } = [];

    public Task<BlobProperties?> GetPropertiesAsync(string blobPath, CancellationToken ct) =>
        Task.FromResult(Blobs.TryGetValue(blobPath, out var blob)
            ? new BlobProperties(ReportedLengths.GetValueOrDefault(blobPath, blob.Bytes.LongLength), blob.ContentType)
            : null);

    public Task<byte[]> ReadHeadAsync(string blobPath, int byteCount, CancellationToken ct) =>
        Task.FromResult(Blobs.TryGetValue(blobPath, out var blob) ? blob.Bytes.Take(byteCount).ToArray() : []);

    public Task DeleteIfExistsAsync(string blobPath, CancellationToken ct)
    {
        DeleteCalls.Add(blobPath);
        Blobs.Remove(blobPath);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        DeleteCalls.Add(blobPath);
        Blobs.Remove(blobPath);
        return Task.CompletedTask;
    }

    public Uri GenerateDownloadSasUri(string blobPath, string downloadFileName, string contentType, TimeSpan expiry)
    {
        DownloadSasCalls.Add((blobPath, downloadFileName, contentType, expiry));
        return new Uri($"https://fake.blob.core.windows.net/product-files/{blobPath}?sas=read&rscd=attachment");
    }
}
