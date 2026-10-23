using CreatorPlatform.Media.Application.Interfaces;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>The private product-file container in memory (tests never call Azure): blobs by path; deletes and signed
/// download URLs are recorded (downloads may run in parallel).</summary>
public sealed class InMemoryProductFileStorage : IProductFileStorage
{
    public Dictionary<string, (string ContentType, byte[] Bytes)> Blobs { get; } = [];
    public List<string> DeleteCalls { get; } = [];
    public List<(string BlobPath, string FileName, string ContentType, TimeSpan Expiry)> DownloadSasCalls { get; } = [];

    public void Put(string blobPath, string contentType, byte[] bytes) => Blobs[blobPath] = (contentType, bytes);

    public Uri GenerateUploadSasUri(string blobPath, TimeSpan expiry) =>
        new($"https://test.blob.core.windows.net/product-files/{blobPath}?sas=write-only");

    public Task<BlobProperties?> GetPropertiesAsync(string blobPath, CancellationToken ct) =>
        Task.FromResult(Blobs.TryGetValue(blobPath, out var blob) ? new BlobProperties(blob.Bytes.LongLength, blob.ContentType) : null);

    public Task<byte[]> ReadHeadAsync(string blobPath, int byteCount, CancellationToken ct) =>
        Task.FromResult(Blobs.TryGetValue(blobPath, out var blob) ? blob.Bytes.Take(byteCount).ToArray() : []);

    public Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        DeleteCalls.Add(blobPath);
        Blobs.Remove(blobPath);
        return Task.CompletedTask;
    }

    public Uri GenerateDownloadSasUri(string blobPath, string downloadFileName, string contentType, TimeSpan expiry)
    {
        lock (DownloadSasCalls)
            DownloadSasCalls.Add((blobPath, downloadFileName, contentType, expiry));
        return new Uri($"https://test.blob.core.windows.net/product-files/{blobPath}?sas=read");
    }
}
