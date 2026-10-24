using Azure.Storage.Blobs;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Application.Media;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Infrastructure.Services;

public sealed class OwnedImageCheck : IOwnedImageCheck
{
    private readonly CreatorPlatformDbContext _context;
    private readonly Uri? _container;

    public OwnedImageCheck(CreatorPlatformDbContext context, IOptions<MediaOptions> options)
    {
        _context = context;
        // The public container's address, as upload links are made from it; none when storage is not configured.
        _container = string.IsNullOrWhiteSpace(options.Value.ConnectionString)
            ? null
            : new BlobServiceClient(options.Value.ConnectionString).GetBlobContainerClient(options.Value.ContainerName).Uri;
    }

    public async Task EnsureOwnedAsync(int? creatorId, IEnumerable<string?> urls, CancellationToken ct)
    {
        var ours = urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!.Trim())
            .Where(PointsAtOurContainer)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ours.Count == 0)
            return;

        // As stored at upload: the blob's URL without a query. One with a query or fragment is never an upload's URL.
        var confirmed = creatorId is int id
            ? await _context.Set<BlobReference>()
                .AsNoTracking()
                .Where(b => b.CreatorId == id && b.Status == BlobReferenceStatus.Confirmed
                    && b.Purpose != MediaUploadPurpose.ProductFile && ours.Contains(b.BlobUrl))
                .Select(b => b.BlobUrl)
                .ToListAsync(ct)
            : [];

        if (ours.Any(url => !confirmed.Contains(url, StringComparer.Ordinal)))
        {
            throw new BadRequestException(
                "An image must be one of your own finished uploads. Upload it again.", IOwnedImageCheck.ImageNotUploadedCode);
        }
    }

    private bool PointsAtOurContainer(string url)
    {
        if (_container is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.GetLeftPart(UriPartial.Authority), _container.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith(_container.AbsolutePath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    }
}
