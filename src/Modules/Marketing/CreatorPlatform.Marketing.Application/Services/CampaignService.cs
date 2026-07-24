using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class CampaignService : ICampaignService
{
    private const string MonthlyEmailSendsLimitKey = "max_email_sends_per_month";
    private const int ListPageSize = 50;
    private const int DefaultRecipientsPageLimit = 50;
    private const int MaxRecipientsPageLimit = 100;

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IAudienceService _audienceService;
    private readonly ICreatorUsageService _usageService;
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICampaignProgressProvider _progressProvider;

    public CampaignService(
        ICreatorContextProvider creatorContextProvider,
        IAudienceService audienceService,
        ICreatorUsageService usageService,
        ICampaignRepository campaignRepository,
        ICampaignProgressProvider progressProvider)
    {
        _creatorContextProvider = creatorContextProvider;
        _audienceService = audienceService;
        _usageService = usageService;
        _campaignRepository = campaignRepository;
        _progressProvider = progressProvider;
    }

    public async Task<AudiencePreviewDto> GetAudiencePreviewAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var (landingPageId, productId) = await ResolveTargetAsync(context.CreatorId, audienceType, targetPublicId, ct);

        var recipientCount = await _audienceService.GetAudienceCountAsync(
            audienceType, landingPageId, productId, context.CreatorId, ct);

        var monthlyLimit = await _creatorContextProvider.GetActivePlanLimitAsync(
            context.CreatorId, MonthlyEmailSendsLimitKey, ct) ?? 0;
        var usedThisMonth = await _usageService.GetUsedAsync(
            context.CreatorId, MonthlyEmailSendsLimitKey, UsagePeriod.CalendarMonth, ct);
        var remaining = monthlyLimit < 0 ? int.MaxValue : Math.Max(0, monthlyLimit - usedThisMonth);

        return new AudiencePreviewDto(recipientCount, monthlyLimit, usedThisMonth, remaining);
    }

    public async Task<AudienceRecipientsPageDto> GetAudienceRecipientsAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId,
        string? afterEmail, int limit, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var (landingPageId, productId) = await ResolveTargetAsync(context.CreatorId, audienceType, targetPublicId, ct);

        var clampedLimit = limit <= 0 ? DefaultRecipientsPageLimit : Math.Min(limit, MaxRecipientsPageLimit);

        var (emails, hasMore) = await _audienceService.GetAudiencePageAsync(
            audienceType, landingPageId, productId, context.CreatorId, afterEmail, clampedLimit, ct);

        return new AudienceRecipientsPageDto(emails, hasMore);
    }

    public async Task<List<CampaignListItemDto>> ListAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var campaigns = await _campaignRepository.GetRecentByCreatorIdAsync(context.CreatorId, ListPageSize, ct);
        if (campaigns.Count == 0)
            return [];

        var landingPageIds = campaigns.Where(c => c.LandingPageId is not null).Select(c => c.LandingPageId!.Value).Distinct().ToList();
        var productIds = campaigns.Where(c => c.ProductId is not null).Select(c => c.ProductId!.Value).Distinct().ToList();

        var landingPagePublicIds = await _creatorContextProvider.GetLandingPagePublicIdsAsync(landingPageIds, ct);
        var productPublicIds = await _creatorContextProvider.GetProductPublicIdsAsync(productIds, ct);
        var progress = await _progressProvider.GetProgressAsync(campaigns.Select(c => c.PublicId).ToList(), ct);

        return campaigns
            .Select(c => ToListItemDto(c, landingPagePublicIds, productPublicIds, progress))
            .ToList();
    }

    public async Task<CampaignDetailDto> GetAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var campaign = await _campaignRepository.GetByPublicIdAsync(campaignPublicId, ct);
        if (campaign is null || campaign.CreatorId != context.CreatorId)
            throw new NotFoundException("Campaign not found.");

        var targetPublicId = await ResolveTargetPublicIdAsync(campaign, ct);
        var progress = await _progressProvider.GetProgressAsync([campaign.PublicId], ct);

        return ToDetailDto(campaign, targetPublicId, progress[campaign.PublicId]);
    }

    public async Task<List<FailedRecipientDto>> GetFailedRecipientsAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var campaign = await _campaignRepository.GetByPublicIdAsync(campaignPublicId, ct);
        if (campaign is null || campaign.CreatorId != context.CreatorId)
            throw new NotFoundException("Campaign not found.");

        return await _progressProvider.GetFailedRecipientsAsync(campaignPublicId, ct);
    }

    private async Task<MarketingCreatorContext> GetCreatorContextAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

        return context;
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

    private async Task<Guid> ResolveTargetPublicIdAsync(Campaign campaign, CancellationToken ct)
    {
        if (campaign.LandingPageId is not null)
        {
            var map = await _creatorContextProvider.GetLandingPagePublicIdsAsync([campaign.LandingPageId.Value], ct);
            return map[campaign.LandingPageId.Value];
        }

        var productMap = await _creatorContextProvider.GetProductPublicIdsAsync([campaign.ProductId!.Value], ct);
        return productMap[campaign.ProductId.Value];
    }

    private static CampaignListItemDto ToListItemDto(
        Campaign campaign,
        Dictionary<int, Guid> landingPagePublicIds,
        Dictionary<int, Guid> productPublicIds,
        Dictionary<Guid, CampaignProgressDto> progress)
    {
        var targetPublicId = campaign.LandingPageId is not null
            ? landingPagePublicIds[campaign.LandingPageId.Value]
            : productPublicIds[campaign.ProductId!.Value];
        var campaignProgress = progress[campaign.PublicId];

        return new CampaignListItemDto(
            campaign.PublicId,
            campaign.Subject,
            campaign.Status.ToString(),
            campaign.AudienceType.ToString(),
            targetPublicId,
            campaign.RecipientCount,
            campaign.QueuedAt,
            campaign.CreatedAt,
            campaignProgress.SentCount,
            campaignProgress.FailedCount);
    }

    private static CampaignDetailDto ToDetailDto(Campaign campaign, Guid targetPublicId, CampaignProgressDto progress)
    {
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
            campaign.CreatedAt,
            campaign.UpdatedAt,
            progress.SentCount,
            progress.FailedCount);
    }
}
