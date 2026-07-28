using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Application.Options;
using CreatorPlatform.Auth.Domain.Roles;
using CreatorPlatform.Auth.Domain.Sessions;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Auth.Domain.Users;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Auth.Application.Services;

public sealed class AuthService : IAuthService
{
    private const string InvalidEmailVerificationTokenMessage = "Invalid or expired email verification token.";
    private const string EmailVerifiedSuccessfullyMessage = "Email verified successfully.";
    private const string LoggedOutSuccessfullyMessage = "Logged out successfully.";
    private const string ResendEmailVerificationMessage = "If an account exists and requires verification, a new email will be sent.";
    private const string InvalidLoginCredentialsMessage = "Invalid email or password.";
    private const string RequestPasswordResetMessage = "If an account exists for that email, we've sent a password reset link.";
    private const string InvalidPasswordResetTokenMessage = "Invalid or expired reset link.";
    private const string PasswordResetSuccessMessage = "Your password has been reset. Please sign in with your new password.";
    private static readonly TimeSpan ResendEmailVerificationCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITokenHasher _tokenHasher;
    private readonly IEmailVerificationTokenRepository _emailVerificationTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly ILogger<AuthService> _logger;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly AuthOptions _authOptions;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IEmailVerificationTokenRepository emailVerificationTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IUnitOfWork unitOfWork,
        IUserRoleRepository userRoleRepository,
        IEmailOutboxService emailOutboxService,
        ILogger<AuthService> logger,
        IUserSessionRepository userSessionRepository,
        IOptions<AuthOptions> authOptions
        )
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _emailVerificationTokenRepository = emailVerificationTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _unitOfWork = unitOfWork;
        _userRoleRepository = userRoleRepository;
        _emailOutboxService = emailOutboxService;
        _logger = logger;
        _userSessionRepository = userSessionRepository;
        _authOptions = authOptions.Value;
    }

    public async Task<RegisterUserResponseDto> RegisterAsync(RegisterUserRequestDto request,
        CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var doesExist = await _userRepository.ExistsByEmailAsync(email, ct);
        if (doesExist)
            throw new UserAlreadyExistsException();

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        CheckName(firstName, nameof(request.FirstName));
        CheckName(lastName, nameof(request.LastName));
        CheckPassword(request.Password);

        var createdAt = DateTimeOffset.UtcNow;
        var passwordHash = _passwordHasher.Hash(request.Password);

        var user = User.Create(
            email,
            passwordHash,
            firstName,
            lastName,
            createdAt);
        
        var rawEmailVerificationToken = _tokenGenerator.GenerateToken();
        var hashedEmailVerificationToken = _tokenHasher.Hash(rawEmailVerificationToken);
        
        var emailVerificationToken = EmailVerificationToken.Create(
            user,
            hashedEmailVerificationToken,
            createdAt.AddHours(24),
            createdAt
        );
        var userRole = UserRole.Create(
            user,
            RoleId.User,
            createdAt
        );
        
        await _userRepository.AddAsync(user, ct);
        await _emailVerificationTokenRepository.AddAsync(emailVerificationToken, ct);
        await _userRoleRepository.AddAsync(userRole, ct);
        await _emailOutboxService.QueueEmailVerificationAsync(
            user.Email,
            user.PublicId.ToString(),
            rawEmailVerificationToken,
            ct);
        
        await _unitOfWork.SaveChangesAsync(ct);
        
        return new RegisterUserResponseDto
        {
            PublicId = user.PublicId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }

    public async Task<LoginUserResultDto> LoginAsync(LoginUserRequestDto request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, ct);

        if (user is null)
            throw new UnauthorizedException(InvalidLoginCredentialsMessage);

        var isPasswordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
            throw new UnauthorizedException(InvalidLoginCredentialsMessage);

        if (user.Status == UserStatus.Disabled)
            throw new UnauthorizedException(InvalidLoginCredentialsMessage);

        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(_authOptions.SessionLifetimeDays);
        var sessionToken = _tokenGenerator.GenerateToken();
        var sessionTokenHash = _tokenHasher.Hash(sessionToken);

        var session = UserSession.Create(
            user.Id,
            sessionTokenHash,
            expiresAt,
            createdAt);

        await _userSessionRepository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new LoginUserResultDto
        {
            SessionToken = sessionToken,
            ExpiresAt = expiresAt,
            User = new LoginUserResponseDto
            {
                PublicId = user.PublicId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                IsEmailVerified = user.IsEmailVerified,
                Status = user.Status.ToString()
            }
        };
    }

    public async Task<LogoutResponseDto> LogoutAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await _userSessionRepository.GetByIdAsync(sessionId, ct);
        if (session is not null && session.RevokedAt is null)
        {
            session.Revoke(DateTimeOffset.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return new LogoutResponseDto
        {
            Message = LoggedOutSuccessfullyMessage
        };
    }

    public async Task<VerifyEmailResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new BadRequestException(InvalidEmailVerificationTokenMessage);

        var token = request.Token.Trim();

        var tokenHash = _tokenHasher.Hash(token);
        var emailVerificationToken = await _emailVerificationTokenRepository.GetByTokenHashAsync(tokenHash, ct);
        
        if (emailVerificationToken is null)
        {
            throw new BadRequestException(InvalidEmailVerificationTokenMessage);
        }
        var now = DateTimeOffset.UtcNow;


        if (emailVerificationToken.User.IsEmailVerified)
        {
            return new VerifyEmailResponseDto
            {
                Message = EmailVerifiedSuccessfullyMessage
            };
        }

        if (emailVerificationToken.IsUsed || emailVerificationToken.IsExpired(now))
        {
            throw new BadRequestException(InvalidEmailVerificationTokenMessage);
        }

        emailVerificationToken.User.VerifyEmail(now);
        emailVerificationToken.MarkAsUsed(now);

        await _unitOfWork.SaveChangesAsync(ct);

        return new VerifyEmailResponseDto
        {
            Message = EmailVerifiedSuccessfullyMessage
        };
    }

    public async Task<ResendEmailVerificationResponseDto> ResendEmailVerificationAsync(
        ResendEmailVerificationRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return CreateResendEmailVerificationResponse();

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, ct);

        if (user is null)
        {
            _logger.LogInformation(
                "Email verification resend skipped because no eligible user was found.");

            return CreateResendEmailVerificationResponse();
        }

        if (user.IsEmailVerified)
        {
            _logger.LogInformation(
                "Email verification resend skipped because user is already verified. UserPublicId: {UserPublicId}.",
                user.PublicId);

            return CreateResendEmailVerificationResponse();
        }

        var createdAt = DateTimeOffset.UtcNow;
        // adding cooldown for next token
        var latestToken = await _emailVerificationTokenRepository.GetLatestByUserIdAsync(user.Id, ct);
        var nextResendAvailableAt = latestToken?.CreatedAt.Add(ResendEmailVerificationCooldown);

        if (latestToken is not null &&
            nextResendAvailableAt > createdAt)
        {
            _logger.LogInformation(
                "Email verification resend skipped because cooldown is still active. UserPublicId: {UserPublicId}. NextAvailableAtUtc: {NextAvailableAtUtc}.",
                user.PublicId,
                nextResendAvailableAt);

            return CreateResendEmailVerificationResponse();
        }

        var rawEmailVerificationToken = _tokenGenerator.GenerateToken();
        var hashedEmailVerificationToken = _tokenHasher.Hash(rawEmailVerificationToken);
        var unusedTokens = await _emailVerificationTokenRepository.GetUnusedByUserIdAsync(user.Id, ct);
        
        // invlaidate all unused tokens to prevent multiple valid tokens existing at the same time, which could be a security risk
        foreach (var unusedToken in unusedTokens)
        {
            unusedToken.Invalidate(createdAt);
        }
        
        // cancel any unsent email verification messages that may be in the outbox by one user, to prevent multiple messages being sent
        await _emailOutboxService.CancelUnsentEmailVerificationMessagesAsync(user.PublicId.ToString(), ct);

        var emailVerificationToken = EmailVerificationToken.Create(
            user,
            hashedEmailVerificationToken,
            createdAt.AddHours(24),
            createdAt);

        await _emailVerificationTokenRepository.AddAsync(emailVerificationToken, ct);
        await _emailOutboxService.QueueEmailVerificationAsync(
            user.Email,
            user.PublicId.ToString(),
            rawEmailVerificationToken,
            ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Email verification resend queued. UserPublicId: {UserPublicId}.",
            user.PublicId);
        
        return CreateResendEmailVerificationResponse();
    }

    public async Task<RequestPasswordResetResponseDto> RequestPasswordResetAsync(
        RequestPasswordResetRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return CreateRequestPasswordResetResponse();

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(email, ct);

        if (user is null)
        {
            // Enumeration protection: identical response whether or not the account exists —
            // same principle as ResendEmailVerificationAsync above.
            _logger.LogInformation(
                "Password reset request skipped because no eligible user was found.");

            return CreateRequestPasswordResetResponse();
        }

        var createdAt = DateTimeOffset.UtcNow;

        // Invalidate all previously-unused tokens first — same hygiene principle as email
        // verification: never leave more than one valid reset link outstanding for a user.
        var unusedTokens = await _passwordResetTokenRepository.GetUnusedByUserIdAsync(user.Id, ct);
        foreach (var unusedToken in unusedTokens)
        {
            unusedToken.Invalidate(createdAt);
        }

        var rawPasswordResetToken = _tokenGenerator.GenerateToken();
        var hashedPasswordResetToken = _tokenHasher.Hash(rawPasswordResetToken);

        var passwordResetToken = PasswordResetToken.Create(
            user,
            hashedPasswordResetToken,
            createdAt.Add(PasswordResetTokenLifetime),
            createdAt);

        await _passwordResetTokenRepository.AddAsync(passwordResetToken, ct);
        await _emailOutboxService.QueuePasswordResetAsync(
            user.Email,
            user.PublicId.ToString(),
            rawPasswordResetToken,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset requested. UserPublicId: {UserPublicId}.",
            user.PublicId);

        return CreateRequestPasswordResetResponse();
    }

    public async Task<ResetPasswordResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new BadRequestException(InvalidPasswordResetTokenMessage);

        var token = request.Token.Trim();
        var tokenHash = _tokenHasher.Hash(token);
        var passwordResetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(tokenHash, ct);

        if (passwordResetToken is null)
            throw new BadRequestException(InvalidPasswordResetTokenMessage);

        var now = DateTimeOffset.UtcNow;

        // Same generic error regardless of used/expired — never tell the caller which reason
        // applied, so a stolen/guessed token can't be used to probe token state.
        if (passwordResetToken.IsUsed || passwordResetToken.IsExpired(now))
            throw new BadRequestException(InvalidPasswordResetTokenMessage);

        CheckPassword(request.NewPassword);

        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
        passwordResetToken.User.SetPassword(newPasswordHash, now);
        passwordResetToken.MarkAsUsed(now);

        // Whoever changed the password may be doing so because they suspect a compromise —
        // revoke every other active session so a possible attacker is signed out too.
        var activeSessions = await _userSessionRepository.GetActiveByUserIdAsync(passwordResetToken.User.Id, now, ct);
        foreach (var session in activeSessions)
        {
            session.Revoke(now);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Password reset completed. UserPublicId: {UserPublicId}.",
            passwordResetToken.User.PublicId);

        return new ResetPasswordResponseDto
        {
            Message = PasswordResetSuccessMessage
        };
    }

    private static RequestPasswordResetResponseDto CreateRequestPasswordResetResponse()
    {
        return new RequestPasswordResetResponseDto
        {
            Message = RequestPasswordResetMessage
        };
    }

    private void CheckPassword(string password)
    {
        var hasNumber = password.Any(char.IsDigit);
        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasSpecialCharacter = password.Any(character => !char.IsLetterOrDigit(character));
        var hasLength = password.Length >= 8;

        if (!hasNumber || !hasUpper || !hasLower || !hasSpecialCharacter || !hasLength)
            throw new BadRequestException("Password must be at least 8 characters and contain uppercase, lowercase, number, and special character.");

    }

    private static void CheckName(string name, string fieldName)
    {
        if (name.Length < 2)
            throw new BadRequestException($"{fieldName} must be at least 2 characters.");

        if (!name.Any(char.IsLetter))
            throw new BadRequestException($"{fieldName} must contain at least one letter.");

        if (name.Any(character => !char.IsLetter(character) &&
                                  character != ' ' &&
                                  character != '-' &&
                                  character != '\''))
            throw new BadRequestException($"{fieldName} contains invalid characters.");
    }

    private static ResendEmailVerificationResponseDto CreateResendEmailVerificationResponse()
    {
        return new ResendEmailVerificationResponseDto
        {
            Message = ResendEmailVerificationMessage
        };
    }
}
