using CreatorPlatform.Email.Application.Exceptions;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Email;

/// <summary>DEV-1: the SMTP sender against a real Mailpit — the message arrives as the ACS sender builds it.</summary>
public sealed class SmtpEmailSenderTests : IClassFixture<MailpitFixture>
{
    private readonly MailpitFixture _mailpit;

    public SmtpEmailSenderTests(MailpitFixture mailpit)
    {
        _mailpit = mailpit;
    }

    private SmtpEmailSender Sender(string? host = null, int? port = null) => new(Options.Create(new EmailOptions
    {
        Provider = EmailProvider.Smtp,
        FromAddress = "no-reply@luma.test",
        Smtp = new EmailSmtpOptions { Host = host ?? _mailpit.Host, Port = port ?? _mailpit.SmtpHostPort },
    }));

    private static string UniqueRecipient() => $"to-{Guid.NewGuid():N}@example.test";

    [Fact]
    public async Task ACampaignEmail_ArrivesWithBothParts_ReplyTo_AndTheOneClickUnsubscribeHeaders()
    {
        var to = UniqueRecipient();

        await Sender().SendAsync(
            to, "Novosti za listopad — č ć đ š ž", "<p>Bok, <strong>Ana</strong>!</p>", "Bok, Ana!",
            "hello@acme.test", "https://api.luma.test/api/public/unsubscribe/token-1", CancellationToken.None);

        var (message, headers, raw) = await _mailpit.GetSingleMessageToAsync(to);
        Assert.Equal("no-reply@luma.test", message.GetProperty("From").GetProperty("Address").GetString());
        Assert.Equal(to, message.GetProperty("To")[0].GetProperty("Address").GetString());
        Assert.Equal("hello@acme.test", message.GetProperty("ReplyTo")[0].GetProperty("Address").GetString());
        Assert.Equal("Novosti za listopad — č ć đ š ž", message.GetProperty("Subject").GetString());
        Assert.Contains("<strong>Ana</strong>", message.GetProperty("HTML").GetString());
        Assert.Equal("Bok, Ana!", message.GetProperty("Text").GetString()!.Trim());
        Assert.Contains("multipart/alternative", raw);
        Assert.Equal(["<https://api.luma.test/api/public/unsubscribe/token-1>"], headers["List-Unsubscribe"]);
        Assert.Equal(["List-Unsubscribe=One-Click"], headers["List-Unsubscribe-Post"]);
    }

    [Fact]
    public async Task ATransactionalEmail_HasNoReplyTo_AndNoUnsubscribeHeaders()
    {
        var to = UniqueRecipient();

        await Sender().SendAsync(to, "Your purchase", "<p>Thanks</p>", "Thanks", null, null, CancellationToken.None);

        var (message, headers, _) = await _mailpit.GetSingleMessageToAsync(to);
        Assert.Empty(message.GetProperty("ReplyTo").EnumerateArray());
        Assert.False(headers.ContainsKey("List-Unsubscribe"));
        Assert.False(headers.ContainsKey("List-Unsubscribe-Post"));
    }

    [Fact]
    public async Task AnUnreachableServer_IsTransient_SoTheOutboxTriesAgainWithoutSpendingARetry()
    {
        var error = await Assert.ThrowsAsync<TransientEmailSendException>(() =>
            Sender("127.0.0.1", ClosedPort.Find()).SendAsync(UniqueRecipient(), "s", "<p>h</p>", "t", null, null, CancellationToken.None));

        Assert.Equal(SmtpEmailSender.RetryAfterUnavailable, error.RetryAfter);
    }
}
