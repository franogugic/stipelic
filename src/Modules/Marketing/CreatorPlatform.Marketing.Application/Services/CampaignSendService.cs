using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class CampaignSendService : ICampaignSendService
{
    private const string MonthlyEmailSendsLimitKey = "max_email_sends_per_month";

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICampaignRecipientRepository _recipientRepository;
    private readonly IAudienceService _audienceService;
    private readonly ICreatorUsageService _usageService;
    private readonly ICampaignEmailRenderer _renderer;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly IUnsubscribeTokenService _unsubscribeTokenService;
    private readonly ICampaignProgressProvider _progressProvider;
    private readonly IMarketingUnitOfWork _unitOfWork;

    public CampaignSendService(
        ICreatorContextProvider creatorContextProvider,
        ICampaignRepository campaignRepository,
        ICampaignRecipientRepository recipientRepository,
        IAudienceService audienceService,
        ICreatorUsageService usageService,
        ICampaignEmailRenderer renderer,
        IEmailOutboxService emailOutboxService,
        IUnsubscribeTokenService unsubscribeTokenService,
        ICampaignProgressProvider progressProvider,
        IMarketingUnitOfWork unitOfWork)
    {
        _creatorContextProvider = creatorContextProvider;
        _campaignRepository = campaignRepository;
        _recipientRepository = recipientRepository;
        _audienceService = audienceService;
        _usageService = usageService;
        _renderer = renderer;
        _emailOutboxService = emailOutboxService;
        _unsubscribeTokenService = unsubscribeTokenService;
        _progressProvider = progressProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<CampaignDetailDto> SendAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        Campaign? campaign = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Lock first, then re-fetch + check status — this is what actually makes a duplicate send
            // impossible under concurrency; the ownership/Draft check alone (without the lock already
            // held) would still race between two concurrent send requests.
            await _unitOfWork.AcquireCreatorCampaignLockAsync(context.CreatorId, ct);

            campaign = await _campaignRepository.GetByPublicIdForUpdateAsync(campaignPublicId, ct);
            if (campaign is null || campaign.CreatorId != context.CreatorId)
                throw new NotFoundException("Campaign not found.");

            if (campaign.Status != CampaignStatus.Draft)
                throw new ConflictException($"Cannot send a {campaign.Status} campaign — only Draft campaigns can be sent.");

            // Deduped/suppressed at the SQL layer already (see AudienceService), but the CampaignRecipient
            // unique index is (CampaignId, Email) — deduping again here is cheap insurance against ever
            // inserting the same recipient twice for one send, regardless of the audience source.
            var emails = (await _audienceService.GetAudienceEmailsAsync(
                campaign.AudienceType, campaign.LandingPageId, campaign.ProductId, context.CreatorId, ct))
                .Distinct()
                .ToList();

            if (emails.Count == 0)
                throw new BadRequestException("No recipients.");

            var monthlyLimit = await _creatorContextProvider.GetActivePlanLimitAsync(context.CreatorId, MonthlyEmailSendsLimitKey, ct);
            if (monthlyLimit is null)
                throw new ConflictException("Sending is temporarily unavailable.");

            var consumed = await _usageService.TryConsumeAsync(
                context.CreatorId, MonthlyEmailSendsLimitKey, emails.Count, monthlyLimit.Value, UsagePeriod.CalendarMonth, ct);
            if (!consumed)
            {
                var usedThisMonth = await _usageService.GetUsedAsync(context.CreatorId, MonthlyEmailSendsLimitKey, UsagePeriod.CalendarMonth, ct);
                var remaining = monthlyLimit.Value < 0 ? int.MaxValue : Math.Max(0, monthlyLimit.Value - usedThisMonth);
                throw new ConflictException(
                    $"Sending this campaign ({emails.Count} recipients) would exceed your monthly email limit. {remaining} send(s) remaining this month.");
            }

            var now = DateTimeOffset.UtcNow;
            var recipients = emails.Select(email => CampaignRecipient.Create(campaign.Id, email, now)).ToList();
            await _recipientRepository.AddRangeAsync(recipients, ct);

            // Flush now so each recipient gets its DB-generated Id — CorrelationKey embeds that id, and
            // it doesn't exist client-side before the row lands. Still one transaction/one advisory lock
            // hold, just two round trips instead of the N a per-recipient save would take.
            await _unitOfWork.SaveChangesAsync(ct);

            campaign.MarkQueued(emails.Count, now);

            var replyTo = context.SupportEmail ?? context.OwnerEmail;
            var rendered = _renderer.Render(
                campaign.Subject,
                campaign.BodyText,
                campaign.CtaLabel,
                campaign.CtaUrl,
                context.BrandName,
                context.LogoUrl,
                context.PrimaryColor);

            foreach (var recipient in recipients)
            {
                var unsubscribeUrl = _unsubscribeTokenService.BuildUnsubscribeUrl(context.CreatorId, recipient.Email);
                var correlationKey = $"{campaign.PublicId}:{recipient.Id}";

                await _emailOutboxService.QueueCampaignAsync(
                    recipient.Email,
                    rendered.Subject,
                    rendered.HtmlBody.Replace("{{UNSUBSCRIBE_URL}}", unsubscribeUrl),
                    rendered.PlainTextBody.Replace("{{UNSUBSCRIBE_URL}}", unsubscribeUrl),
                    replyTo,
                    unsubscribeUrl,
                    correlationKey,
                    ct);
            }

            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        var sent = campaign!;
        var targetPublicId = sent.LandingPageId is not null
            ? (await _creatorContextProvider.GetLandingPagePublicIdsAsync([sent.LandingPageId.Value], ct))[sent.LandingPageId.Value]
            : (await _creatorContextProvider.GetProductPublicIdsAsync([sent.ProductId!.Value], ct))[sent.ProductId.Value];

        var progress = (await _progressProvider.GetProgressAsync([sent.PublicId], ct))[sent.PublicId];

        return new CampaignDetailDto(
            sent.PublicId,
            sent.Subject,
            sent.BodyText,
            sent.CtaLabel,
            sent.CtaUrl,
            sent.AudienceType.ToString(),
            targetPublicId,
            sent.Status.ToString(),
            sent.RecipientCount,
            sent.QueuedAt,
            sent.CreatedAt,
            sent.UpdatedAt,
            progress.SentCount,
            progress.FailedCount);
    }
}
