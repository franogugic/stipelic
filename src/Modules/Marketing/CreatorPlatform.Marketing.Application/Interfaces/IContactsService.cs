using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>A validated export: ownership and filters were checked when it was created, so <see cref="Contacts"/>
/// can be streamed after the response has started. Enumerate it once.</summary>
public sealed record ContactExport(string CreatorSlug, IAsyncEnumerable<ContactDto> Contacts);

public interface IContactsService
{
    /// <summary><paramref name="status"/>: "active" | "unsubscribed" | empty (everyone); anything else is a 400.</summary>
    Task<ContactsPageDto> SearchAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? status, string? afterEmail, int limit,
        CancellationToken ct);

    Task<ContactStatsDto> GetStatsAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>The overview with the growth cut to <paramref name="period"/> (today | 7d | 15d | 30d | 2m | 3m | 6m | all).
    /// 400 for any other period.</summary>
    Task<ContactPeriodStatsDto> GetPeriodStatsAsync(string slug, int ownerUserId, string period, CancellationToken ct);

    /// <summary>Resolves the creator and the optional landing page filter (404s are thrown here, before any
    /// output), and returns the matching contacts as a lazy, keyset-batched stream — never the whole directory in
    /// memory.</summary>
    Task<ContactExport> StartExportAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? status, CancellationToken ct);

    /// <summary>Removes one contact from the creator's directory: its summary and its captures on the
    /// creator's landing pages, atomically. Opt-outs and send history are kept. 404 if the creator has no
    /// contact with that email.</summary>
    Task DeleteAsync(string slug, int ownerUserId, string email, CancellationToken ct);
}
