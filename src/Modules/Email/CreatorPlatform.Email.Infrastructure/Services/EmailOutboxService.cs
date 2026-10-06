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

    public async Task QueueEmailVerificationAsync(
        string toEmail, string? firstName, string userPublicId, string token, CancellationToken ct)
    {
        var email = EmailVerificationTemplate.Render(Branding, firstName, toEmail, BuildVerificationUrl(token));
        await AddAsync(EmailOutboxMessagePurpose.EmailVerification, userPublicId, toEmail, email, ct);
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

    public async Task QueuePasswordResetAsync(
        string toEmail, string? firstName, string userPublicId, string token, CancellationToken ct)
    {
        var email = PasswordResetTemplate.Render(Branding, firstName, toEmail, BuildPasswordResetUrl(token));
        await AddAsync(EmailOutboxMessagePurpose.PasswordReset, userPublicId, toEmail, email, ct);
    }

    public async Task QueueEmailChangeVerificationAsync(
        string toEmail, string? firstName, string currentEmail, string userPublicId, string token, CancellationToken ct)
    {
        var email = EmailChangeVerificationTemplate.Render(Branding, firstName, currentEmail, toEmail, BuildEmailChangeUrl(token));
        await AddAsync(EmailOutboxMessagePurpose.EmailChangeVerification, userPublicId, toEmail, email, ct);
    }

    public async Task QueueEmailChangedNotificationAsync(
        string toEmail, string? firstName, string userPublicId, string newEmail, DateTimeOffset changedAt, CancellationToken ct)
    {
        var email = EmailChangedTemplate.Render(
            Branding, firstName, toEmail, EmailChangedTemplate.MaskEmail(newEmail), changedAt,
            secureAccountUrl: $"{FrontendBaseUrl}/forgot-password");
        await AddAsync(EmailOutboxMessagePurpose.EmailChanged, userPublicId, toEmail, email, ct);
    }

    public async Task QueueOrderAccessAsync(string toEmail, string orderPublicId, OrderAccessEmail order, CancellationToken ct)
    {
        var email = OrderAccessTemplate.Render(order);
        var replyTo = string.IsNullOrWhiteSpace(order.SupportEmail) ? null : order.SupportEmail;
        await AddAsync(EmailOutboxMessagePurpose.OrderAccess, orderPublicId, toEmail, email, ct, replyTo);
    }

    public async Task QueuePayoutRequestedAsync(
        string toEmail, string payoutPublicId, PayoutRequestedEmail payout, CancellationToken ct)
    {
        var email = PayoutRequestedTemplate.Render(Branding, payout, $"{FrontendBaseUrl}/admin/payouts");
        await AddAsync(EmailOutboxMessagePurpose.PayoutRequested, payoutPublicId, toEmail, email, ct);
    }

    public async Task QueueCampaignAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string listUnsubscribeUrl,
        string correlationKey,
        CancellationToken ct)
    {
        var message = EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.CampaignBroadcast,
            correlationKey,
            toEmail,
            subject,
            htmlBody,
            plainTextBody,
            DateTimeOffset.UtcNow,
            replyTo,
            listUnsubscribeUrl);

        await _context.Set<EmailOutboxMessage>().AddAsync(message, ct);
    }

    private EmailBranding Branding => new(_options.LogoUrl, _options.HelpUrl, _options.PrivacyUrl, _options.SupportEmail);

    private string FrontendBaseUrl => _options.FrontendBaseUrl.TrimEnd('/');

    private async Task AddAsync(
        EmailOutboxMessagePurpose purpose, string correlationKey, string toEmail, RenderedEmail email, CancellationToken ct,
        string? replyTo = null)
    {
        var message = EmailOutboxMessage.Create(
            purpose, correlationKey, toEmail, email.Subject, email.Html, email.PlainText, DateTimeOffset.UtcNow, replyTo);

        await _context.Set<EmailOutboxMessage>().AddAsync(message, ct);
    }

    private string BuildVerificationUrl(string token)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);

        return $"{baseUrl}/verify-email?token={encodedToken}";
    }

    private string BuildEmailChangeUrl(string token)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);

        return $"{baseUrl}/confirm-email-change?token={encodedToken}";
    }

    private string BuildPasswordResetUrl(string token)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);

        return $"{baseUrl}/reset-password?token={encodedToken}";
    }
}
