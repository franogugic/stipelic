using System.Web;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Media;

/// <summary>S3: an image upload link may create its one blob but never overwrite it (SAS permission "c" only, no "w"),
/// so an image confirm has checked cannot be swapped afterwards (product files: ProductFileSasTests). Signed offline with
/// the development account's key; nothing is sent.</summary>
public sealed class UploadSasPermissionsTests
{
    private static readonly IOptions<MediaOptions> Options = Microsoft.Extensions.Options.Options.Create(new MediaOptions
    {
        ConnectionString = "UseDevelopmentStorage=true",
        ContainerName = "creator-media",
        PrivateContainerName = "product-files",
    });

    private static string Permissions(Uri sas) => HttpUtility.ParseQueryString(sas.Query)["sp"]!;

    [Fact]
    public void AnImageUploadLink_CanOnlyCreate()
    {
        var client = new AzureBlobStorageClient(Options, NullLogger<AzureBlobStorageClient>.Instance);

        Assert.Equal("c", Permissions(client.GenerateUploadSasUri("ProductThumbnail/1/x.png", "image/png", TimeSpan.FromMinutes(15))));
    }
}
