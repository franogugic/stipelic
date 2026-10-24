using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Application.Workspaces;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Infrastructure.Workspaces;

/// <summary>
/// Deletes everything under the creator's upload prefixes (<c>{purpose}/{creatorId}/</c>, see MediaUploadService):
/// images in the public container, product documents in the private one. Listing the prefix also finds files that never
/// got a reference row (an upload that was issued but never confirmed).
/// </summary>
public sealed class AzureWorkspaceFiles(IOptions<MediaOptions> options) : IWorkspaceFiles
{
    public async Task DeleteAllAsync(int creatorId, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            throw new InvalidOperationException("Blob storage is not configured (Media:ConnectionString).");

        var service = new BlobServiceClient(settings.ConnectionString);
        foreach (var purpose in Enum.GetValues<MediaUploadPurpose>())
        {
            var container = service.GetBlobContainerClient(
                purpose == MediaUploadPurpose.ProductFile ? settings.PrivateContainerName : settings.ContainerName);
            await DeletePrefixAsync(container, $"{purpose}/{creatorId}/", ct);
        }
    }

    private static async Task DeletePrefixAsync(BlobContainerClient container, string prefix, CancellationToken ct)
    {
        await foreach (var blob in container.GetBlobsAsync(new GetBlobsOptions { Prefix = prefix }, ct))
            await container.GetBlobClient(blob.Name).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }
}
