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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Auth;

/// <summary>A pending email change can be read, sent again and cancelled — and every one of those leaves exactly
/// one working link (or none).</summary>
[Collection(PostgresCollection.Name)]
public sealed class PendingEmailChangeTests
{
    private const string Password = "CorrectHorse1!";
    private const string VerificationPurpose = "EmailChangeVerification";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public PendingEmailChangeTests(PostgresFixture fixture)
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

    private static CurrentUserDto Signed(TestData.SeededUser user) => new() { Id = user.Id, Email = user.Email };

    private static string NewToken() => $"tok-{Guid.NewGuid():N}";

    /// <summary>Requests a change and returns the raw link token.</summary>
    private async Task<string> RequestAsync(TestData.SeededUser user, string newEmail)
    {
        var rawToken = NewToken();
        await WithServiceAsync(rawToken, s => s.RequestAsync(
            Signed(user), new RequestEmailChangeRequestDto { NewEmail = newEmail, CurrentPassword = Password },
            CancellationToken.None));
        return rawToken;
    }

    /// <summary>Resends and returns the raw token the fresh link would carry.</summary>
    private async Task<string> ResendAsync(TestData.SeededUser user)
    {
        var rawToken = NewToken();
        await WithServiceAsync(rawToken, s => s.ResendAsync(Signed(user), CancellationToken.None));
        return rawToken;
    }

    private Task<PendingEmailChangeDto?> GetPendingAsync(TestData.SeededUser user) =>
        WithServiceAsync("unused", s => s.GetPendingAsync(Signed(user), CancellationToken.None));

    private Task CancelAsync(TestData.SeededUser user) =>
        WithServiceAsync("unused", async s =>
        {
            await s.CancelAsync(Signed(user), CancellationToken.None);
            return true;
        });

    private Task<ConfirmEmailChangeResponseDto> ConfirmAsync(string rawToken) =>
        WithServiceAsync("unused", s => s.ConfirmAsync(
            new ConfirmEmailChangeRequestDto { Token = rawToken }, null, CancellationToken.None));

    // ---- GET -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsOnlyTheNewestUnusedUnexpiredChange()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        Assert.Null(await GetPendingAsync(user));

        await RequestAsync(user, TestData.UniqueEmail("first"));
        var second = TestData.UniqueEmail("second");
        await RequestAsync(user, second);

        var pending = await GetPendingAsync(user);
        Assert.NotNull(pending);
        Assert.Equal(second, pending.NewEmail);
        Assert.InRange(pending.ExpiresAt, DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow.AddHours(25));

        await _data.ExpireEmailChangeTokensAsync(user.Id);
        Assert.Null(await GetPendingAsync(user));
    }

    [Fact]
    public async Task Get_AfterTheChangeWasConfirmed_IsNull()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var token = await RequestAsync(user, TestData.UniqueEmail("new"));

        await ConfirmAsync(token);

        Assert.Null(await GetPendingAsync(user));
    }

    // ---- Resend ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resend_InvalidatesTheOldLink_AndTheNewOneWorks()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var newEmail = TestData.UniqueEmail("new");
        var oldToken = await RequestAsync(user, newEmail);

        var freshToken = await ResendAsync(user);

        Assert.Equal(2, (await _data.GetOutboxSubjectsAsync(newEmail, VerificationPurpose)).Count);
        Assert.Equal(newEmail, (await GetPendingAsync(user))?.NewEmail);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => ConfirmAsync(oldToken));
        Assert.Equal("Invalid or expired email change link.", exception.Message);

        Assert.Equal(newEmail, (await ConfirmAsync(freshToken)).Email);
        Assert.Equal(newEmail, (await _data.GetUserEmailAsync(user.Id)).Email);
    }

    [Fact]
    public async Task Resend_WhenTheAddressWasTakenMeanwhile_SendsNothing_AndLeavesNoWorkingLink()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var newEmail = TestData.UniqueEmail("new");
        var oldToken = await RequestAsync(user, newEmail);
        var other = await _data.CreateUserWithPasswordAsync(Password);
        await SetEmailAsync(other.Id, newEmail);

        // Same answer as a real send: it never reveals that the address now belongs to someone.
        var response = await WithServiceAsync(NewToken(), s => s.ResendAsync(Signed(user), CancellationToken.None));
        Assert.False(string.IsNullOrWhiteSpace(response.Message));

        Assert.Single(await _data.GetOutboxSubjectsAsync(newEmail, VerificationPurpose));
        Assert.Null(await GetPendingAsync(user));
        Assert.Equal(0, await CountUnusedTokensAsync(user.Id));
        await Assert.ThrowsAsync<BadRequestException>(() => ConfirmAsync(oldToken));
        Assert.Equal(user.Email, (await _data.GetUserEmailAsync(user.Id)).Email);
    }

    [Fact]
    public async Task Resend_WithoutAPendingChange_Is404()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        await Assert.ThrowsAsync<NotFoundException>(() => ResendAsync(user));

        // An expired change is no longer pending either.
        await RequestAsync(user, TestData.UniqueEmail("new"));
        await _data.ExpireEmailChangeTokensAsync(user.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => ResendAsync(user));
    }

    // ---- Cancel ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Cancel_MakesThePendingLinkFail_AndIsIdempotent()
    {
        var user = await _data.CreateUserWithPasswordAsync(Password);
        var token = await RequestAsync(user, TestData.UniqueEmail("new"));

        await CancelAsync(user);
        await CancelAsync(user);

        Assert.Null(await GetPendingAsync(user));
        await Assert.ThrowsAsync<BadRequestException>(() => ConfirmAsync(token));
        await Assert.ThrowsAsync<NotFoundException>(() => ResendAsync(user));
        Assert.Equal(user.Email, (await _data.GetUserEmailAsync(user.Id)).Email);
    }

    private async Task SetEmailAsync(int userId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""UPDATE auth.users SET "Email" = {email} WHERE "Id" = {userId}""");
    }

    private async Task<int> CountUnusedTokensAsync(int userId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM auth.email_change_tokens WHERE "UserId" = {userId} AND "UsedAt" IS NULL
            """).ToListAsync()).Single();
    }
}
