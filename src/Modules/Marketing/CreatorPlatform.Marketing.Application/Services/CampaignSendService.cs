using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Templates;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class CampaignSendService : ICampaignSendService
{
    private const string MonthlyEmailSendsLimitKey = "max_email_sends_per_month";

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IEmailTemplateRepository _templateRepository;
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
        IEmailTemplateRepository templateRepository,
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
        _templateRepository = templateRepository;
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

    public async Task<CampaignDetailDto> SendAsync(string slug, int ownerUserId, SendCampaignRequestDto request, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        var audienceType = ParseAudienceType(request.AudienceType);
        var templatePublicId = request.TemplatePublicId;
        var targetPublicId = request.TargetPublicId;

        Campaign? campaign = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Lock first — audience resolution + usage-counter consumption must not race across two
            // concurrent sends for the same creator.
            await _unitOfWork.AcquireCreatorCampaignLockAsync(context.CreatorId, ct);

            var template = await _templateRepository.GetByPublicIdAsync(templatePublicId, ct);
            if (template is null || template.CreatorId != context.CreatorId)
                throw new NotFoundException("Template not found.");

            if (template.Status != EmailTemplateStatus.Active)
                throw new ConflictException("This template is archived and can no longer be sent.");

            var (landingPageId, productId) = await ResolveTargetAsync(context.CreatorId, audienceType, targetPublicId, ct);

            // Deduped/suppressed at the SQL layer already (see AudienceService), but the CampaignRecipient
            // unique index is (CampaignId, Email) — deduping again here is cheap insurance against ever
            // inserting the same recipient twice for one send, regardless of the audience source.
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

            var now = DateTimeOffset.UtcNow;

            // Snapshot the template's current content into the send record — a later template edit or
            // archive must never change what this send says it contained.
            campaign = Campaign.CreateQueuedFromTemplate(
                context.CreatorId,
                template.Id,
                template.Subject,
                template.BodyText,
                template.CtaLabel,
                template.CtaUrl,
                audienceType,
                landingPageId,
                productId,
                emails.Count,
                now);

            await _campaignRepository.AddAsync(campaign, ct);

            // Flush now so the campaign gets its DB-generated Id — recipients need it as their FK, and it
            // doesn't exist client-side before this row lands.
            await _unitOfWork.SaveChangesAsync(ct);

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
        var targetPublicIdResolved = sent.LandingPageId is not null
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
            targetPublicIdResolved,
            sent.Status.ToString(),
            sent.RecipientCount,
            sent.QueuedAt,
            sent.CreatedAt,
            sent.UpdatedAt,
            progress.SentCount,
            progress.FailedCount);
    }

    private static CampaignAudienceType ParseAudienceType(string value)
    {
        if (!Enum.TryParse<CampaignAudienceType>(value, ignoreCase: true, out var audienceType))
            throw new BadRequestException($"Invalid audience type. Valid values: {string.Join(", ", Enum.GetNames<CampaignAudienceType>())}.");

        return audienceType;
    }

    private async Task<(int? LandingPageId, int? ProductId)> ResolveTargetAsync(
        int creatorId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct)
    {
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
}
