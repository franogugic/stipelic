using CreatorPlatform.Access.Application.Dtos;
using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Access.Application.Services;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Products.Domain.Access;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Access;

/// <summary>F8.1: the public download page. A malformed or unknown token is 404 ACCESS_INVALID, a refunded order's link
/// 410 ACCESS_REVOKED; a download is a fresh short-lived signed URL for the product's file, named as the file.</summary>
public class ProductAccessServiceTests
{
    private readonly FakeAccessGrantReader _reader = new();
    private readonly FakeProductFileStorage _storage = new();
    private readonly ProductAccessService _service;

    public ProductAccessServiceTests()
    {
        _service = new ProductAccessService(_reader, _storage, Options.Create(new MediaOptions { DownloadUrlExpiryMinutes = 10 }));
    }

    private static readonly DateTimeOffset RefundedAt = new(2026, 10, 2, 9, 30, 0, TimeSpan.Zero);

    private static AccessGrantView View(bool revoked = false, Guid? orderPublicId = null) => new(
        revoked ? RefundedAt : null, "Adriatic Summer Presets", "Adriatic Presets.zip", 18_350_080, "MH Studio workspace", "MH Studio", "#E2553A",
        "https://cdn.test/logo.png", orderPublicId, orderPublicId is null ? null : new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]   // 43 characters, not base64url
    public async Task AMalformedToken_IsInvalid_WithoutTouchingTheDatabase(string? token)
    {
        var get = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(token, CancellationToken.None));
        var download = await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadAsync(token, CancellationToken.None));

        Assert.Equal(ProductAccessService.InvalidCode, get.Code);
        Assert.Equal(ProductAccessService.InvalidCode, download.Code);
        Assert.Equal(0, _reader.Calls);
    }

    [Fact]
    public async Task AnUnknownToken_IsInvalid()
    {
        var token = AccessGrant.NewToken();

        Assert.Equal(ProductAccessService.InvalidCode,
            (await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(token, CancellationToken.None))).Code);
        Assert.Equal(ProductAccessService.InvalidCode,
            (await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadAsync(token, CancellationToken.None))).Code);
        Assert.Empty(_storage.DownloadSasCalls);
    }

    [Fact]
    public async Task ABuyersLink_ShowsTheFileTheBrandAndTheOrder()
    {
        var token = AccessGrant.NewToken();
        var orderPublicId = Guid.Parse("a1b2c3d4-0000-0000-0000-000000000000");
        _reader.Grants[token] = View(orderPublicId: orderPublicId);

        var access = await _service.GetAsync(token, CancellationToken.None);

        Assert.Equal("Adriatic Summer Presets", access.ProductName);
        Assert.Equal("Adriatic Presets.zip", access.FileName);
        Assert.Equal(18_350_080, access.FileSizeBytes);
        Assert.Equal(new("MH Studio", "#E2553A", "https://cdn.test/logo.png"), access.Brand);
        Assert.Equal("order", access.Source);
        Assert.Equal("A1B2C3D4", access.OrderNumber);
        Assert.NotNull(access.PaidAt);
    }

    [Fact]
    public async Task ASignUpsLink_IsACapture_WithoutOrderFields()
    {
        var token = AccessGrant.NewToken();
        _reader.Grants[token] = View();

        var access = await _service.GetAsync(token, CancellationToken.None);

        Assert.Equal("capture", access.Source);
        Assert.Null(access.OrderNumber);
        Assert.Null(access.PaidAt);
    }

    [Fact]
    public async Task ARefundedLink_IsGone_WithWhatTheTurnedOffPageShows_AndDownloadsNothing()
    {
        var token = AccessGrant.NewToken();
        _reader.Grants[token] = View(revoked: true, orderPublicId: Guid.Parse("a1b2c3d4-0000-0000-0000-000000000000"));

        var get = await Assert.ThrowsAsync<GoneException>(() => _service.GetAsync(token, CancellationToken.None));
        var download = await Assert.ThrowsAsync<GoneException>(() => _service.DownloadAsync(token, CancellationToken.None));

        Assert.Equal(
            new ProductAccessRevokedDto("Adriatic Summer Presets", new("MH Studio", "#E2553A", "https://cdn.test/logo.png"), "A1B2C3D4", RefundedAt),
            get.Body);
        Assert.Equal(get.Body, download.Body);
        Assert.Equal(ProductAccessService.RevokedCode, get.Code);
        Assert.Equal(ProductAccessService.RevokedCode, download.Code);
        Assert.Empty(_storage.DownloadSasCalls);
        Assert.Equal(0, _reader.DownloadsRecorded);
    }

    [Fact]
    public async Task ADownload_IsATenMinuteSignedUrl_NamedAsTheFile_AndCounted()
    {
        var token = AccessGrant.NewToken();
        _reader.Grants[token] = View(orderPublicId: Guid.NewGuid());
        _reader.Targets[token] = new AccessDownloadTarget("ProductFile/1/abc.zip", "Adriatic Presets.zip", "application/zip");

        var download = await _service.DownloadAsync(token, CancellationToken.None);

        var sas = Assert.Single(_storage.DownloadSasCalls);
        Assert.Equal(("ProductFile/1/abc.zip", "Adriatic Presets.zip", "application/zip", TimeSpan.FromMinutes(10)), sas);
        Assert.StartsWith("https://fake.blob.core.windows.net/product-files/ProductFile/1/abc.zip?", download.Url);
        Assert.Equal(1, _reader.DownloadsRecorded);
    }

    private sealed class FakeAccessGrantReader : IAccessGrantReader
    {
        public Dictionary<string, AccessGrantView> Grants { get; } = [];
        public Dictionary<string, AccessDownloadTarget> Targets { get; } = [];
        public int Calls { get; private set; }
        public int DownloadsRecorded { get; private set; }

        public Task<AccessGrantView?> GetAsync(string token, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Grants.GetValueOrDefault(token));
        }

        public Task<AccessDownloadTarget?> RecordDownloadAsync(string token, DateTimeOffset downloadedAt, CancellationToken ct)
        {
            Calls++;
            // Like the real statement: only a link that is on counts and returns the file.
            if (!Grants.TryGetValue(token, out var grant) || grant.RevokedAt is not null || !Targets.TryGetValue(token, out var target))
                return Task.FromResult<AccessDownloadTarget?>(null);

            DownloadsRecorded++;
            return Task.FromResult<AccessDownloadTarget?>(target);
        }
    }
}
