using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Infrastructure.Maintenance;

/// <summary>An upload must not count as abandoned while its upload URL still works: the client may be uploading right then
/// and confirm a moment later. Requires the cleanup age to outlast <c>Media:SasExpiryMinutes</c> by
/// <see cref="MarginMinutes"/>.</summary>
public sealed class AbandonedUploadsOptionsValidator(IOptions<MediaOptions> mediaOptions) : IValidateOptions<MaintenanceOptions>
{
    public const int MarginMinutes = 15;

    public ValidateOptionsResult Validate(string? name, MaintenanceOptions options)
    {
        var minimum = mediaOptions.Value.SasExpiryMinutes + MarginMinutes;

        return options.AbandonedUploadMinutes >= minimum
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Maintenance:AbandonedUploadMinutes must be at least Media:SasExpiryMinutes + {MarginMinutes} ({minimum}), " +
                "so an upload in progress is never deleted.");
    }
}
