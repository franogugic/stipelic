using System.Web;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Media;

/// <summary>F8.1: the signed URLs of the private container, generated offline with the storage emulator's well-known
/// account key. A download URL reads one blob for ten minutes and makes the browser save it under the product's file
/// name; an upload URL can only create its blob, never overwrite it (S3).</summary>
public sealed class ProductFileSasTests
{
    private static readonly AzureProductFileStorage Storage = new(
        Options.Create(new MediaOptions { ConnectionString = "UseDevelopmentStorage=true", PrivateContainerName = "product-files" }),
        NullLogger<AzureProductFileStorage>.Instance);

    [Fact]
    public void ADownloadUrl_ReadsOneBlob_ForTenMinutes_AsAnAttachmentNamedLikeTheFile()
    {
        var before = DateTimeOffset.UtcNow;

        var url = Storage.GenerateDownloadSasUri("ProductFile/1/abc.zip", "Adriatic Presets.zip", "application/zip", TimeSpan.FromMinutes(10));

        var query = HttpUtility.ParseQueryString(url.Query);
        Assert.EndsWith("/product-files/ProductFile/1/abc.zip", url.AbsolutePath);
        Assert.Equal("r", query["sp"]);
        Assert.Equal("b", query["sr"]);
        Assert.Equal("attachment; filename=\"Adriatic Presets.zip\"; filename*=UTF-8''Adriatic%20Presets.zip", query["rscd"]);
        Assert.Equal("application/zip", query["rsct"]);
        var expires = DateTimeOffset.Parse(query["se"]!);
        Assert.InRange(expires, before.AddMinutes(10).AddSeconds(-2), DateTimeOffset.UtcNow.AddMinutes(10).AddSeconds(2));
    }

    [Fact]
    public void AnUploadUrl_CanOnlyCreate_NeverOverwrite()
    {
        var url = Storage.GenerateUploadSasUri("ProductFile/1/abc.pdf", TimeSpan.FromMinutes(15));

        Assert.Equal("c", HttpUtility.ParseQueryString(url.Query)["sp"]);
    }

    [Theory]
    [InlineData("Guide.pdf", "attachment; filename=\"Guide.pdf\"; filename*=UTF-8''Guide.pdf")]
    [InlineData("Vodič \"2026\".pdf", "attachment; filename=\"Vodi_ _2026_.pdf\"; filename*=UTF-8''Vodi%C4%8D%20%222026%22.pdf")]
    [InlineData("čćž", "attachment; filename=\"___\"; filename*=UTF-8''%C4%8D%C4%87%C5%BE")]
    public void TheFileName_IsSafeInTheHeader_AndExactForModernBrowsers(string fileName, string expected)
    {
        Assert.Equal(expected, ContentDispositions.Attachment(fileName));
    }
}
