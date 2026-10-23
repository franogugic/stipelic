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
    private readonly CampaignAudienceTargets _targets;
    private readonly CampaignDetailBuilder _detailBuilder;

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
        _targets = new CampaignAudienceTargets(creatorContextProvider, campaignRepository);
        _detailBuilder = new CampaignDetailBuilder(creatorContextProvider, campaignRepository, audienceService, progressProvider);
    }

    public async Task<AudiencePreviewDto> GetAudiencePreviewAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var target = await ResolveTargetAsync(context.CreatorId, audienceType, targetPublicId, ct);

        var recipientCount = await _audienceService.GetAudienceCountAsync(
            audienceType, target.LandingPageId, target.ProductId, target.SourceCampaignId, context.CreatorId, ct);

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

        var target = await ResolveTargetAsync(context.CreatorId, audienceType, targetPublicId, ct);

        var clampedLimit = limit <= 0 ? DefaultRecipientsPageLimit : Math.Min(limit, MaxRecipientsPageLimit);

        var (emails, hasMore) = await _audienceService.GetAudiencePageAsync(
            audienceType, target.LandingPageId, target.ProductId, target.SourceCampaignId, context.CreatorId,
            afterEmail, clampedLimit, ct);

        return new AudienceRecipientsPageDto(emails, hasMore);
    }

    public async Task<CampaignAudiencesDto> GetAudiencesAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        return await _audienceService.GetAudienceOverviewAsync(context.CreatorId, ct);
    }

    public async Task<List<CampaignListItemDto>> ListAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var campaigns = await _campaignRepository.GetRecentByCreatorIdAsync(context.CreatorId, ListPageSize, ct);

        return await _detailBuilder.BuildListAsync(campaigns, ct);
    }

    public async Task<CampaignDetailDto> GetAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var campaign = await _campaignRepository.GetByPublicIdAsync(campaignPublicId, ct);
        if (campaign is null || campaign.CreatorId != context.CreatorId)
            throw new NotFoundException("Campaign not found.");

        return await _detailBuilder.BuildDetailAsync(campaign, ct);
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

    /// <summary>A non-opener preview needs a sent original (409 with the reason code otherwise); its timing and slot
    /// rules only apply to the send itself.</summary>
    private async Task<ResolvedAudienceTarget> ResolveTargetAsync(
        int creatorId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct)
    {
        var target = await _targets.ResolveAsync(creatorId, audienceType, targetPublicId, ct);
        if (target.Source is { } source)
            CampaignAudienceTargets.EnsureResendableSource(source);

        return target;
    }
}
