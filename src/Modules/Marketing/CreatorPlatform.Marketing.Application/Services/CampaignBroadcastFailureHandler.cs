using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Marketing.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>Refunds the creator's monthly email-sends counter when a CampaignBroadcast message
/// permanently fails to deliver — it was consumed at send time (see CampaignSendService) as if
/// successfully queued, but a terminal failure means it never actually reached the recipient.</summary>
public sealed class CampaignBroadcastFailureHandler : IEmailSendFailureHandler
{
    private const string MonthlyEmailSendsLimitKey = "max_email_sends_per_month";
    private const int CampaignPublicIdLength = 36; // Guid.ToString() "D" format is always exactly 36 chars.

    private readonly ICampaignRepository _campaignRepository;
    private readonly ICreatorUsageService _usageService;
    private readonly ILogger<CampaignBroadcastFailureHandler> _logger;

    public CampaignBroadcastFailureHandler(
        ICampaignRepository campaignRepository,
        ICreatorUsageService usageService,
        ILogger<CampaignBroadcastFailureHandler> logger)
    {
        _campaignRepository = campaignRepository;
        _usageService = usageService;
        _logger = logger;
    }

    public EmailOutboxMessagePurpose Purpose => EmailOutboxMessagePurpose.CampaignBroadcast;

    public async Task HandleAsync(EmailOutboxMessage message, CancellationToken ct)
    {
        if (message.CorrelationKey.Length < CampaignPublicIdLength ||
            !Guid.TryParse(message.CorrelationKey[..CampaignPublicIdLength], out var campaignPublicId))
        {
            _logger.LogWarning(
                "CampaignBroadcast failure handler could not parse a campaign id from CorrelationKey {CorrelationKey} on message {MessageId}; skipping refund.",
                message.CorrelationKey, message.Id);
            return;
        }

        var campaign = await _campaignRepository.GetByPublicIdAsync(campaignPublicId, ct);
        if (campaign is null)
        {
            _logger.LogWarning(
                "CampaignBroadcast failure handler could not find campaign {CampaignPublicId} for message {MessageId}; skipping refund.",
                campaignPublicId, message.Id);
            return;
        }

        // Resolve the period from when the send actually happened, not "now" — a fail can land in the
        // following calendar month and must not credit whatever period happens to be current then.
        var asOf = campaign.QueuedAt ?? campaign.CreatedAt;

        await _usageService.RefundAsync(campaign.CreatorId, MonthlyEmailSendsLimitKey, 1, UsagePeriod.CalendarMonth, asOf, ct);
    }
}
