using CreatorPlatform.Shared.Application.Media;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>For tests about something else: every image URL passes the owned-image check.</summary>
public sealed class AnyImageAccepted : IOwnedImageCheck
{
    public Task EnsureOwnedAsync(int? creatorId, IEnumerable<string?> urls, CancellationToken ct) => Task.CompletedTask;
}
