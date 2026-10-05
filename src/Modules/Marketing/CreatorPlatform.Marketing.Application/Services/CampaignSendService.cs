using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Mail;
using CreatorPlatform.Marketing.Domain.Templates;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class CampaignSendService : ICampaignSendService
{
    private const string MonthlyEmailSendsLimitKey = "max_email_sends_per_month";
    private const int MinScheduleBufferMinutes = 2;
    private const int MaxScheduleDays = 365;
    private const int MaxNoteLength = 500;

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IEmailTemplateRepository _templateRepository;
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICampaignRecipientRepository _recipientRepository;
    private readonly IAudienceService _audienceService;
    private readonly ICreatorUsageService _usageService;
    private readonly ICampaignEmailRenderer _renderer;
    private readonly IEmailOutboxService _emailOutboxService;
    private readonly IUnsubscribeTokenService _unsubscribeTokenService;
    private readonly IOpenTrackingTokenService _openTrackingTokenService;
    private readonly ICampaignProgressProvider _progressProvider;
    private readonly IMarketingUnitOfWork _unitOfWork;
    private readonly ILogger<CampaignSendService> _logger;

    public CampaignSendService(
        ICreatorContextProvider creatorContextProvider,
        IEmailTemplateRepository templateRepository,
        ICampaignRepository campaignRepository,
        ICampaignRecipientRepository recipientRepository,
        IAudienceService audienceService,
        ICreatorUsageService usageService,
        ICampaignEmailRenderer renderer,
        IEmailOutboxService emailOutboxService,
        IUnsubscribeTokenService unsubscribeTokenService,
        IOpenTrackingTokenService openTrackingTokenService,
        ICampaignProgressProvider progressProvider,
        IMarketingUnitOfWork unitOfWork,
        ILogger<CampaignSendService> logger)
    {
        _creatorContextProvider = creatorContextProvider;
        _templateRepository = templateRepository;
        _campaignRepository = campaignRepository;
        _recipientRepository = recipientRepository;
        _audienceService = audienceService;
        _usageService = usageService;
        _renderer = renderer;
        _emailOutboxService = emailOutboxService;
        _unsubscribeTokenService = unsubscribeTokenService;
        _openTrackingTokenService = openTrackingTokenService;
        _progressProvider = progressProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CampaignDetailDto> SendAsync(string slug, int ownerUserId, SendCampaignRequestDto request, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var audienceType = ParseAudienceType(request.AudienceType);
        var inlineContent = ResolveInlineContent(request);

        if (inlineContent is null && request.TemplatePublicId is null)
            throw new BadRequestException("Either a template or the email content is required.");

        Campaign? campaign = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Lock first — audience resolution + usage-counter consumption must not race across two
            // concurrent sends for the same creator (immediate or schedule-request alike).
            await _unitOfWork.AcquireCreatorCampaignLockAsync(context.CreatorId, ct);

            var template = request.TemplatePublicId is { } templatePublicId
                ? await GetActiveOwnedTemplateAsync(context.CreatorId, templatePublicId, ct)
                : null;

            // Inline content always wins: the send is a snapshot of exactly what the request carried, and
            // a template sent alongside it is only kept as a reference (TemplateId). Without inline
            // content the snapshot comes from the template, as before.
            var content = inlineContent ?? new MailContent(template!.Subject, template.BodyText, template.CtaLabel, template.CtaUrl);

            var (landingPageId, productId) = await ResolveTargetAsync(context.CreatorId, audienceType, request.TargetPublicId, ct);

            var now = DateTimeOffset.UtcNow;

            if (request.ScheduledAt is { } scheduledAt)
            {
                ValidateScheduledAt(scheduledAt, now);

                // Content snapshot only — audience/limit/recipients/outbox are deliberately untouched
                // here, resolved again at dispatch time so a late unsubscribe is always honored.
                campaign = Campaign.CreateScheduled(
                    context.CreatorId,
                    template?.Id,
                    content.Subject,
                    content.BodyText,
                    content.CtaLabel,
                    content.CtaUrl,
                    audienceType,
                    landingPageId,
                    productId,
                    scheduledAt,
                    now);

                await _campaignRepository.AddAsync(campaign, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }

            // Immediate send — behavior unchanged from before scheduling existed.
            var emails = (await _audienceService.GetAudienceEmailsAsync(
                audienceType, landingPageId, productId, context.CreatorId, ct))
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
                    $"Sending to this audience ({emails.Count} recipients) would exceed your monthly email limit. {remaining} send(s) remaining this month.");
            }

            // Snapshot the content into the send record — a later template edit or archive must never
            // change what this send says it contained.
            campaign = Campaign.CreateQueued(
                context.CreatorId,
                template?.Id,
                content.Subject,
                content.BodyText,
                content.CtaLabel,
                content.CtaUrl,
                audienceType,
                landingPageId,
                productId,
                emails.Count,
                now);

            await _campaignRepository.AddAsync(campaign, ct);

            // Flush now so the campaign gets its DB-generated Id — recipients need it as their FK, and it
            // doesn't exist client-side before this row lands.
            await _unitOfWork.SaveChangesAsync(ct);

            await CreateRecipientsAndQueueOutboxAsync(campaign, emails, context, now, ct);
        }, ct);

        return await BuildDetailDtoAsync(campaign!, ct);
    }

    public async Task DispatchScheduledAsync(Guid campaignPublicId, CancellationToken ct)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var campaign = await _campaignRepository.GetByPublicIdForUpdateAsync(campaignPublicId, ct);
            if (campaign is null)
            {
                _logger.LogInformation(
                    "Scheduled campaign {CampaignPublicId} not found; skipping dispatch.", campaignPublicId);
                return;
            }

            await _unitOfWork.AcquireCreatorCampaignLockAsync(campaign.CreatorId, ct);

            // Re-check strictly AFTER acquiring the lock — another dispatch attempt or a creator-initiated
            // Cancel could have already transitioned this row while we waited for the lock. A plain
            // re-query would return the already-tracked instance unchanged (EF's identity map), so this
            // needs an explicit reload from the database.
            await _unitOfWork.ReloadAsync(campaign, ct);

            if (campaign.Status != CampaignStatus.Scheduled)
            {
                _logger.LogInformation(
                    "Scheduled campaign {CampaignPublicId} is already {Status} (dispatched or cancelled by another process); skipping.",
                    campaignPublicId, campaign.Status);
                return;
            }

            var context = await _creatorContextProvider.GetByCreatorIdAsync(campaign.CreatorId, ct);
            if (context is null)
            {
                campaign.MarkFailed(Truncate("Creator workspace no longer exists."), DateTimeOffset.UtcNow);
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }

            try
            {
                await ExecuteSendPipelineAsync(campaign, context, ct);
            }
            catch (Exception exception)
            {
                // The poller must keep going after one bad row — never rethrow. Audience-empty and
                // limit-exceeded (the two realistic failure modes here) are both detected before any
                // recipient/outbox row is written, so no partial rows land alongside this Failed status.
                _logger.LogWarning(
                    exception, "Scheduled campaign {CampaignPublicId} dispatch failed; marking Failed.", campaignPublicId);
                campaign.MarkFailed(Truncate(exception.Message), DateTimeOffset.UtcNow);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }, ct);
    }

    public async Task<CampaignDetailDto> CancelScheduledAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        Campaign? campaign = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _unitOfWork.AcquireCreatorCampaignLockAsync(context.CreatorId, ct);

            campaign = await _campaignRepository.GetByPublicIdForUpdateAsync(campaignPublicId, ct);
            if (campaign is null || campaign.CreatorId != context.CreatorId)
                throw new NotFoundException("Campaign not found.");

            if (campaign.Status != CampaignStatus.Scheduled)
                throw new ConflictException($"Cannot cancel a {campaign.Status} campaign — only Scheduled campaigns can be cancelled.");

            campaign.Cancel(DateTimeOffset.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return await BuildDetailDtoAsync(campaign!, ct);
    }

    public async Task<ResendFailedResultDto> ResendFailedAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var campaign = await _campaignRepository.GetByPublicIdAsync(campaignPublicId, ct);
        if (campaign is null || campaign.CreatorId != context.CreatorId)
            throw new NotFoundException("Campaign not found.");

        int requeuedCount = 0;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            requeuedCount = await _progressProvider.RequeueFailedAsync(campaignPublicId, DateTimeOffset.UtcNow, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return new ResendFailedResultDto(requeuedCount);
    }

    /// <summary>Dispatch-only: resolves the audience for real, consumes the usage limit, transitions
    /// Scheduled → Queued, then hands off to the same recipients/outbox tail the immediate-send path
    /// uses. Never called for immediate sends — that path already has its recipientCount and creates the
    /// campaign directly Queued (see <see cref="SendAsync"/>).</summary>
    private async Task ExecuteSendPipelineAsync(Campaign campaign, MarketingCreatorContext context, CancellationToken ct)
    {
        var emails = (await _audienceService.GetAudienceEmailsAsync(
            campaign.AudienceType, campaign.LandingPageId, campaign.ProductId, campaign.CreatorId, ct))
            .Distinct()
            .ToList();

        if (emails.Count == 0)
            throw new BadRequestException("No recipients.");

        var monthlyLimit = await _creatorContextProvider.GetActivePlanLimitAsync(campaign.CreatorId, MonthlyEmailSendsLimitKey, ct);
        if (monthlyLimit is null)
            throw new ConflictException("Sending is temporarily unavailable.");

        var consumed = await _usageService.TryConsumeAsync(
            campaign.CreatorId, MonthlyEmailSendsLimitKey, emails.Count, monthlyLimit.Value, UsagePeriod.CalendarMonth, ct);
        if (!consumed)
        {
            var usedThisMonth = await _usageService.GetUsedAsync(campaign.CreatorId, MonthlyEmailSendsLimitKey, UsagePeriod.CalendarMonth, ct);
            var remaining = monthlyLimit.Value < 0 ? int.MaxValue : Math.Max(0, monthlyLimit.Value - usedThisMonth);
            throw new ConflictException(
                $"Sending to this audience ({emails.Count} recipients) would exceed your monthly email limit. {remaining} send(s) remaining this month.");
        }

        var now = DateTimeOffset.UtcNow;
        campaign.MarkQueuedFromSchedule(emails.Count, now);
        await _unitOfWork.SaveChangesAsync(ct);

        await CreateRecipientsAndQueueOutboxAsync(campaign, emails, context, now, ct);
    }

    /// <summary>The recipients → renderer → outbox fan-out tail, identical for an immediate send and a
    /// scheduled dispatch — the only genuinely shared core between the two paths.</summary>
    private async Task CreateRecipientsAndQueueOutboxAsync(
        Campaign campaign, List<string> emails, MarketingCreatorContext context, DateTimeOffset now, CancellationToken ct)
    {
        var recipients = emails.Select(email => CampaignRecipient.Create(campaign.Id, email, now)).ToList();
        await _recipientRepository.AddRangeAsync(recipients, ct);

        // Flush again so each recipient gets its own DB-generated Id — CorrelationKey embeds that id.
        await _unitOfWork.SaveChangesAsync(ct);

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
            // The link in the body opens the branded page (a GET never unsubscribes, so link scanners can't); the
            // List-Unsubscribe header keeps the RFC 8058 one-click API URL.
            var unsubscribeUrl = _unsubscribeTokenService.BuildUnsubscribePageUrl(context.CreatorId, recipient.Email);
            var oneClickUnsubscribeUrl = _unsubscribeTokenService.BuildUnsubscribeUrl(context.CreatorId, recipient.Email);
            var openPixelUrl = _openTrackingTokenService.BuildPixelUrl(recipient.Id);
            var correlationKey = $"{campaign.PublicId}:{recipient.Id}";

            await _emailOutboxService.QueueCampaignAsync(
                recipient.Email,
                rendered.Subject,
                rendered.HtmlBody
                    .Replace("{{UNSUBSCRIBE_URL}}", unsubscribeUrl)
                    .Replace("{{OPEN_PIXEL_URL}}", openPixelUrl),
                rendered.PlainTextBody.Replace("{{UNSUBSCRIBE_URL}}", unsubscribeUrl),
                replyTo,
                oneClickUnsubscribeUrl,
                correlationKey,
                ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<MarketingCreatorContext> GetCreatorContextAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        return context;
    }

    private sealed record MailContent(string Subject, string BodyText, string? CtaLabel, string? CtaUrl);

    /// <summary>Null when the request carries no inline content at all (template-only send). As soon as any
    /// content field is set the whole set is validated through <see cref="MailContentRules"/> — so a lone
    /// subject, a CTA label without a URL or a non-http(s) URL is a 400 before anything is written.</summary>
    private static MailContent? ResolveInlineContent(SendCampaignRequestDto request)
    {
        var hasContent =
            !string.IsNullOrWhiteSpace(request.Subject) ||
            !string.IsNullOrWhiteSpace(request.BodyText) ||
            !string.IsNullOrWhiteSpace(request.CtaLabel) ||
            !string.IsNullOrWhiteSpace(request.CtaUrl);

        if (!hasContent)
            return null;

        try
        {
            MailContentRules.Validate(request.Subject ?? string.Empty, request.BodyText ?? string.Empty, request.CtaLabel, request.CtaUrl);
        }
        catch (ArgumentException exception)
        {
            throw new BadRequestException(exception.Message);
        }

        return new MailContent(request.Subject!, request.BodyText!, request.CtaLabel, request.CtaUrl);
    }

    private async Task<EmailTemplate> GetActiveOwnedTemplateAsync(int creatorId, Guid templatePublicId, CancellationToken ct)
    {
        var template = await _templateRepository.GetByPublicIdAsync(templatePublicId, ct);
        if (template is null || template.CreatorId != creatorId)
            throw new NotFoundException("Template not found.");

        if (template.Status != EmailTemplateStatus.Active)
            throw new ConflictException("This template is archived and can no longer be sent.");

        return template;
    }

    private static void ValidateScheduledAt(DateTimeOffset scheduledAt, DateTimeOffset now)
    {
        if (scheduledAt < now.AddMinutes(MinScheduleBufferMinutes))
            throw new BadRequestException($"Scheduled time must be at least {MinScheduleBufferMinutes} minutes from now.");

        if (scheduledAt > now.AddDays(MaxScheduleDays))
            throw new BadRequestException($"Scheduled time cannot be more than {MaxScheduleDays} days from now.");
    }

    private static string? Truncate(string? note)
        => note is { Length: > MaxNoteLength } ? note[..MaxNoteLength] : note;

    private static CampaignAudienceType ParseAudienceType(string value)
    {
        if (!Enum.TryParse<CampaignAudienceType>(value, ignoreCase: true, out var audienceType))
            throw new BadRequestException($"Invalid audience type. Valid values: {string.Join(", ", Enum.GetNames<CampaignAudienceType>())}.");

        return audienceType;
    }

    private async Task<(int? LandingPageId, int? ProductId)> ResolveTargetAsync(
        int creatorId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct)
    {
        if (audienceType == CampaignAudienceType.All)
        {
            if (targetPublicId != Guid.Empty)
                throw new BadRequestException("The All audience does not take a target.");

            return (null, null);
        }

        if (targetPublicId == Guid.Empty)
            throw new BadRequestException("A target is required for this audience.");

        if (audienceType == CampaignAudienceType.LandingPage)
        {
            var landingPageId = await _creatorContextProvider.ResolveLandingPageIdAsync(creatorId, targetPublicId, ct);
            if (landingPageId is null)
                throw new NotFoundException("Landing page not found.");

            return (landingPageId, null);
        }

        var productId = await _creatorContextProvider.ResolveProductIdAsync(creatorId, targetPublicId, ct);
        if (productId is null)
            throw new NotFoundException("Product not found.");

        return (null, productId);
    }

    private async Task<CampaignDetailDto> BuildDetailDtoAsync(Campaign campaign, CancellationToken ct)
    {
        var targetPublicId = campaign.LandingPageId is not null
            ? (await _creatorContextProvider.GetLandingPagePublicIdsAsync([campaign.LandingPageId.Value], ct))[campaign.LandingPageId.Value]
            : campaign.ProductId is not null
                ? (await _creatorContextProvider.GetProductPublicIdsAsync([campaign.ProductId.Value], ct))[campaign.ProductId.Value]
                : (Guid?)null;

        var progress = (await _progressProvider.GetProgressAsync([campaign.PublicId], ct))[campaign.PublicId];

        return new CampaignDetailDto(
            campaign.PublicId,
            campaign.Subject,
            campaign.BodyText,
            campaign.CtaLabel,
            campaign.CtaUrl,
            campaign.AudienceType.ToString(),
            targetPublicId,
            campaign.Status.ToString(),
            campaign.RecipientCount,
            campaign.QueuedAt,
            campaign.ScheduledAt,
            campaign.Note,
            campaign.CreatedAt,
            campaign.UpdatedAt,
            progress.SentCount,
            progress.FailedCount,
            campaign.UniqueOpenCount);
    }
}
