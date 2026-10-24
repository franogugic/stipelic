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

    // The next campaign queue position per creator within this scope (one request or one scheduled dispatch): looked up
    // once per creator, then counted on, because the rows queued so far are not flushed yet.
    private readonly Dictionary<int, long> _nextQueuePosition = [];

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

    public async Task QueueLeadMagnetAsync(string toEmail, string correlationKey, LeadMagnetEmail lead, CancellationToken ct)
    {
        var email = LeadMagnetTemplate.Render(lead);
        var replyTo = string.IsNullOrWhiteSpace(lead.SupportEmail) ? null : lead.SupportEmail;
        await AddAsync(EmailOutboxMessagePurpose.LeadMagnetDelivery, correlationKey, toEmail, email, ct, replyTo);
    }

    public async Task QueuePayoutRequestedAsync(
        string toEmail, string payoutPublicId, PayoutRequestedEmail payout, CancellationToken ct)
    {
        var email = PayoutRequestedTemplate.Render(Branding, payout, $"{FrontendBaseUrl}/admin/payouts");
        await AddAsync(EmailOutboxMessagePurpose.PayoutRequested, payoutPublicId, toEmail, email, ct);
    }

    public async Task QueueCampaignAsync(
        int creatorId,
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string listUnsubscribeUrl,
        string correlationKey,
        CancellationToken ct)
    {
        if (!_nextQueuePosition.TryGetValue(creatorId, out var position))
            position = await GetCampaignQueueStartAsync(creatorId, ct);
        _nextQueuePosition[creatorId] = position + 1;

        var message = EmailOutboxMessage.Create(
            EmailOutboxMessagePurpose.CampaignBroadcast,
            correlationKey,
            toEmail,
            subject,
            htmlBody,
            plainTextBody,
            DateTimeOffset.UtcNow,
            replyTo,
            listUnsubscribeUrl,
            creatorId,
            position);

        await _context.Set<EmailOutboxMessage>().AddAsync(message, ct);
    }

    /// <summary>
    /// Start-time fair queuing: a creator's new campaign mail starts at the head of the campaign queue (the lowest position
    /// still waiting), so it is sent alternately with everyone else's from now on; or right after the creator's own mail
    /// still waiting, if that ends later, so one creator never holds more than one share. Both lookups use the partial
    /// indexes IX_email_outbox_messages_CampaignQueue and _CreatorCampaignQueue (same filter).
    /// </summary>
    private async Task<long> GetCampaignQueueStartAsync(int creatorId, CancellationToken ct) =>
        await _context.Database.SqlQuery<long>($"""
            SELECT GREATEST(
                COALESCE((SELECT MIN("QueuePosition") FROM email.email_outbox_messages
                          WHERE "Purpose" = 'CampaignBroadcast' AND "Status" IN ('Pending', 'Processing')), 0),
                COALESCE((SELECT MAX("QueuePosition") + 1 FROM email.email_outbox_messages
                          WHERE "Purpose" = 'CampaignBroadcast' AND "Status" IN ('Pending', 'Processing')
                            AND "CreatorId" = {creatorId}), 0)) AS "Value"
            """).SingleAsync(ct);

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
