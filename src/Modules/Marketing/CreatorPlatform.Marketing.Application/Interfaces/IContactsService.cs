using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <param name="AffectedLandingPageIds">Internal ids of the landing pages that lost a capture — the caller
/// invalidates their cached analytics.</param>
public sealed record ContactDeletionResult(IReadOnlyList<int> AffectedLandingPageIds);

/// <summary>A validated export: ownership and filters were checked when it was created, so <see cref="Contacts"/>
/// can be streamed after the response has started. Enumerate it once.</summary>
public sealed record ContactExport(string CreatorSlug, IAsyncEnumerable<ContactDto> Contacts);

public interface IContactsService
{
    Task<ContactsPageDto> SearchAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? afterEmail, int limit,
        CancellationToken ct);

    Task<ContactStatsDto> GetStatsAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>Resolves the creator and the optional landing page filter (404s are thrown here, before any
    /// output), and returns the matching contacts as a lazy, keyset-batched stream — never the whole directory in
    /// memory.</summary>
    Task<ContactExport> StartExportAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, CancellationToken ct);

    /// <summary>Removes one contact from the creator's directory: its summary and its captures on the
    /// creator's landing pages, atomically. Opt-outs and send history are kept. 404 if the creator has no
    /// contact with that email.</summary>
    Task<ContactDeletionResult> DeleteAsync(string slug, int ownerUserId, string email, CancellationToken ct);
}
