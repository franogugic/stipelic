using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Infrastructure.Maintenance;

/// <summary>
/// Deletes uploads that were never confirmed: a <see cref="BlobReferenceStatus.Pending"/> reference older than
/// <see cref="MaintenanceOptions.AbandonedUploadMinutes"/>, together with whatever the client stored under it (images in
/// the public container, product documents in the private one). The SPA only uses an upload after confirming it, and a
/// product only takes a confirmed document, so nothing points at these blobs.
///
/// Per reference, the row is deleted (only while still Pending) and the blob deleted inside one transaction: a blob that
/// cannot be deleted rolls the row back, so it is retried on the next run instead of being forgotten in storage, and a
/// reference confirmed in the meantime is never touched.
/// </summary>
public sealed class DeleteAbandonedUploadsTask(
    CreatorPlatformDbContext db,
    IOptions<MaintenanceOptions> maintenanceOptions,
    IOptions<MediaOptions> mediaOptions,
    IServiceProvider services,
    ILogger<DeleteAbandonedUploadsTask> logger) : IMaintenanceTask
{
    /// <summary>Blob deletes are network calls: a smaller batch than the row-only tasks.</summary>
    public const int MaxBatchSize = 100;

    private static int _notConfiguredLogged;

    public string Name => "abandoned-uploads";

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mediaOptions.Value.ConnectionString))
        {
            if (Interlocked.Exchange(ref _notConfiguredLogged, 1) == 0)
                logger.LogWarning("Abandoned uploads are not cleaned up: this Worker has no blob storage (Media:ConnectionString).");
            return 0;
        }

        // Resolved only now: the Azure clients cannot be created without a connection string.
        var images = services.GetRequiredService<IBlobStorageClient>();
        var documents = services.GetRequiredService<IProductFileStorage>();

        var issuedBefore = now.AddMinutes(-maintenanceOptions.Value.AbandonedUploadMinutes);
        var batchSize = Math.Min(maintenanceOptions.Value.BatchSize, MaxBatchSize);
        var failed = new HashSet<int>();
        var deleted = 0;

        while (true)
        {
            var batch = await db.Set<BlobReference>()
                .AsNoTracking()
                .Where(reference => reference.Status == BlobReferenceStatus.Pending
                                    && reference.CreatedAt < issuedBefore
                                    && !failed.Contains(reference.Id))
                .OrderBy(reference => reference.CreatedAt)
                .Take(batchSize)
                .Select(reference => new AbandonedUpload(reference.Id, reference.Purpose, reference.BlobUrl, reference.BlobPath))
                .ToListAsync(ct);

            foreach (var upload in batch)
            {
                try
                {
                    deleted += await DeleteAsync(upload, images, documents, ct);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    failed.Add(upload.Id);
                    logger.LogWarning(exception,
                        "Abandoned upload {BlobReferenceId} ({Purpose}) could not be deleted; it is retried on the next run.",
                        upload.Id,
                        upload.Purpose);
                }
            }

            // A batch where nothing could be deleted: storage is most likely unreachable, stop for this run.
            if (batch.Count < batchSize || batch.All(upload => failed.Contains(upload.Id)))
                break;
        }

        if (failed.Count > 0)
            throw new InvalidOperationException(
                $"{failed.Count} abandoned upload(s) could not be deleted and stay for the next run; {deleted} were deleted.");

        return deleted;
    }

    private async Task<int> DeleteAsync(
        AbandonedUpload upload, IBlobStorageClient images, IProductFileStorage documents, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var removed = await db.Set<BlobReference>()
            .Where(reference => reference.Id == upload.Id && reference.Status == BlobReferenceStatus.Pending)
            .ExecuteDeleteAsync(ct);
        if (removed == 0)
            return 0;   // confirmed (or deleted) since it was listed: leave the blob alone

        if (upload.Purpose == MediaUploadPurpose.ProductFile)
            await documents.DeleteIfExistsAsync(
                upload.BlobPath ?? throw new InvalidOperationException($"Product document upload {upload.Id} has no blob path."), ct);
        else
            await images.DeleteIfExistsAsync(upload.BlobUrl, ct);

        await transaction.CommitAsync(ct);
        return removed;
    }

    private sealed record AbandonedUpload(int Id, MediaUploadPurpose Purpose, string BlobUrl, string? BlobPath);
}
