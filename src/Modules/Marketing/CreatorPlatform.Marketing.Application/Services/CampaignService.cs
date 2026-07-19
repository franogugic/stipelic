using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class CampaignService : ICampaignService
{
    private const string MonthlyEmailSendsLimitKey = "max_email_sends_per_month";

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IAudienceService _audienceService;
    private readonly ICreatorUsageService _usageService;

    public CampaignService(
        ICreatorContextProvider creatorContextProvider,
        IAudienceService audienceService,
        ICreatorUsageService usageService)
    {
        _creatorContextProvider = creatorContextProvider;
        _audienceService = audienceService;
        _usageService = usageService;
    }

    public async Task<AudiencePreviewDto> GetAudiencePreviewAsync(
        string slug, int ownerUserId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");

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
