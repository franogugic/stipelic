using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>Builds the campaign detail and list DTOs, resend fields included — shared by the read side and by the
/// send paths, which answer with the campaign they created or changed.</summary>
internal sealed class CampaignDetailBuilder
{
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ICampaignRepository _campaignRepository;
    private readonly IAudienceService _audienceService;
    private readonly ICampaignProgressProvider _progressProvider;

    public CampaignDetailBuilder(
        ICreatorContextProvider creatorContextProvider,
        ICampaignRepository campaignRepository,
        IAudienceService audienceService,
        ICampaignProgressProvider progressProvider)
    {
        _creatorContextProvider = creatorContextProvider;
        _campaignRepository = campaignRepository;
        _audienceService = audienceService;
        _progressProvider = progressProvider;
    }

    public async Task<CampaignDetailDto> BuildDetailAsync(Campaign campaign, CancellationToken ct)
    {
        var targetPublicId = await ResolveTargetPublicIdAsync(campaign, ct);
        var resends = await LoadResendsAsync(campaign, ct);

        // One progress query for the campaign and all its resends.
        var progress = await _progressProvider.GetProgressAsync(
            resends.Select(r => r.PublicId).Prepend(campaign.PublicId).ToList(), ct);

        var source = campaign.SourceCampaignId is { } sourceId
            ? (await _campaignRepository.GetSourceSummariesAsync([sourceId], ct)).GetValueOrDefault(sourceId)
            : null;

        var nonOpenerResend = await BuildNonOpenerResendAsync(campaign, resends, progress, ct);
        var own = progress[campaign.PublicId];

        return new CampaignDetailDto(
            campaign.PublicId,
            campaign.Subject,
            campaign.BodyText,
            campaign.CtaLabel,
            campaign.CtaUrl,
            campaign.AudienceType.ToString(),
            targetPublicId ?? source?.PublicId,
            campaign.Status.ToString(),
            campaign.RecipientCount,
            campaign.QueuedAt,
            campaign.ScheduledAt,
            campaign.Note,
            campaign.CreatedAt,
            campaign.UpdatedAt,
            own.SentCount,
            own.FailedCount,
            campaign.UniqueOpenCount,
            source?.PublicId,
            campaign.ResendRound,
            source is null ? null : new CampaignSourceDto(source.PublicId, source.Subject),
            nonOpenerResend);
    }

    public async Task<List<CampaignListItemDto>> BuildListAsync(IReadOnlyList<Campaign> campaigns, CancellationToken ct)
    {
        if (campaigns.Count == 0)
            return [];

        var landingPageIds = campaigns.Where(c => c.LandingPageId is not null).Select(c => c.LandingPageId!.Value).Distinct().ToList();
        var productIds = campaigns.Where(c => c.ProductId is not null).Select(c => c.ProductId!.Value).Distinct().ToList();
        var sourceIds = campaigns.Where(c => c.SourceCampaignId is not null).Select(c => c.SourceCampaignId!.Value).Distinct().ToList();

        var landingPagePublicIds = await _creatorContextProvider.GetLandingPagePublicIdsAsync(landingPageIds, ct);
        var productPublicIds = await _creatorContextProvider.GetProductPublicIdsAsync(productIds, ct);
        var sources = sourceIds.Count == 0 ? [] : await _campaignRepository.GetSourceSummariesAsync(sourceIds, ct);
        var progress = await _progressProvider.GetProgressAsync(campaigns.Select(c => c.PublicId).ToList(), ct);

        return campaigns.Select(campaign =>
        {
            var targetPublicId = campaign.LandingPageId is not null
                ? landingPagePublicIds[campaign.LandingPageId.Value]
                : campaign.ProductId is not null
                    ? productPublicIds[campaign.ProductId.Value]
                    : (Guid?)null;
            var source = campaign.SourceCampaignId is { } sourceId ? sources.GetValueOrDefault(sourceId) : null;
            var campaignProgress = progress[campaign.PublicId];

            return new CampaignListItemDto(
                campaign.PublicId,
                campaign.Subject,
                campaign.Status.ToString(),
                campaign.AudienceType.ToString(),
                targetPublicId ?? source?.PublicId,
                campaign.RecipientCount,
                campaign.QueuedAt,
                campaign.ScheduledAt,
                campaign.Note,
                campaign.CreatedAt,
                campaignProgress.SentCount,
                campaignProgress.FailedCount,
                campaign.UniqueOpenCount,
                source?.PublicId,
                campaign.ResendRound,
                source is null ? null : new CampaignSourceDto(source.PublicId, source.Subject));
        }).ToList();
    }

    /// <summary>The resends hang off an original only; a resend or an unsent campaign has none to show.</summary>
    private async Task<List<Campaign>> LoadResendsAsync(Campaign campaign, CancellationToken ct) =>
        NonOpenerResendPolicy.SourceReason(campaign) is null
            ? await _campaignRepository.GetResendsAsync(campaign.Id, ct)
            : [];

    private async Task<NonOpenerResendDto> BuildNonOpenerResendAsync(
        Campaign campaign,
        List<Campaign> resends,
        Dictionary<Guid, CampaignProgressDto> progress,
        CancellationToken ct)
    {
        if (NonOpenerResendPolicy.SourceReason(campaign) is { } sourceReason)
            return new NonOpenerResendDto(false, sourceReason, 0, null, null, 0, Campaign.MaxNonOpenerResends, 0, 0, []);

        var recipientCount = await _audienceService.GetAudienceCountAsync(
            CampaignAudienceType.CampaignNonOpeners, null, null, campaign.Id, campaign.CreatorId, ct);
        var decision = NonOpenerResendPolicy.Evaluate(campaign, resends, recipientCount, DateTimeOffset.UtcNow);
        var (openedAny, openedThroughResends) = await _audienceService.GetNonOpenerReachAsync(campaign.Id, ct);

        var rows = resends
            .Select(r => new NonOpenerResendRowDto(
                r.PublicId,
                r.ResendRound!.Value,
                r.Status.ToString(),
                r.Subject,
                r.QueuedAt,
                r.ScheduledAt,
                r.RecipientCount,
                progress[r.PublicId].SentCount,
                r.UniqueOpenCount))
            .ToList();

        return new NonOpenerResendDto(
            decision.Eligible,
            decision.Reason,
            recipientCount,
            decision.EligibleFrom,
            decision.EligibleUntil,
            decision.Used,
            Campaign.MaxNonOpenerResends,
            openedAny,
            openedThroughResends,
            rows);
    }

    private async Task<Guid?> ResolveTargetPublicIdAsync(Campaign campaign, CancellationToken ct)
    {
        if (campaign.LandingPageId is { } landingPageId)
            return (await _creatorContextProvider.GetLandingPagePublicIdsAsync([landingPageId], ct))[landingPageId];

        if (campaign.ProductId is { } productId)
            return (await _creatorContextProvider.GetProductPublicIdsAsync([productId], ct))[productId];

        // All targets nothing; a resend's target is its original, filled in from Source by the callers.
        return null;
    }
}
