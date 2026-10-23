using CreatorPlatform.Shared.Application.Media;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

/// <summary>Records the image URLs a service asked about; accepts them all unless <see cref="Refuse"/> is set.</summary>
public sealed class FakeOwnedImageCheck : IOwnedImageCheck
{
    public List<(int? CreatorId, List<string?> Urls)> Calls { get; } = [];
    public bool Refuse { get; set; }

    public Task EnsureOwnedAsync(int? creatorId, IEnumerable<string?> urls, CancellationToken ct)
    {
        Calls.Add((creatorId, urls.ToList()));
        if (Refuse)
            throw new CreatorPlatform.Shared.Application.Exceptions.BadRequestException("Not your upload.", IOwnedImageCheck.ImageNotUploadedCode);
        return Task.CompletedTask;
    }
}
