using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Application.Options;

/// <summary>Fails the start on unusable media settings. Container names are only required once storage is configured
/// (a connection string is set), so a host without storage still starts.</summary>
public sealed class MediaOptionsValidator : IValidateOptions<MediaOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaOptions options)
    {
        var failures = new List<string>();

        if (options.MaxFileSizeBytes <= 0)
            failures.Add("Media:MaxFileSizeBytes must be positive.");
        if (options.ProductFileMaxSizeBytes is <= 0 or > 1_073_741_824)
            failures.Add("Media:ProductFileMaxSizeBytes must be between 1 byte and 1 GB.");
        if (options.SasExpiryMinutes is < 1 or > 60)
            failures.Add("Media:SasExpiryMinutes must be between 1 and 60.");
        if (options.DownloadUrlExpiryMinutes is < 1 or > 60)
            failures.Add("Media:DownloadUrlExpiryMinutes must be between 1 and 60.");
        if (options.ProductFileContentTypes.Length == 0)
            failures.Add("Media:ProductFileContentTypes must list at least one type.");

        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            if (string.IsNullOrWhiteSpace(options.ContainerName))
                failures.Add("Media:ContainerName is required when Media:ConnectionString is set.");
            if (string.IsNullOrWhiteSpace(options.PrivateContainerName))
                failures.Add("Media:PrivateContainerName is required when Media:ConnectionString is set.");
            else if (string.Equals(options.PrivateContainerName, options.ContainerName, StringComparison.OrdinalIgnoreCase))
                failures.Add("Media:PrivateContainerName must differ from Media:ContainerName (that one is public).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
