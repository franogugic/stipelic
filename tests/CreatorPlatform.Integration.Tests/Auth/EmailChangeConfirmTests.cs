using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Application.Services;
using CreatorPlatform.Auth.Infrastructure.Persistence;
using CreatorPlatform.Auth.Infrastructure.Repositories;
using CreatorPlatform.Auth.Infrastructure.Security;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Auth;

/// <summary>The email change security path end to end on real SQL: request (real BCrypt) → confirm.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailChangeConfirmTests
{
    private const string Password = "CorrectHorse1!";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public EmailChangeConfirmTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private sealed class FixedTokenGenerator(string token) : ITokenGenerator
    {
        public string GenerateToken() => token;
    }

    private async Task<T> WithServiceAsync<T>(string rawToken, Func<EmailChangeService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new EmailChangeService(
            new UserRepository(db),
            new BCryptPasswordHasher(),
            new FixedTokenGenerator(rawToken),
            new Sha256TokenHasher(),
            new EmailChangeTokenRepository(db),
            new PasswordResetTokenRepository(db),
            new UserSessionRepository(db),
            new EmailOutboxService(db, Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.luma.test" })),
            new UnitOfWork(db),
            NullLogger<EmailChangeService>.Instance);
        return await act(service);
    }

    private static CurrentUserDto Signed(TestData.SeededUser user, Guid sessionId) =>
        new() { Id = user.Id, Email = user.Email, SessionId = sessionId };

    /// <summary>Requests a change for <paramref name="user"/> and returns the raw link token.</summary>
    private async Task<string> RequestAsync(TestData.SeededUser user, string newEmail)
    {
        var rawToken = $"tok-{Guid.NewGuid():N}";
        await WithServiceAsync(rawToken, s => s.RequestAsync(
            Signed(user, Guid.Empty), new RequestEmailChangeRequestDto { NewEmail = newEmail, CurrentPassword = Password },
            CancellationToken.None));
        return rawToken;
    }

    private Task<ConfirmEmailChangeResponseDto> ConfirmAsync(string rawToken, CurrentUserDto? currentUser = null) =>
        WithServiceAsync("unused", s => s.ConfirmAsync(
            new ConfirmEmailChangeRequestDto { Token = rawToken }, currentUser, CancellationToken.None));

    [Fact]
    public async Task Confirm_ChangesTheEmailMarksItVerifiedAndNotifiesTheOldAddress()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var newEmail = TestData.UniqueEmail("new");
        var token = await RequestAsync(user, newEmail);
        Assert.Single(await _data.GetOutboxSubjectsAsync(newEmail, "EmailChangeVerification"));

        var response = await ConfirmAsync(token);

        Assert.Equal(newEmail, response.Email);
        var row = await _data.GetUserEmailAsync(user.Id);
        Assert.Equal(newEmail, row.Email);
        Assert.NotNull(row.EmailVerifiedAt);
        Assert.InRange(row.EmailVerifiedAt!.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(["Your email was changed"], await _data.GetOutboxSubjectsAsync(user.Email, "EmailChanged"));
    }

    [Fact]
    public async Task Confirm_RevokesEveryOtherSessionAndKeepsTheCurrentOne()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var current = await _data.CreateSessionAsync(user.Id);
        var otherA = await _data.CreateSessionAsync(user.Id);
        var otherB = await _data.CreateSessionAsync(user.Id);
        var token = await RequestAsync(user, TestData.UniqueEmail("new"));

        await ConfirmAsync(token, Signed(user, current));

        Assert.False(await _data.IsSessionRevokedAsync(current));
        Assert.True(await _data.IsSessionRevokedAsync(otherA));
        Assert.True(await _data.IsSessionRevokedAsync(otherB));
    }

    [Fact]
    public async Task Confirm_WithoutASession_RevokesAllSessions()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var session = await _data.CreateSessionAsync(user.Id);
        var token = await RequestAsync(user, TestData.UniqueEmail("new"));

        await ConfirmAsync(token, currentUser: null);

        Assert.True(await _data.IsSessionRevokedAsync(session));
    }

    [Fact]
    public async Task Confirm_ExpiredToken_Returns400AndChangesNothing()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var session = await _data.CreateSessionAsync(user.Id);
        var token = await RequestAsync(user, TestData.UniqueEmail("new"));
        await _data.ExpireEmailChangeTokensAsync(user.Id);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => ConfirmAsync(token));

        Assert.Equal("Invalid or expired email change link.", exception.Message);
        Assert.Equal(user.Email, (await _data.GetUserEmailAsync(user.Id)).Email);
        Assert.False(await _data.IsSessionRevokedAsync(session));
        Assert.Empty(await _data.GetOutboxSubjectsAsync(user.Email, "EmailChanged"));
    }

    [Fact]
    public async Task Confirm_Twice_TheSecondIsRejectedAndNothingRunsAgain()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var newEmail = TestData.UniqueEmail("new");
        var token = await RequestAsync(user, newEmail);
        await ConfirmAsync(token);

        await Assert.ThrowsAsync<BadRequestException>(() => ConfirmAsync(token));

        Assert.Equal(newEmail, (await _data.GetUserEmailAsync(user.Id)).Email);
        Assert.Single(await _data.GetOutboxSubjectsAsync(user.Email, "EmailChanged"));
    }

    [Fact]
    public async Task Confirm_TwiceAtTheSameTime_AppliesOnlyOnce()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var token = await RequestAsync(user, TestData.UniqueEmail("new"));

        var results = await Task.WhenAll(Attempt(), Attempt());

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Single(await _data.GetOutboxSubjectsAsync(user.Email, "EmailChanged"));

        async Task<bool> Attempt()
        {
            try { await ConfirmAsync(token); return true; }
            catch (BadRequestException) { return false; }
        }
    }

    [Fact]
    public async Task Request_TakenAddress_Returns202ButCreatesNoLink()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var other = await _data.CreateUserWithPasswordAsync(Password);

        var response = await WithServiceAsync("tok-taken", s => s.RequestAsync(
            Signed(user, Guid.Empty), new RequestEmailChangeRequestDto { NewEmail = other.Email, CurrentPassword = Password },
            CancellationToken.None));

        Assert.False(string.IsNullOrWhiteSpace(response.Message));
        Assert.Empty(await _data.GetOutboxSubjectsAsync(other.Email, "EmailChangeVerification"));
        await Assert.ThrowsAsync<BadRequestException>(() => ConfirmAsync("tok-taken"));
    }

    [Fact]
    public async Task Request_WrongPassword_Returns400()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);

        await Assert.ThrowsAsync<BadRequestException>(() => WithServiceAsync("tok-wrong", s => s.RequestAsync(
            Signed(user, Guid.Empty), new RequestEmailChangeRequestDto { NewEmail = TestData.UniqueEmail("new"), CurrentPassword = "Nope1234!" },
            CancellationToken.None)));
    }

    [Fact]
    public async Task Confirm_AddressTakenAfterTheRequest_Returns409AndChangesNothing()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var other = await _data.CreateUserWithPasswordAsync(Password);
        var contested = TestData.UniqueEmail("contested");
        var token = await RequestAsync(user, contested);
        var otherToken = await RequestAsync(other, contested);
        await ConfirmAsync(otherToken); // the other account takes the address first

        var exception = await Assert.ThrowsAsync<ConflictException>(() => ConfirmAsync(token));

        Assert.Equal("EMAIL_IN_USE", exception.Code);
        Assert.Equal(user.Email, (await _data.GetUserEmailAsync(user.Id)).Email);
    }

    [Fact]
    public async Task Confirm_TwoAccountsRacingForTheSameAddress_ExactlyOneWins()
    {
        var first = await _data.CreateUserWithPasswordAsync(Password);
        var second = await _data.CreateUserWithPasswordAsync(Password);
        var contested = TestData.UniqueEmail("race");
        var firstToken = await RequestAsync(first, contested);
        var secondToken = await RequestAsync(second, contested);

        var outcomes = await Task.WhenAll(Attempt(firstToken), Attempt(secondToken));

        Assert.Single(outcomes, o => o == "ok");
        Assert.Single(outcomes, o => o == "EMAIL_IN_USE");
        var emails = new[] { (await _data.GetUserEmailAsync(first.Id)).Email, (await _data.GetUserEmailAsync(second.Id)).Email };
        Assert.Single(emails, e => e == contested);

        async Task<string> Attempt(string token)
        {
            try { await ConfirmAsync(token); return "ok"; }
            catch (ConflictException e) { return e.Code; }
        }
    }
}
