using System.Net.Mail;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Auth.Application.Services;

public sealed class EmailChangeService : IEmailChangeService
{
    public const string EmailInUseCode = "EMAIL_IN_USE";

    private const int MaxEmailLength = 100;
    private const string IncorrectPasswordMessage = "Current password is incorrect.";
    private const string InvalidEmailMessage = "Enter a valid email address.";
    private const string SameEmailMessage = "This is already your email address.";
    private const string RequestAcceptedMessage =
        "If this address can be used, we've sent a confirmation link to it. The change takes effect once you open it.";
    private const string InvalidTokenMessage = "Invalid or expired email change link.";
    private const string EmailInUseMessage = "This email address is already in use.";
    private const string EmailChangedMessage = "Your email address has been changed.";
    private const string NoPendingChangeMessage = "There is no pending email change.";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITokenHasher _tokenHasher;
    private readonly IEmailChangeTokenRepository _emailChangeTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EmailChangeService> _logger;

    public EmailChangeService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IEmailChangeTokenRepository emailChangeTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IUserSessionRepository userSessionRepository,
        IEmailOutboxService emailOutboxService,
        IUnitOfWork unitOfWork,
        ILogger<EmailChangeService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _emailChangeTokenRepository = emailChangeTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _userSessionRepository = userSessionRepository;
        _emailOutboxService = emailOutboxService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RequestEmailChangeResponseDto> RequestAsync(
        CurrentUserDto currentUser, RequestEmailChangeRequestDto request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdForUpdateAsync(currentUser.Id, ct)
            ?? throw new UnauthorizedException("Authentication is required.");

        // 1. Password first: a hijacked session alone must not be enough to move the account to another inbox.
        if (string.IsNullOrEmpty(request.CurrentPassword)
            || !_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new BadRequestException(IncorrectPasswordMessage);
        }

        // 2. Format.
        var newEmail = NormalizeEmail(request.NewEmail);

        // 3. Different from the current address.
        if (newEmail == user.Email)
            throw new BadRequestException(SameEmailMessage);

        // 4. Not taken. The caller gets exactly the same answer either way, so this can't probe for accounts.
        if (await _userRepository.ExistsByEmailAsync(newEmail, ct))
        {
            _logger.LogInformation(
                "Email change requested to an address that is already in use; no link sent. UserPublicId: {UserPublicId}.",
                user.PublicId);
            return Accepted();
        }

        var now = DateTimeOffset.UtcNow;

        // Only the newest link works: never more than one pending change per user.
        foreach (var unused in await _emailChangeTokenRepository.GetUnusedByUserIdAsync(user.Id, ct))
            unused.Invalidate(now);

        var rawToken = _tokenGenerator.GenerateToken();
        var token = EmailChangeToken.Create(user, newEmail, _tokenHasher.Hash(rawToken), now.Add(TokenLifetime), now);

        await _emailChangeTokenRepository.AddAsync(token, ct);
        await _emailOutboxService.QueueEmailChangeVerificationAsync(newEmail, user.FirstName, user.Email, user.PublicId.ToString(), rawToken, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Email change requested. UserPublicId: {UserPublicId}.", user.PublicId);

        return Accepted();
    }

    public async Task<ConfirmEmailChangeResponseDto> ConfirmAsync(
        ConfirmEmailChangeRequestDto request, CurrentUserDto? currentUser, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new BadRequestException(InvalidTokenMessage);

        var tokenHash = _tokenHasher.Hash(request.Token.Trim());
        string? newEmail = null;

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Row lock: a second confirmation of the same link waits here, then sees it used.
                var token = await _emailChangeTokenRepository.GetByTokenHashForUpdateAsync(tokenHash, ct);
                var now = DateTimeOffset.UtcNow;

                // One message for unknown, used and expired — the token's state is never revealed.
                if (token is null || token.IsUsed || token.IsExpired(now))
                    throw new BadRequestException(InvalidTokenMessage);

                var user = await _userRepository.GetByIdForUpdateAsync(token.UserId, ct)
                    ?? throw new BadRequestException(InvalidTokenMessage);

                // Someone may have registered or switched to this address since the link was sent. The unique
                // index on auth.users.Email is the last line of defence for a race past this check.
                if (await _userRepository.ExistsByEmailAsync(token.NewEmail, ct))
                    throw new ConflictException(EmailInUseMessage, EmailInUseCode);

                var oldEmail = user.Email;
                user.ChangeEmail(token.NewEmail, now);
                token.MarkAsUsed(now);

                // Retire every other pending change, and every outstanding password reset link — those were
                // mailed to the old address, which must no longer be a way into the account.
                foreach (var other in await _emailChangeTokenRepository.GetUnusedByUserIdAsync(user.Id, ct))
                    other.Invalidate(now);
                foreach (var resetToken in await _passwordResetTokenRepository.GetUnusedByUserIdAsync(user.Id, ct))
                    resetToken.Invalidate(now);

                // Sign out everywhere else. The confirming session is kept only when it is this user's own.
                var keepSessionId = currentUser?.Id == user.Id ? currentUser.SessionId : (Guid?)null;
                foreach (var session in await _userSessionRepository.GetActiveByUserIdAsync(user.Id, now, ct))
                {
                    if (session.Id != keepSessionId)
                        session.Revoke(now);
                }

                await _emailOutboxService.QueueEmailChangedNotificationAsync(
                    oldEmail, user.FirstName, user.PublicId.ToString(), token.NewEmail, now, ct);

                await _unitOfWork.SaveChangesAsync(ct);

                newEmail = token.NewEmail;
                _logger.LogInformation("Email changed. UserPublicId: {UserPublicId}.", user.PublicId);
            }, ct);
        }
        catch (UserAlreadyExistsException)
        {
            // Lost the race to the unique index: same answer as the pre-check above.
            throw new ConflictException(EmailInUseMessage, EmailInUseCode);
        }

        return new ConfirmEmailChangeResponseDto { Message = EmailChangedMessage, Email = newEmail! };
    }

    public async Task<PendingEmailChangeDto?> GetPendingAsync(CurrentUserDto currentUser, CancellationToken ct)
    {
        var pending = await _emailChangeTokenRepository.GetPendingByUserIdAsync(currentUser.Id, DateTimeOffset.UtcNow, ct);

        return pending is null ? null : new PendingEmailChangeDto { NewEmail = pending.NewEmail, ExpiresAt = pending.ExpiresAt };
    }

    public async Task<RequestEmailChangeResponseDto> ResendAsync(CurrentUserDto currentUser, CancellationToken ct)
    {
        // The row lock serializes resends (and a request) of the same user, so only one fresh link survives.
        var user = await _userRepository.GetByIdForUpdateAsync(currentUser.Id, ct)
            ?? throw new UnauthorizedException("Authentication is required.");

        var now = DateTimeOffset.UtcNow;
        var unused = await _emailChangeTokenRepository.GetUnusedByUserIdAsync(user.Id, ct);
        var pending = unused
            .Where(token => !token.IsExpired(now))
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefault()
            ?? throw new NotFoundException(NoPendingChangeMessage);

        // The old link stops working either way.
        foreach (var token in unused)
            token.Invalidate(now);

        // The address may have been taken since the request: retire the change silently, same answer as a send.
        if (await _userRepository.ExistsByEmailAsync(pending.NewEmail, ct))
        {
            await _unitOfWork.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Email change resend for an address that is now in use; the change was retired and no link sent. UserPublicId: {UserPublicId}.",
                user.PublicId);
            return Accepted();
        }

        var rawToken = _tokenGenerator.GenerateToken();
        var fresh = EmailChangeToken.Create(user, pending.NewEmail, _tokenHasher.Hash(rawToken), now.Add(TokenLifetime), now);

        await _emailChangeTokenRepository.AddAsync(fresh, ct);
        await _emailOutboxService.QueueEmailChangeVerificationAsync(pending.NewEmail, user.FirstName, user.Email, user.PublicId.ToString(), rawToken, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Email change link sent again. UserPublicId: {UserPublicId}.", user.PublicId);

        return Accepted();
    }

    public async Task CancelAsync(CurrentUserDto currentUser, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var unused = await _emailChangeTokenRepository.GetUnusedByUserIdAsync(currentUser.Id, ct);
        if (unused.Count == 0)
            return;

        foreach (var token in unused)
            token.Invalidate(now);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Pending email change cancelled. UserId: {UserId}.", currentUser.Id);
    }

    private static RequestEmailChangeResponseDto Accepted() => new() { Message = RequestAcceptedMessage };

    private static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0 || normalized.Length > MaxEmailLength)
            throw new BadRequestException(InvalidEmailMessage);

        try
        {
            // MailAddress also accepts "Name <a@b.c>" — require that the whole input is just the address.
            if (new MailAddress(normalized).Address != normalized)
                throw new BadRequestException(InvalidEmailMessage);
        }
        catch (FormatException)
        {
            throw new BadRequestException(InvalidEmailMessage);
        }

        return normalized;
    }
}
