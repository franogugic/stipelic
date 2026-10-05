using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>The web app's unsubscribe page. A bad, tampered or no-longer-valid token (e.g. its workspace was deleted)
/// → 404 with code <c>unsubscribe_link_invalid</c>.</summary>
public interface IUnsubscribePageService
{
    /// <summary>The creator's brand and whether the address is already unsubscribed. No side effect.</summary>
    Task<UnsubscribePageDto> GetInfoAsync(string token, CancellationToken ct);

    /// <summary>Unsubscribes the address from all of the creator's future campaigns. Idempotent.</summary>
    Task<UnsubscribePageDto> ConfirmAsync(string token, CancellationToken ct);
}
