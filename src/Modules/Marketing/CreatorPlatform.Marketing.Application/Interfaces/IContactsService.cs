using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <param name="AffectedLandingPageIds">Internal ids of the landing pages that lost a capture — the caller
/// invalidates their cached analytics.</param>
public sealed record ContactDeletionResult(IReadOnlyList<int> AffectedLandingPageIds);

public interface IContactsService
{
    Task<ContactsPageDto> SearchAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? afterEmail, int limit,
        CancellationToken ct);

    Task<ContactStatsDto> GetStatsAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>Removes one contact from the creator's directory: its summary and its captures on the
    /// creator's landing pages, atomically. Opt-outs and send history are kept. 404 if the creator has no
    /// contact with that email.</summary>
    Task<ContactDeletionResult> DeleteAsync(string slug, int ownerUserId, string email, CancellationToken ct);
}
