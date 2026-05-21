using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Application.Templates;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Shared.Infrastructure.Persistence;
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

    public async Task QueueEmailVerificationAsync(string toEmail, string token, CancellationToken ct)
    {
        var verificationUrl = BuildVerificationUrl(token);

        var message = EmailOutboxMessage.Create(
            toEmail,
            EmailVerificationTemplate.Subject,
            EmailVerificationTemplate.BuildHtml(verificationUrl),
            EmailVerificationTemplate.BuildPlainText(verificationUrl),
            DateTimeOffset.UtcNow);

        await _context.Set<EmailOutboxMessage>().AddAsync(message, ct);
    }

    private string BuildVerificationUrl(string token)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);

        return $"{baseUrl}/verify-email?token={encodedToken}";
    }
}


