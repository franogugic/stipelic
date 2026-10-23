using System.Text.Json;
using CreatorPlatform.Access.Application.Dtos;
using CreatorPlatform.Media.Application.Dtos;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Products.Application.Dtos;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>F8.1.1: the JSON field names the F8.2 frontend reads (the contract agreed with it), as the API writes them.</summary>
public sealed class ProductFileContractTests
{
    private static HashSet<string> Keys<T>(T value) =>
        JsonSerializer.SerializeToElement(value, JsonSerializerOptions.Web).EnumerateObject().Select(p => p.Name).ToHashSet();

    [Fact]
    public void ConfirmingAProductFile_AnswersItsReference_AndAnImageItsUrl()
    {
        Assert.Equal(["fileReference", "fileName", "fileSizeBytes", "contentType"],
            Keys(new ConfirmUploadResponseDto { FileReference = Guid.NewGuid(), FileName = "a.pdf", FileSizeBytes = 1, ContentType = "application/pdf" }));
        Assert.Equal(["url"], Keys(new ConfirmUploadResponseDto { Url = "https://cdn.test/a.png" }));
    }

    [Fact]
    public void AProduct_CarriesItsFileFlat_AndFreeProductsTheirSignUps()
    {
        var keys = Keys(new ProductResponseDto());

        Assert.Superset(new HashSet<string> { "fileName", "fileSizeBytes", "fileContentType", "fileUploadedAt", "signupCount", "signupsThisMonth", "signupsTrend", "salesTrend" }, keys);
        Assert.DoesNotContain("file", keys);
        Assert.DoesNotContain("type", keys);
        Assert.DoesNotContain("accessUrl", keys);
    }

    [Fact]
    public void TheDownloadPage_GetsTheBrandColor_AndTheReceiptTheFile()
    {
        var access = new ProductAccessDto("Guide", "Guide.pdf", 1, new ProductAccessBrandDto("Acme", "#000000", null), "capture", null, null);
        Assert.Equal(["name", "color", "logoUrl"],
            JsonSerializer.SerializeToElement(access, JsonSerializerOptions.Web).GetProperty("brand").EnumerateObject().Select(p => p.Name).ToHashSet());

        var receipt = new OrderReceiptDto("A1B2C3D4", "Paid", null, null, "Guide", 2900, "Eur", null,
            new OrderReceiptCreatorDto("Acme", "acme", null, null, null), "token", "Guide.pdf", 1024);
        Assert.Superset(new HashSet<string> { "accessToken", "fileName", "fileSizeBytes" }, Keys(receipt));
    }
}
