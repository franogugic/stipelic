using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Domain.Roles;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Auth.Domain.Users;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Auth.Application.Services;

public sealed class AuthService : IAuthService
{
    private const string InvalidEmailVerificationTokenMessage = "Invalid or expired email verification token.";
    private const string EmailVerifiedSuccessfullyMessage = "Email verified successfully.";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITokenHasher _tokenHasher;
    private readonly IEmailVerificationTokenRepository _emailVerificationTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IEmailOutboxService _emailOutboxService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator,
        ITokenHasher tokenHasher,
        IEmailVerificationTokenRepository emailVerificationTokenRepository,
        IUnitOfWork unitOfWork,
        IUserRoleRepository userRoleRepository,
        IEmailOutboxService emailOutboxService
        )
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _tokenHasher = tokenHasher;
        _emailVerificationTokenRepository = emailVerificationTokenRepository;
        _unitOfWork = unitOfWork;
        _userRoleRepository = userRoleRepository;
        _emailOutboxService = emailOutboxService;
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
        await _emailOutboxService.QueueEmailVerificationAsync(user.Email, rawEmailVerificationToken, ct);
        
        await _unitOfWork.SaveChangesAsync(ct);
        
        return new RegisterUserResponseDto
        {
            PublicId = user.PublicId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email
        };
    }

    public async Task<VerifyEmailResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new BadRequestException(InvalidEmailVerificationTokenMessage);

        var token = request.Token.Trim();

        var tokenHash = _tokenHasher.Hash(token);
        var emailVerificationToken = await _emailVerificationTokenRepository.GetByTokenHashAsync(tokenHash, ct);

        var now = DateTimeOffset.UtcNow;
        if (emailVerificationToken is null)
        {
            throw new BadRequestException(InvalidEmailVerificationTokenMessage);
        }

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
    }
}
