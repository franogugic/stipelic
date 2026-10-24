using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApplicationBlobProperties = CreatorPlatform.Media.Application.Interfaces.BlobProperties;

namespace CreatorPlatform.Media.Infrastructure.Services;

public sealed class AzureBlobStorageClient : IBlobStorageClient
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobStorageClient> _logger;

    public AzureBlobStorageClient(IOptions<MediaOptions> options, ILogger<AzureBlobStorageClient> logger)
    {
        _logger = logger;
        var serviceClient = new BlobServiceClient(options.Value.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(options.Value.ContainerName);
    }

    public Uri GenerateUploadSasUri(string blobPath, string contentType, TimeSpan expiry)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry),
        };
        // Create only, no Write: the link can make this one new blob but never overwrite it — so an image cannot be
        // swapped for other bytes after confirm checked it. (A SAS cannot pin the type or size of an upload: confirm
        // checks them, and unconfirmed blobs are deleted by the maintenance job.)
        sasBuilder.SetPermissions(BlobSasPermissions.Create);

        return blobClient.GenerateSasUri(sasBuilder);
    }

    public async Task<ApplicationBlobProperties?> GetPropertiesAsync(string blobUrl, CancellationToken ct)
    {
        var blobClient = GetBlobClientFromUrl(blobUrl);

        try
        {
            var response = await blobClient.GetPropertiesAsync(cancellationToken: ct);
            return new ApplicationBlobProperties(response.Value.ContentLength, response.Value.ContentType);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string blobUrl, CancellationToken ct)
    {
        try
        {
            var blobClient = GetBlobClientFromUrl(blobUrl);
            await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete blob {BlobUrl}; leaving it in place.", blobUrl);
        }
    }

    public async Task DeleteIfExistsAsync(string blobUrl, CancellationToken ct) =>
        await GetBlobClientFromUrl(blobUrl).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);

    private BlobClient GetBlobClientFromUrl(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var blobName = uri.AbsolutePath[(uri.AbsolutePath.IndexOf(_containerClient.Name, StringComparison.Ordinal) + _containerClient.Name.Length + 1)..];
        return _containerClient.GetBlobClient(blobName);
    }
}
