using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Media.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Maintenance;

/// <summary>S3 (job part): uploads never confirmed within the hour are deleted with their blobs, in the right container;
/// a blob that cannot be deleted keeps its row for the next run; confirmed and recent uploads are never touched.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DeleteAbandonedUploadsTaskTests
{
    private readonly PostgresFixture _postgres;
    private readonly TestData _data;

    public DeleteAbandonedUploadsTaskTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _data = new TestData(postgres);
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed class RecordingImageStorage : IBlobStorageClient
    {
        public List<string> StrictDeletes { get; } = [];

        public Uri GenerateUploadSasUri(string blobPath, string contentType, TimeSpan expiry) => throw new NotSupportedException();

        public Task<BlobProperties?> GetPropertiesAsync(string blobUrl, CancellationToken ct) => throw new NotSupportedException();

        public Task DeleteAsync(string blobUrl, CancellationToken ct) => throw new NotSupportedException("the job must use the strict delete");

        public Task DeleteIfExistsAsync(string blobUrl, CancellationToken ct)
        {
            StrictDeletes.Add(blobUrl);
            return Task.CompletedTask;
        }
    }

    private async Task<BlobReference> AddAsync(int creatorId, MediaUploadPurpose purpose, DateTimeOffset createdAt, bool confirmed = false)
    {
        var path = $"{purpose}/{creatorId}/{Guid.NewGuid()}.bin";
        var reference = BlobReference.CreatePending(
            creatorId, purpose, $"https://storage.test/media/{path}", path, "application/pdf", createdAt);
        if (confirmed)
            reference.Confirm(100, createdAt.AddMinutes(1));

        await using var db = _postgres.CreateDbContext();
        db.Add(reference);
        await db.SaveChangesAsync();
        return reference;
    }

    private async Task<bool> ExistsAsync(BlobReference reference)
    {
        await using var db = _postgres.CreateDbContext();
        return await db.Set<BlobReference>().AnyAsync(row => row.Id == reference.Id);
    }

    private DeleteAbandonedUploadsTask CreateTask(
        RecordingImageStorage images, InMemoryProductFileStorage documents, string connectionString = "configured")
    {
        var services = new ServiceCollection()
            .AddSingleton<IBlobStorageClient>(images)
            .AddSingleton<IProductFileStorage>(documents)
            .BuildServiceProvider();

        return new DeleteAbandonedUploadsTask(
            _postgres.CreateDbContext(),
            Options.Create(new MaintenanceOptions()),
            Options.Create(new MediaOptions { ConnectionString = connectionString }),
            services,
            NullLogger<DeleteAbandonedUploadsTask>.Instance);
    }

    [Fact]
    public async Task UploadsNeverConfirmedWithinTheHour_GoWithTheirBlobs_FromTheRightContainer()
    {
        var creator = await _data.CreateCreatorAsync();
        var image = await AddAsync(creator.CreatorId, MediaUploadPurpose.ProductThumbnail, Now.AddMinutes(-61));
        var document = await AddAsync(creator.CreatorId, MediaUploadPurpose.ProductFile, Now.AddHours(-5));
        var images = new RecordingImageStorage();
        var documents = new InMemoryProductFileStorage();

        var deleted = await CreateTask(images, documents).RunAsync(Now, CancellationToken.None);

        Assert.True(deleted >= 2);
        Assert.False(await ExistsAsync(image));
        Assert.False(await ExistsAsync(document));
        Assert.Contains(image.BlobUrl, images.StrictDeletes);            // public container: by URL
        Assert.Contains(document.BlobPath!, documents.DeleteCalls);      // private container: by path
        Assert.DoesNotContain(document.BlobUrl, images.StrictDeletes);
    }

    [Fact]
    public async Task ConfirmedUploads_AndUploadsStillWithinTheHour_AreNeverTouched()
    {
        var creator = await _data.CreateCreatorAsync();
        var recent = await AddAsync(creator.CreatorId, MediaUploadPurpose.CreatorLogo, Now.AddMinutes(-59));
        var confirmed = await AddAsync(creator.CreatorId, MediaUploadPurpose.ProductFile, Now.AddDays(-30), confirmed: true);
        var images = new RecordingImageStorage();
        var documents = new InMemoryProductFileStorage();

        await CreateTask(images, documents).RunAsync(Now, CancellationToken.None);

        Assert.True(await ExistsAsync(recent));
        Assert.True(await ExistsAsync(confirmed));
        Assert.DoesNotContain(recent.BlobUrl, images.StrictDeletes);
        Assert.DoesNotContain(confirmed.BlobPath!, documents.DeleteCalls);
    }

    [Fact]
    public async Task ABlobThatCannotBeDeleted_KeepsItsRow_TheRunReportsIt_AndTheNextRunDeletesIt()
    {
        var creator = await _data.CreateCreatorAsync();
        var image = await AddAsync(creator.CreatorId, MediaUploadPurpose.LandingPageHero, Now.AddHours(-2));
        var document = await AddAsync(creator.CreatorId, MediaUploadPurpose.ProductFile, Now.AddHours(-2));
        var images = new RecordingImageStorage();
        var documents = new InMemoryProductFileStorage { FailStrictDeletes = true };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTask(images, documents).RunAsync(Now, CancellationToken.None));

        Assert.Contains("could not be deleted", error.Message);
        Assert.True(await ExistsAsync(document));    // rolled back: not forgotten in storage
        Assert.False(await ExistsAsync(image));      // the others still went

        documents.FailStrictDeletes = false;
        await CreateTask(images, documents).RunAsync(Now, CancellationToken.None);

        Assert.False(await ExistsAsync(document));
        Assert.Contains(document.BlobPath!, documents.DeleteCalls);
    }

    [Fact]
    public async Task WithoutBlobStorage_NothingIsDeleted()
    {
        var creator = await _data.CreateCreatorAsync();
        var old = await AddAsync(creator.CreatorId, MediaUploadPurpose.ProductThumbnail, Now.AddDays(-1));

        var deleted = await CreateTask(new RecordingImageStorage(), new InMemoryProductFileStorage(), connectionString: "")
            .RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.True(await ExistsAsync(old));
    }

    [Theory]
    [InlineData(15, 30, true)]
    [InlineData(15, 29, false)]
    [InlineData(60, 60, false)]   // the upload URL would still work when the cleanup runs
    [InlineData(60, 75, true)]
    public void TheCleanupAge_MustOutlastTheUploadUrl(int sasExpiryMinutes, int abandonedUploadMinutes, bool valid)
    {
        var validator = new AbandonedUploadsOptionsValidator(Options.Create(new MediaOptions { SasExpiryMinutes = sasExpiryMinutes }));

        var result = validator.Validate(null, new MaintenanceOptions { AbandonedUploadMinutes = abandonedUploadMinutes });

        Assert.Equal(valid, result.Succeeded);
    }
}
