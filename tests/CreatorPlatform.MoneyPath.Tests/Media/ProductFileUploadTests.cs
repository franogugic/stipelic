using System.Text;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Application.Services;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Media;

/// <summary>F8.1: a product document is accepted only when the stored blob really is a PDF, EPUB or ZIP within the size
/// limit; anything else is deleted and refused. The answer is an opaque reference, never a URL.</summary>
public class ProductFileUploadTests
{
    private const int CreatorId = 1;
    private const long MaxBytes = 104_857_600;

    private readonly FakeBlobReferenceRepository _references = new();
    private readonly FakeProductFileStorage _storage = new();
    private readonly FakeMediaUnitOfWork _unitOfWork = new();
    private readonly MediaUploadService _service;

    public ProductFileUploadTests()
    {
        var options = new MediaOptions
        {
            ConnectionString = "unused-in-tests",
            ContainerName = "creator-media",
            PrivateContainerName = "product-files",
            MaxFileSizeBytes = 5_242_880,
            ProductFileMaxSizeBytes = MaxBytes,
            SasExpiryMinutes = 15,
            AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"],
            ProductFileContentTypes = ["application/pdf", "application/epub+zip", "application/zip"],
        };
        _service = new MediaUploadService(_references, new FakeBlobStorageClient(), _storage, _unitOfWork, Options.Create(options));
    }

    private static byte[] Pdf(int size = 2048) => Pad("%PDF-1.7\n"u8.ToArray(), size);

    private static byte[] Zip(int size = 2048) => Pad([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00], size);

    /// <summary>A ZIP local file header for an uncompressed first entry <paramref name="name"/> holding <paramref name="data"/>.</summary>
    private static byte[] ZipFirstEntry(string name, string data, ushort compression = 0, int size = 4096)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var dataBytes = Encoding.ASCII.GetBytes(data);
        var header = new byte[30];
        header[0] = 0x50; header[1] = 0x4B; header[2] = 0x03; header[3] = 0x04;
        header[8] = (byte)compression; header[9] = (byte)(compression >> 8);
        header[26] = (byte)nameBytes.Length;
        return Pad([.. header, .. nameBytes, .. dataBytes], size);
    }

    private static byte[] Epub() => ZipFirstEntry("mimetype", "application/epub+zip");

    private static byte[] Pad(byte[] head, int size) => [.. head, .. new byte[Math.Max(0, size - head.Length)]];

    private async Task<(string BlobUrl, BlobReference Reference)> UploadAsync(string declaredType, string storedType, byte[] bytes)
    {
        var upload = await _service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductFile, declaredType, CancellationToken.None);
        var reference = Assert.Single(_references.References, r => r.BlobUrl == upload.BlobUrl);
        _storage.Put(reference.BlobPath!, storedType, bytes);
        return (upload.BlobUrl, reference);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("application/epub+zip")]
    [InlineData("application/zip")]
    public async Task ARealDocument_IsConfirmed_AndAnsweredWithAReferenceNotAUrl(string contentType)
    {
        var bytes = contentType switch { "application/pdf" => Pdf(), "application/zip" => Zip(), _ => Epub() };
        var (blobUrl, reference) = await UploadAsync(contentType, contentType, bytes);

        var result = await _service.ConfirmUploadAsync(CreatorId, blobUrl, CancellationToken.None, "My Guide.pdf");

        Assert.Null(result.Url);
        Assert.Equal(reference.PublicId, result.FileReference);
        Assert.Equal("My Guide.pdf", result.FileName);
        Assert.Equal(bytes.LongLength, result.FileSizeBytes);
        Assert.Equal("My Guide.pdf", reference.FileName);
        Assert.Equal(contentType, result.ContentType);
        Assert.Equal(BlobReferenceStatus.Confirmed, reference.Status);
        Assert.Empty(_storage.DeleteCalls);
        Assert.StartsWith("ProductFile/1/", reference.BlobPath);
    }

    public static TheoryData<string, byte[]> FakeDocuments => new()
    {
        // A PNG renamed to .pdf.
        { "application/pdf", Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], 2048) },
        // An ordinary ZIP declared as an EPUB: its first entry is not "mimetype".
        { "application/epub+zip", ZipFirstEntry("chapter1.xhtml", "<html/>") },
        // The right entry name but the wrong content.
        { "application/epub+zip", ZipFirstEntry("mimetype", "application/zip+epub") },
        // A compressed mimetype entry is not a valid EPUB container.
        { "application/epub+zip", ZipFirstEntry("mimetype", "application/epub+zip", compression: 8) },
        // A PDF declared as a ZIP.
        { "application/zip", Pdf() },
    };

    [Theory]
    [MemberData(nameof(FakeDocuments))]
    public async Task AFakeDocument_IsDeletedAndRefused(string contentType, byte[] bytes)
    {
        var (blobUrl, reference) = await UploadAsync(contentType, contentType, bytes);

        var error = await Assert.ThrowsAsync<BadRequestException>(() => _service.ConfirmUploadAsync(CreatorId, blobUrl, CancellationToken.None, "Guide.pdf"));

        Assert.Equal(MediaUploadService.FileRejectedCode, error.Code);
        Assert.Equal([reference.BlobPath!], _storage.DeleteCalls);
        Assert.Equal(BlobReferenceStatus.Pending, reference.Status);
    }

    [Fact]
    public async Task AnOversizeFile_IsDeletedAndRefused()
    {
        var (blobUrl, reference) = await UploadAsync("application/pdf", "application/pdf", Pdf());
        _storage.ReportedLengths[reference.BlobPath!] = MaxBytes + 1;

        await Assert.ThrowsAsync<BadRequestException>(() => _service.ConfirmUploadAsync(CreatorId, blobUrl, CancellationToken.None, "Guide.pdf"));

        Assert.Equal([reference.BlobPath!], _storage.DeleteCalls);
    }

    [Fact]
    public async Task AFileStoredWithAnotherTypeThanDeclared_IsDeletedAndRefused()
    {
        // Declared a PDF, but the browser stored it as a ZIP (a valid one).
        var (blobUrl, reference) = await UploadAsync("application/pdf", "application/zip", Zip());

        await Assert.ThrowsAsync<BadRequestException>(() => _service.ConfirmUploadAsync(CreatorId, blobUrl, CancellationToken.None, "Guide.pdf"));

        Assert.Equal([reference.BlobPath!], _storage.DeleteCalls);
    }

    [Fact]
    public async Task AnUnsupportedType_IsRefusedBeforeAnyUploadUrl()
    {
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductFile, "application/x-msdownload", CancellationToken.None));

        Assert.Empty(_references.References);
    }

    [Fact]
    public async Task ConfirmingAProductFile_NeedsItsName_AndKeepsOnlyTheName()
    {
        var (blobUrl, reference) = await UploadAsync("application/pdf", "application/pdf", Pdf());

        var missing = await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.ConfirmUploadAsync(CreatorId, blobUrl, CancellationToken.None, "  "));
        Assert.Equal(MediaUploadService.FileNameRequiredCode, missing.Code);
        Assert.Equal(BlobReferenceStatus.Pending, reference.Status);    // nothing deleted: the client can confirm again
        Assert.Empty(_storage.DeleteCalls);

        var result = await _service.ConfirmUploadAsync(CreatorId, blobUrl, CancellationToken.None, "..\\..\\secret\\Guide.pdf");
        Assert.Equal("Guide.pdf", result.FileName);
        Assert.Equal("Guide.pdf", reference.FileName);
    }

    [Fact]
    public async Task ANameGivenAtUploadUrl_IsChecked_AndUsedWhenTheConfirmGivesNone()
    {
        var blank = await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductFile, "application/pdf", CancellationToken.None, " "));
        Assert.Equal(MediaUploadService.FileNameRequiredCode, blank.Code);

        var upload = await _service.RequestUploadUrlAsync(CreatorId, MediaUploadPurpose.ProductFile, "application/pdf", CancellationToken.None, "Early.pdf");
        var reference = Assert.Single(_references.References, r => r.BlobUrl == upload.BlobUrl);
        _storage.Put(reference.BlobPath!, "application/pdf", Pdf());

        var result = await _service.ConfirmUploadAsync(CreatorId, upload.BlobUrl, CancellationToken.None);

        Assert.Equal("Early.pdf", result.FileName);
    }
}
