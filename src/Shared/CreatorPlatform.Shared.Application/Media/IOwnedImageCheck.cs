namespace CreatorPlatform.Shared.Application.Media;

/// <summary>
/// Images that live in our own public container (thumbnails, logos, page images) must be uploads the creator finished:
/// a Confirmed blob reference of that creator. Then the job that deletes abandoned (Pending) uploads can never delete an
/// image in use, and nobody can point a page at another creator's or a half-uploaded blob. External image URLs are not
/// checked here.
/// </summary>
public interface IOwnedImageCheck
{
    public const string ImageNotUploadedCode = "IMAGE_NOT_UPLOADED";

    /// <summary>Throws <c>BadRequestException</c> (<see cref="ImageNotUploadedCode"/>) when any of <paramref name="urls"/>
    /// points at our container and is not a Confirmed image upload of <paramref name="creatorId"/> (null: a workspace not
    /// created yet, which owns nothing). Blank entries are ignored.</summary>
    Task EnsureOwnedAsync(int? creatorId, IEnumerable<string?> urls, CancellationToken ct);
}
