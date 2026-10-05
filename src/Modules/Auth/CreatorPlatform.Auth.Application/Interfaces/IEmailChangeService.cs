using CreatorPlatform.Auth.Application.Dtos;

namespace CreatorPlatform.Auth.Application.Interfaces;

public interface IEmailChangeService
{
    /// <summary>Checks, in order: the current password (400), the new address's format (400), that it differs from
    /// the current one (400), that it isn't taken. Taken → the same response as sent, with no token and no mail.
    /// Otherwise retires the user's earlier unused links, creates a 24-hour link and mails it to the NEW address.</summary>
    Task<RequestEmailChangeResponseDto> RequestAsync(
        CurrentUserDto currentUser, RequestEmailChangeRequestDto request, CancellationToken ct);

    /// <summary>Applies the change for a valid, unexpired, unused link: re-checks that the address is still free
    /// (409), switches the email (counting as verified), signs the user out everywhere except
    /// <paramref name="currentUser"/>'s own session, and notifies the OLD address. 400 for an unknown, used or
    /// expired link. Works with or without a session.</summary>
    Task<ConfirmEmailChangeResponseDto> ConfirmAsync(
        ConfirmEmailChangeRequestDto request, CurrentUserDto? currentUser, CancellationToken ct);

    /// <summary>The newest unused, unexpired change of the user, or null.</summary>
    Task<PendingEmailChangeDto?> GetPendingAsync(CurrentUserDto currentUser, CancellationToken ct);

    /// <summary>Sends the pending change's link again: retires the user's unused links and mails a fresh 24-hour link
    /// for the same address. If the address has been taken since, every link is retired and no mail goes out — with
    /// the same answer, so it can't probe for accounts. 404 when there is no pending change.</summary>
    Task<RequestEmailChangeResponseDto> ResendAsync(CurrentUserDto currentUser, CancellationToken ct);

    /// <summary>Retires every unused link of the user. Idempotent.</summary>
    Task CancelAsync(CurrentUserDto currentUser, CancellationToken ct);
}
