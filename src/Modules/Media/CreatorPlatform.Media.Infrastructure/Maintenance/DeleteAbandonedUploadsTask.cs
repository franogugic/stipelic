using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Application.Services;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Infrastructure.Maintenance;

/// <summary>
/// Cleans up uploads that were never confirmed, in two steps:
/// <list type="number">
/// <item>A <see cref="BlobReferenceStatus.Pending"/> reference older than
/// <see cref="MaintenanceOptions.AbandonedUploadMinutes"/> loses its blob (images in the public container, product
/// documents in the private one) and becomes <see cref="BlobReferenceStatus.Abandoned"/>. Nothing points at these blobs:
/// pages, products and logos only take Confirmed uploads (PR 1). The status change (only while still Pending) and the
/// blob delete share one transaction: a blob that cannot be deleted rolls the row back, so it is retried on the next run
/// instead of being forgotten in storage, and a reference confirmed in the meantime is never touched.</item>
/// <item>An Abandoned row is deleted once it is older than the daily upload budget's window
/// (<see cref="MediaUploadService.UploadBudgetWindow"/>) plus an hour. Until then it still counts against that budget, so
/// abandoning uploads cannot be used to issue more than the daily limit.</item>
/// </list>
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

    /// <summary>How long an Abandoned row is kept: past the upload budget's window, with an hour of margin.</summary>
    public static readonly TimeSpan AbandonedRowRetention = MediaUploadService.UploadBudgetWindow + TimeSpan.FromHours(1);

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
        var abandoned = 0;

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
                    abandoned += await AbandonAsync(upload, images, documents, ct);
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

        var removed = await DeleteExpiredAbandonedRowsAsync(now, ct);

        if (failed.Count > 0)
            throw new InvalidOperationException(
                $"{failed.Count} abandoned upload(s) could not be deleted and stay for the next run; {abandoned} were deleted.");

        return abandoned + removed;
    }

    private async Task<int> DeleteExpiredAbandonedRowsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var issuedBefore = now - AbandonedRowRetention;
        var batchSize = maintenanceOptions.Value.BatchSize;
        var total = 0;

        while (true)
        {
            var deleted = await db.Set<BlobReference>()
                .Where(reference => reference.Status == BlobReferenceStatus.Abandoned && reference.CreatedAt < issuedBefore)
                .OrderBy(reference => reference.Id)
                .Take(batchSize)
                .ExecuteDeleteAsync(ct);

            total += deleted;
            if (deleted < batchSize)
                return total;
        }
    }

    private async Task<int> AbandonAsync(
        AbandonedUpload upload, IBlobStorageClient images, IProductFileStorage documents, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var marked = await db.Set<BlobReference>()
            .Where(reference => reference.Id == upload.Id && reference.Status == BlobReferenceStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(reference => reference.Status, BlobReferenceStatus.Abandoned), ct);
        if (marked == 0)
            return 0;   // confirmed (or cleaned up) since it was listed: leave the blob alone

        if (upload.Purpose == MediaUploadPurpose.ProductFile)
            await documents.DeleteIfExistsAsync(
                upload.BlobPath ?? throw new InvalidOperationException($"Product document upload {upload.Id} has no blob path."), ct);
        else
            await images.DeleteIfExistsAsync(upload.BlobUrl, ct);

        await transaction.CommitAsync(ct);
        return marked;
    }

    private sealed record AbandonedUpload(int Id, MediaUploadPurpose Purpose, string BlobUrl, string? BlobPath);
}
