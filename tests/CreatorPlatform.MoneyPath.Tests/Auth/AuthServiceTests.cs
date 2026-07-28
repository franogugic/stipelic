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
        FakeTokenGenerator TokenGenerator) BuildService()
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
            new FakeEmailVerificationTokenRepository(),
            passwordResetTokenRepository,
            new FakeUnitOfWork(),
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
}
