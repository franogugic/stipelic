using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApplicationBlobProperties = CreatorPlatform.Media.Application.Interfaces.BlobProperties;

namespace CreatorPlatform.Media.Infrastructure.Services;

/// <summary>Product documents in the private container (<see cref="MediaOptions.PrivateContainerName"/>). SAS tokens are
/// signed with the account key from the connection string, offline.</summary>
public sealed class AzureProductFileStorage : IProductFileStorage
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureProductFileStorage> _logger;

    public AzureProductFileStorage(IOptions<MediaOptions> options, ILogger<AzureProductFileStorage> logger)
    {
        _logger = logger;
        var serviceClient = new BlobServiceClient(options.Value.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(options.Value.PrivateContainerName);
    }

    public Uri GenerateUploadSasUri(string blobPath, TimeSpan expiry)
    {
        var sas = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sas.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        return _containerClient.GetBlobClient(blobPath).GenerateSasUri(sas);
    }

    public async Task<ApplicationBlobProperties?> GetPropertiesAsync(string blobPath, CancellationToken ct)
    {
        try
        {
            var response = await _containerClient.GetBlobClient(blobPath).GetPropertiesAsync(cancellationToken: ct);
            return new ApplicationBlobProperties(response.Value.ContentLength, response.Value.ContentType);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    public async Task<byte[]> ReadHeadAsync(string blobPath, int byteCount, CancellationToken ct)
    {
        var response = await _containerClient.GetBlobClient(blobPath).DownloadContentAsync(
            new BlobDownloadOptions { Range = new HttpRange(0, byteCount) }, ct);
        return response.Value.Content.ToArray();
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        try
        {
            await _containerClient.GetBlobClient(blobPath).DeleteIfExistsAsync(cancellationToken: ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to delete product file {BlobPath}; leaving it in place.", blobPath);
        }
    }

    public Uri GenerateDownloadSasUri(string blobPath, string downloadFileName, string contentType, TimeSpan expiry)
    {
        var sas = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry),
            ContentDisposition = ContentDispositions.Attachment(downloadFileName),
            ContentType = contentType
        };
        sas.SetPermissions(BlobSasPermissions.Read);

        return _containerClient.GetBlobClient(blobPath).GenerateSasUri(sas);
    }
}

/// <summary><c>attachment; filename="…"; filename*=UTF-8''…</c> (RFC 6266): an ASCII fallback with quotes and control
/// characters removed, and the exact name percent-encoded for every browser that reads <c>filename*</c>.</summary>
public static class ContentDispositions
{
    public static string Attachment(string fileName)
    {
        var ascii = new string(fileName
            .Select(c => c is < ' ' or > '~' or '"' or '\\' ? '_' : c)
            .ToArray());
        if (string.IsNullOrWhiteSpace(ascii))
            ascii = "download";

        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}
