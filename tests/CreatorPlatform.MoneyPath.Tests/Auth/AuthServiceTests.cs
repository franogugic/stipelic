using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Options;
using CreatorPlatform.Auth.Application.Services;
using CreatorPlatform.Auth.Domain.Sessions;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Auth.Domain.Users;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Auth;

public class AuthServiceTests
{
    private static (
        AuthService Service,
        FakeUserRepository UserRepository,
        FakePasswordResetTokenRepository PasswordResetTokenRepository,
        FakeUserSessionRepository UserSessionRepository,
        FakeEmailOutboxService EmailOutboxService,
        FakeTokenGenerator TokenGenerator) BuildService(
        FakeEmailVerificationTokenRepository? emailVerificationTokenRepository = null,
        FakeUnitOfWork? unitOfWork = null)
    {
        var userRepository = new FakeUserRepository();
        var passwordResetTokenRepository = new FakePasswordResetTokenRepository();
        var userSessionRepository = new FakeUserSessionRepository();
        var emailOutboxService = new FakeEmailOutboxService();
        var tokenGenerator = new FakeTokenGenerator();

        var service = new AuthService(
            userRepository,
            new FakePasswordHasher(),
            tokenGenerator,
            new FakeTokenHasher(),
            emailVerificationTokenRepository ?? new FakeEmailVerificationTokenRepository(),
            passwordResetTokenRepository,
            unitOfWork ?? new FakeUnitOfWork(),
            new FakeUserRoleRepository(),
            emailOutboxService,
            NullLogger<AuthService>.Instance,
            userSessionRepository,
            Options.Create(new AuthOptions
            {
                SessionCookieName = "session",
                SessionLifetimeDays = 30,
                SessionCookieSecure = true,
                SessionCookieSameSite = "Lax"
            }));

        return (service, userRepository, passwordResetTokenRepository, userSessionRepository, emailOutboxService, tokenGenerator);
    }

    private static User CreateUser(FakeUserRepository userRepository, string email = "someone@example.com")
    {
        var user = User.Create(email, "hashed:OldPassword1!", "Some", "One", DateTimeOffset.UtcNow);
        userRepository.UsersByEmail[email] = user;
        return user;
    }

    [Fact]
    public async Task RequestPasswordResetAsync_NonexistentEmail_ReturnsIdenticalResponseAndQueuesNoEmail()
    {
        var (service, _, passwordResetTokenRepository, _, emailOutboxService, _) = BuildService();

        var response = await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = "nobody@example.com" },
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.Message));
        Assert.Empty(passwordResetTokenRepository.Tokens);
        Assert.Empty(emailOutboxService.QueuedPasswordResetMessages);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_ExistingEmail_CreatesTokenAndQueuesEmail()
    {
        var (service, userRepository, passwordResetTokenRepository, _, emailOutboxService, _) = BuildService();
        var user = CreateUser(userRepository);

        var response = await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = user.Email },
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.Message));
        Assert.Single(passwordResetTokenRepository.Tokens);
        Assert.Single(emailOutboxService.QueuedPasswordResetMessages);
        Assert.Equal(user.Email, emailOutboxService.QueuedPasswordResetMessages[0].ToEmail);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_NonexistentAndExistingEmail_ReturnIdenticalMessages()
    {
        var (service, userRepository, _, _, _, _) = BuildService();
        var user = CreateUser(userRepository);

        var responseForExisting = await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = user.Email },
            CancellationToken.None);
        var responseForNonexistent = await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = "nobody@example.com" },
            CancellationToken.None);

        Assert.Equal(responseForExisting.Message, responseForNonexistent.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ChangesPasswordMarksTokenUsedAndRevokesActiveSessions()
    {
        var (service, userRepository, _, userSessionRepository, _, tokenGenerator) = BuildService();
        var user = CreateUser(userRepository);

        var activeSession = UserSession.Create(
            user.Id,
            "existing-session-hash",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow);
        userSessionRepository.Sessions.Add(activeSession);

        tokenGenerator.NextToken = "reset-token-1";
        await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = user.Email },
            CancellationToken.None);

        var response = await service.ResetPasswordAsync(
            new ResetPasswordRequestDto { Token = "reset-token-1", NewPassword = "NewPassword1!" },
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.Message));
        Assert.Equal("hashed:NewPassword1!", user.PasswordHash);
        Assert.NotNull(activeSession.RevokedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_NonexistentToken_ThrowsGenericErrorAndLeavesPasswordUnchanged()
    {
        var (service, userRepository, _, _, _, _) = BuildService();
        var user = CreateUser(userRepository);
        var originalPasswordHash = user.PasswordHash;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ResetPasswordAsync(
                new ResetPasswordRequestDto { Token = "does-not-exist", NewPassword = "AnotherPassword1!" },
                CancellationToken.None));

        Assert.Equal("Invalid or expired reset link.", exception.Message);
        Assert.Equal(originalPasswordHash, user.PasswordHash);
    }

    [Fact]
    public async Task ResetPasswordAsync_UsedToken_ThrowsGenericErrorOnSecondAttempt()
    {
        var (service, userRepository, _, _, _, tokenGenerator) = BuildService();
        var user = CreateUser(userRepository);

        tokenGenerator.NextToken = "reset-token-used";
        await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = user.Email },
            CancellationToken.None);
        await service.ResetPasswordAsync(
            new ResetPasswordRequestDto { Token = "reset-token-used", NewPassword = "NewPassword1!" },
            CancellationToken.None);
        var passwordHashAfterFirstReset = user.PasswordHash;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ResetPasswordAsync(
                new ResetPasswordRequestDto { Token = "reset-token-used", NewPassword = "AnotherPassword1!" },
                CancellationToken.None));

        Assert.Equal("Invalid or expired reset link.", exception.Message);
        Assert.Equal(passwordHashAfterFirstReset, user.PasswordHash);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ThrowsGenericErrorAndLeavesPasswordUnchanged()
    {
        var (service, userRepository, passwordResetTokenRepository, _, _, _) = BuildService();
        var user = CreateUser(userRepository);
        var originalPasswordHash = user.PasswordHash;

        var expiredToken = PasswordResetToken.Create(
            user,
            "hash:expired-raw-token",
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(-2));
        passwordResetTokenRepository.Tokens.Add(expiredToken);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ResetPasswordAsync(
                new ResetPasswordRequestDto { Token = "expired-raw-token", NewPassword = "AnotherPassword1!" },
                CancellationToken.None));

        Assert.Equal("Invalid or expired reset link.", exception.Message);
        Assert.Equal(originalPasswordHash, user.PasswordHash);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_SecondRequest_InvalidatesOldToken()
    {
        var (service, userRepository, _, _, _, tokenGenerator) = BuildService();
        var user = CreateUser(userRepository);

        tokenGenerator.NextToken = "first-reset-token";
        await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = user.Email },
            CancellationToken.None);

        tokenGenerator.NextToken = "second-reset-token";
        await service.RequestPasswordResetAsync(
            new RequestPasswordResetRequestDto { Email = user.Email },
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ResetPasswordAsync(
                new ResetPasswordRequestDto { Token = "first-reset-token", NewPassword = "NewPassword1!" },
                CancellationToken.None));
        Assert.Equal("Invalid or expired reset link.", exception.Message);

        var response = await service.ResetPasswordAsync(
            new ResetPasswordRequestDto { Token = "second-reset-token", NewPassword = "NewPassword1!" },
            CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
    }

    private static RegisterUserRequestDto RegisterRequest(bool acceptTerms) => new()
    {
        FirstName = "Ana",
        LastName = "Horvat",
        Email = "ana@example.com",
        Password = "Adriatic20!",
        AcceptTerms = acceptTerms
    };

    [Fact]
    public async Task RegisterAsync_TermsNotAccepted_ThrowsBadRequestAndCreatesNoUser()
    {
        var (service, userRepository, _, _, _, _) = BuildService();

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.RegisterAsync(RegisterRequest(acceptTerms: false), CancellationToken.None));

        Assert.Equal("You must accept the Terms and Privacy Policy.", exception.Message);
        Assert.Empty(userRepository.AddedUsers);
    }

    [Fact]
    public async Task RegisterAsync_TermsAccepted_RecordsTermsAcceptedAt()
    {
        var (service, userRepository, _, _, _, _) = BuildService();
        var before = DateTimeOffset.UtcNow;

        await service.RegisterAsync(RegisterRequest(acceptTerms: true), CancellationToken.None);

        var user = Assert.Single(userRepository.AddedUsers);
        Assert.NotNull(user.TermsAcceptedAt);
        Assert.InRange(user.TermsAcceptedAt!.Value, before, DateTimeOffset.UtcNow);
        Assert.Equal(user.CreatedAt, user.TermsAcceptedAt);
    }

    private static EmailVerificationToken AddVerificationToken(
        FakeEmailVerificationTokenRepository repository,
        User user,
        string rawToken,
        DateTimeOffset expiresAt)
    {
        var token = EmailVerificationToken.Create(user, new FakeTokenHasher().Hash(rawToken), expiresAt, DateTimeOffset.UtcNow.AddHours(-25));
        repository.Tokens.Add(token);
        return token;
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsExpiredWithEmailAndWritesNothing()
    {
        var tokens = new FakeEmailVerificationTokenRepository();
        var unitOfWork = new FakeUnitOfWork();
        var (service, userRepository, _, _, _, _) = BuildService(tokens, unitOfWork);
        var user = CreateUser(userRepository);
        var token = AddVerificationToken(tokens, user, "expired-token", DateTimeOffset.UtcNow.AddMinutes(-1));

        var response = await service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = "expired-token" }, CancellationToken.None);

        Assert.Equal(VerifyEmailOutcome.Expired, response.Outcome);
        Assert.Equal(user.Email, response.Email);
        Assert.Null(response.FirstName);
        Assert.False(user.IsEmailVerified);
        Assert.False(token.IsUsed);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task VerifyEmailAsync_UsedTokenForUnverifiedUser_ReturnsExpired()
    {
        var tokens = new FakeEmailVerificationTokenRepository();
        var (service, userRepository, _, _, _, _) = BuildService(tokens);
        var user = CreateUser(userRepository);
        var token = AddVerificationToken(tokens, user, "used-token", DateTimeOffset.UtcNow.AddHours(1));
        token.MarkAsUsed(DateTimeOffset.UtcNow.AddMinutes(-5));

        var response = await service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = "used-token" }, CancellationToken.None);

        Assert.Equal(VerifyEmailOutcome.Expired, response.Outcome);
        Assert.Equal(user.Email, response.Email);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task VerifyEmailAsync_UnknownToken_ThrowsBadRequest()
    {
        var (service, _, _, _, _, _) = BuildService();

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = "no-such-token" }, CancellationToken.None));
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_VerifiesUserAndReturnsFirstName()
    {
        var tokens = new FakeEmailVerificationTokenRepository();
        var (service, userRepository, _, _, _, _) = BuildService(tokens);
        var user = CreateUser(userRepository);
        var token = AddVerificationToken(tokens, user, "valid-token", DateTimeOffset.UtcNow.AddHours(1));

        var response = await service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = "valid-token" }, CancellationToken.None);

        Assert.Equal(VerifyEmailOutcome.Verified, response.Outcome);
        Assert.Equal(user.FirstName, response.FirstName);
        Assert.Null(response.Email);
        Assert.True(user.IsEmailVerified);
        Assert.True(token.IsUsed);
    }

    private static PasswordResetToken AddResetToken(
        FakePasswordResetTokenRepository repository, User user, string rawToken, DateTimeOffset expiresAt)
    {
        var token = PasswordResetToken.Create(user, new FakeTokenHasher().Hash(rawToken), expiresAt, DateTimeOffset.UtcNow.AddMinutes(-30));
        repository.Tokens.Add(token);
        return token;
    }

    [Fact]
    public async Task InspectPasswordResetTokenAsync_ValidToken_ReturnsValidWithEmailAndWritesNothing()
    {
        var unitOfWork = new FakeUnitOfWork();
        var (service, userRepository, resetTokens, _, _, _) = BuildService(unitOfWork: unitOfWork);
        var user = CreateUser(userRepository);
        var token = AddResetToken(resetTokens, user, "valid-reset-token", DateTimeOffset.UtcNow.AddMinutes(30));
        var originalPasswordHash = user.PasswordHash;

        var response = await service.InspectPasswordResetTokenAsync(
            new InspectPasswordResetTokenRequestDto { Token = "  valid-reset-token  " }, CancellationToken.None);

        Assert.Equal(PasswordResetTokenStatus.Valid, response.Status);
        Assert.Equal(user.Email, response.Email);
        Assert.False(token.IsUsed);
        Assert.Equal(originalPasswordHash, user.PasswordHash);
        Assert.Single(resetTokens.Tokens);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task InspectPasswordResetTokenAsync_ExpiredToken_ReturnsExpiredWithoutEmailAndWritesNothing()
    {
        var unitOfWork = new FakeUnitOfWork();
        var (service, userRepository, resetTokens, _, _, _) = BuildService(unitOfWork: unitOfWork);
        var user = CreateUser(userRepository);
        var token = AddResetToken(resetTokens, user, "expired-reset-token", DateTimeOffset.UtcNow.AddMinutes(-1));

        var response = await service.InspectPasswordResetTokenAsync(
            new InspectPasswordResetTokenRequestDto { Token = "expired-reset-token" }, CancellationToken.None);

        Assert.Equal(PasswordResetTokenStatus.Expired, response.Status);
        Assert.Null(response.Email);
        Assert.False(token.IsUsed);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task InspectPasswordResetTokenAsync_UsedToken_ReturnsExpiredWithoutEmailAndWritesNothing()
    {
        var unitOfWork = new FakeUnitOfWork();
        var (service, userRepository, resetTokens, _, _, _) = BuildService(unitOfWork: unitOfWork);
        var user = CreateUser(userRepository);
        var token = AddResetToken(resetTokens, user, "used-reset-token", DateTimeOffset.UtcNow.AddMinutes(30));
        var usedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        token.MarkAsUsed(usedAt);

        var response = await service.InspectPasswordResetTokenAsync(
            new InspectPasswordResetTokenRequestDto { Token = "used-reset-token" }, CancellationToken.None);

        Assert.Equal(PasswordResetTokenStatus.Expired, response.Status);
        Assert.Null(response.Email);
        Assert.Equal(usedAt, token.UsedAt);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("no-such-token")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task InspectPasswordResetTokenAsync_UnknownOrBlankToken_ThrowsGenericErrorAndWritesNothing(string? rawToken)
    {
        var unitOfWork = new FakeUnitOfWork();
        var (service, userRepository, resetTokens, _, _, _) = BuildService(unitOfWork: unitOfWork);
        var user = CreateUser(userRepository);
        var otherToken = AddResetToken(resetTokens, user, "someone-elses-token", DateTimeOffset.UtcNow.AddMinutes(30));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.InspectPasswordResetTokenAsync(
                new InspectPasswordResetTokenRequestDto { Token = rawToken }, CancellationToken.None));

        Assert.Equal("Invalid or expired reset link.", exception.Message);
        Assert.False(otherToken.IsUsed);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }
}
