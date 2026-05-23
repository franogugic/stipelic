using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Services;

public sealed class EmailOutboxService : IEmailOutboxService
{
    private readonly CreatorPlatformDbContext _context;
    private readonly EmailOptions _options;

    public EmailOutboxService(CreatorPlatformDbContext context, IOptions<EmailOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task QueueEmailVerificationAsync(string toEmail, string userPublicId, string token, CancellationToken ct)
    {
        var verificationUrl = BuildVerificationUrl(token);

        var message = EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.EmailVerification,
            userPublicId,
            toEmail,
            EmailVerificationTemplate.Subject,
            EmailVerificationTemplate.BuildHtml(verificationUrl),
            EmailVerificationTemplate.BuildPlainText(verificationUrl),
            DateTimeOffset.UtcNow);

        await _context.Set<EmailOutboxMessage>().AddAsync(message, ct);
    }

    public async Task CancelUnsentEmailVerificationMessagesAsync(string userPublicId, CancellationToken ct)
    {
        var messages = await _context.Set<EmailOutboxMessage>()
            .Where(message =>
                message.Purpose == EmailOutboxMessagePurpose.EmailVerification &&
                message.CorrelationKey == userPublicId &&
                (message.Status == EmailOutboxMessageStatus.Pending ||
                 message.Status == EmailOutboxMessageStatus.Processing))
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            message.Cancel();
        }
    }

    private string BuildVerificationUrl(string token)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);

        return $"{baseUrl}/verify-email?token={encodedToken}";
    }
}
