using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Services;
using CreatorPlatform.Auth.Domain.Tokens;
using CreatorPlatform.Auth.Domain.Users;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.MoneyPath.Tests.Auth;

public class EmailChangeRequestTests
{
    private const string Password = "OldPassword1!";

    private sealed record Fixture(
        EmailChangeService Service,
        FakeUserRepository Users,
        FakeEmailChangeTokenRepository Tokens,
        FakeEmailOutboxService Outbox,
        FakeUnitOfWork UnitOfWork,
        User User,
        CurrentUserDto CurrentUser);

    private static Fixture Build()
    {
        var users = new FakeUserRepository();
        var user = User.Create("owner@example.com", $"hashed:{Password}", "Ana", "Kovač", DateTimeOffset.UtcNow);
        users.UsersByEmail[user.Email] = user;
        users.UsersByEmail["taken@example.com"] = User.Create("taken@example.com", "hashed:x", "Other", "User", DateTimeOffset.UtcNow);

        var tokens = new FakeEmailChangeTokenRepository();
        var outbox = new FakeEmailOutboxService();
        var unitOfWork = new FakeUnitOfWork();
        var service = new EmailChangeService(
            users,
            new FakePasswordHasher(),
            new FakeTokenGenerator { NextToken = "raw-change-token" },
            new FakeTokenHasher(),
            tokens,
            new FakePasswordResetTokenRepository(),
            new FakeUserSessionRepository(),
            outbox,
            unitOfWork,
            NullLogger<EmailChangeService>.Instance);

        return new Fixture(service, users, tokens, outbox, unitOfWork, user, new CurrentUserDto { Id = user.Id, Email = user.Email });
    }

    private static RequestEmailChangeRequestDto Request(string newEmail, string password = Password)
        => new() { NewEmail = newEmail, CurrentPassword = password };

    [Fact]
    public async Task WrongPassword_Returns400AndCreatesNothing()
    {
        var f = Build();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => f.Service.RequestAsync(f.CurrentUser, Request("new@example.com", "WrongPassword1!"), CancellationToken.None));

        Assert.Equal("Current password is incorrect.", exception.Message);
        Assert.Empty(f.Tokens.Tokens);
        Assert.Empty(f.Outbox.QueuedEmailChangeVerifications);
        Assert.Equal(0, f.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task WrongPassword_IsCheckedBeforeTheEmailFormat()
    {
        var f = Build();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => f.Service.RequestAsync(f.CurrentUser, Request("not-an-email", "WrongPassword1!"), CancellationToken.None));

        Assert.Equal("Current password is incorrect.", exception.Message);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Ana <ana@example.com>")]
    [InlineData("   ")]
    public async Task InvalidEmailFormat_Returns400(string newEmail)
    {
        var f = Build();

        await Assert.ThrowsAsync<BadRequestException>(
            () => f.Service.RequestAsync(f.CurrentUser, Request(newEmail), CancellationToken.None));

        Assert.Empty(f.Tokens.Tokens);
    }

    [Fact]
    public async Task SameAsCurrentEmail_Returns400()
    {
        var f = Build();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => f.Service.RequestAsync(f.CurrentUser, Request("  OWNER@example.com "), CancellationToken.None));

        Assert.Equal("This is already your email address.", exception.Message);
    }

    [Fact]
    public async Task TakenEmail_Returns202WithTheSameMessageAndCreatesNoTokenOrMail()
    {
        var f = Build();
        var sent = await Build().Service.RequestAsync(f.CurrentUser, Request("fresh@example.com"), CancellationToken.None);

        var taken = await f.Service.RequestAsync(f.CurrentUser, Request("Taken@Example.com"), CancellationToken.None);

        Assert.Equal(sent.Message, taken.Message);
        Assert.Empty(f.Tokens.Tokens);
        Assert.Empty(f.Outbox.QueuedEmailChangeVerifications);
        Assert.Equal(0, f.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ValidRequest_Creates24HourTokenAndMailsTheNewAddress()
    {
        var f = Build();

        await f.Service.RequestAsync(f.CurrentUser, Request("  New@Example.com "), CancellationToken.None);

        var token = Assert.Single(f.Tokens.Tokens);
        Assert.Equal("new@example.com", token.NewEmail);
        Assert.Equal("hash:raw-change-token", token.TokenHash);
        Assert.InRange(token.ExpiresAt - token.CreatedAt, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        var mail = Assert.Single(f.Outbox.QueuedEmailChangeVerifications);
        Assert.Equal(("new@example.com", "raw-change-token"), (mail.ToEmail, mail.Token));
        Assert.Equal("owner@example.com", f.User.Email); // nothing changes until the link is confirmed
        Assert.Equal(1, f.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task NewRequest_InvalidatesThePreviousUnusedToken()
    {
        var f = Build();
        var previous = EmailChangeToken.Create(f.User, "first@example.com", "hash:old", DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow);
        f.Tokens.Tokens.Add(previous);

        await f.Service.RequestAsync(f.CurrentUser, Request("second@example.com"), CancellationToken.None);

        Assert.True(previous.IsUsed);
        Assert.Single(f.Tokens.Tokens, t => !t.IsUsed);
    }
}
