using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Application.Services;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Media;

public class MediaUploadServiceTests
{
    private const int CreatorId = 1;
    private const int OtherCreatorId = 2;

    private sealed record Harness(
        MediaUploadService Service,
        FakeBlobReferenceRepository Repository,
        FakeBlobStorageClient BlobStorageClient,
        FakeMediaUnitOfWork UnitOfWork,
        MediaOptions Options);

    private static Harness BuildHarness(int sasExpiryMinutes = 15)
    {
        var options = new MediaOptions
        {
            ConnectionString = "unused-in-tests",
            ContainerName = "creator-media",
            MaxFileSizeBytes = 5_242_880,
            SasExpiryMinutes = sasExpiryMinutes,
            AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"],
        };

        var repository = new FakeBlobReferenceRepository();
        var blobStorageClient = new FakeBlobStorageClient();
        var unitOfWork = new FakeMediaUnitOfWork();

        var service = new MediaUploadService(repository, blobStorageClient, unitOfWork, Options.Create(options));

        return new Harness(service, repository, blobStorageClient, unitOfWork, options);
    }

    [Fact]
    public async Task RequestUploadUrlAsync_UnsupportedContentType_ThrowsBadRequestAndWritesNothing()
    {
        var h = BuildHarness();

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductThumbnail, "image/gif", CancellationToken.None));

        Assert.Empty(h.Repository.References);
        Assert.Equal(0, h.UnitOfWork.SaveChangesCallCount);
        Assert.Empty(h.BlobStorageClient.GenerateSasCalls);
    }

    [Fact]
    public async Task RequestUploadUrlAsync_SupportedContentType_CreatesPendingReferenceWithCorrectCreatorAndPurpose()
    {
        var h = BuildHarness();

        var result = await h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.CreatorLogo, "image/png", CancellationToken.None);

        var reference = Assert.Single(h.Repository.References);
        Assert.Equal(CreatorId, reference.CreatorId);
        Assert.Equal(MediaUploadPurpose.CreatorLogo, reference.Purpose);
        Assert.Equal(BlobReferenceStatus.Pending, reference.Status);
        Assert.Equal(1, h.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(reference.BlobUrl, result.BlobUrl);
        Assert.Contains("CreatorLogo", result.BlobUrl);
        Assert.Contains(CreatorId.ToString(), result.BlobUrl);
    }

    [Fact]
    public async Task ConfirmUploadAsync_HappyPath_ConfirmsWithCorrectSizeBytes()
    {
        var h = BuildHarness();
        var uploadResult = await h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductThumbnail, "image/jpeg", CancellationToken.None);
        h.BlobStorageClient.PropertiesToReturn = new BlobProperties(123_456, "image/jpeg");

        var confirmResult = await h.Service.ConfirmUploadAsync(CreatorId, uploadResult.BlobUrl, CancellationToken.None);

        var reference = Assert.Single(h.Repository.References);
        Assert.Equal(BlobReferenceStatus.Confirmed, reference.Status);
        Assert.Equal(123_456, reference.SizeBytes);
        Assert.NotNull(reference.ConfirmedAt);
        Assert.Equal(uploadResult.BlobUrl, confirmResult.Url);
        Assert.Empty(h.BlobStorageClient.DeleteCalls);
    }

    [Fact]
    public async Task ConfirmUploadAsync_DifferentCreatorOnSameBlobUrl_ThrowsNotFound()
    {
        var h = BuildHarness();
        var uploadResult = await h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductThumbnail, "image/jpeg", CancellationToken.None);
        h.BlobStorageClient.PropertiesToReturn = new BlobProperties(1000, "image/jpeg");

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.ConfirmUploadAsync(OtherCreatorId, uploadResult.BlobUrl, CancellationToken.None));

        var reference = Assert.Single(h.Repository.References);
        Assert.Equal(BlobReferenceStatus.Pending, reference.Status);
    }

    [Fact]
    public async Task ConfirmUploadAsync_ExpiredPending_ThrowsConflictAndDoesNotCallGetPropertiesOrDelete()
    {
        var h = BuildHarness(sasExpiryMinutes: 15);
        var longAgo = DateTimeOffset.UtcNow.AddMinutes(-30);
        var reference = BlobReference.CreatePending(CreatorId, MediaUploadPurpose.ProductThumbnail, "https://fake.blob.core.windows.net/creator-media/expired.jpg", "image/jpeg", longAgo);
        await h.Repository.AddAsync(reference, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.ConfirmUploadAsync(CreatorId, reference.BlobUrl, CancellationToken.None));

        Assert.Empty(h.BlobStorageClient.GetPropertiesCalls);
        Assert.Empty(h.BlobStorageClient.DeleteCalls);
        Assert.Equal(BlobReferenceStatus.Pending, reference.Status);
    }

    [Fact]
    public async Task ConfirmUploadAsync_BlobNeverUploaded_ThrowsConflictAndDoesNotCallDelete()
    {
        var h = BuildHarness();
        var uploadResult = await h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductThumbnail, "image/jpeg", CancellationToken.None);
        h.BlobStorageClient.PropertiesToReturn = null;

        await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.ConfirmUploadAsync(CreatorId, uploadResult.BlobUrl, CancellationToken.None));

        Assert.Empty(h.BlobStorageClient.DeleteCalls);
        var reference = Assert.Single(h.Repository.References);
        Assert.Equal(BlobReferenceStatus.Pending, reference.Status);
    }

    [Fact]
    public async Task ConfirmUploadAsync_FileTooLarge_ThrowsBadRequestAndDeletesBlobExactlyOnce()
    {
        var h = BuildHarness();
        var uploadResult = await h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductThumbnail, "image/jpeg", CancellationToken.None);
        h.BlobStorageClient.PropertiesToReturn = new BlobProperties(10_000_000, "image/jpeg");

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.ConfirmUploadAsync(CreatorId, uploadResult.BlobUrl, CancellationToken.None));

        var deleteCall = Assert.Single(h.BlobStorageClient.DeleteCalls);
        Assert.Equal(uploadResult.BlobUrl, deleteCall);
        var reference = Assert.Single(h.Repository.References);
        Assert.Equal(BlobReferenceStatus.Pending, reference.Status);
    }

    [Fact]
    public async Task ConfirmUploadAsync_WrongContentType_ThrowsBadRequestAndDeletesBlobExactlyOnce()
    {
        var h = BuildHarness();
        var uploadResult = await h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductThumbnail, "image/jpeg", CancellationToken.None);
        h.BlobStorageClient.PropertiesToReturn = new BlobProperties(1000, "image/svg+xml");

        await Assert.ThrowsAsync<BadRequestException>(
            () => h.Service.ConfirmUploadAsync(CreatorId, uploadResult.BlobUrl, CancellationToken.None));

        Assert.Single(h.BlobStorageClient.DeleteCalls);
    }

    [Fact]
    public async Task ConfirmUploadAsync_AlreadyConfirmed_ThrowsNotFound()
    {
        var h = BuildHarness();
        var uploadResult = await h.Service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductThumbnail, "image/jpeg", CancellationToken.None);
        h.BlobStorageClient.PropertiesToReturn = new BlobProperties(1000, "image/jpeg");
        await h.Service.ConfirmUploadAsync(CreatorId, uploadResult.BlobUrl, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.ConfirmUploadAsync(CreatorId, uploadResult.BlobUrl, CancellationToken.None));
    }
}
