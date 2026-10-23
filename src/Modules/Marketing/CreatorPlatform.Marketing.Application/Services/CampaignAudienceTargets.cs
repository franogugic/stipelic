using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

/// <summary>An audience's target, resolved to internal ids after the ownership check. <see cref="Source"/> is the
/// original campaign of a <see cref="CampaignAudienceType.CampaignNonOpeners"/> audience.</summary>
public sealed record ResolvedAudienceTarget(int? LandingPageId, int? ProductId, Campaign? Source)
{
    public int? SourceCampaignId => Source?.Id;
}

/// <summary>Resolves the <c>TargetPublicId</c> of a preview, recipients page or send to the creator's own landing
/// page, product or campaign — anything else is a 404, so another creator's ids never resolve.</summary>
internal sealed class CampaignAudienceTargets
{
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ICampaignRepository _campaignRepository;

    public CampaignAudienceTargets(ICreatorContextProvider creatorContextProvider, ICampaignRepository campaignRepository)
    {
        _creatorContextProvider = creatorContextProvider;
        _campaignRepository = campaignRepository;
    }

    public async Task<ResolvedAudienceTarget> ResolveAsync(
        int creatorId, CampaignAudienceType audienceType, Guid targetPublicId, CancellationToken ct)
    {
        if (audienceType == CampaignAudienceType.All)
        {
            if (targetPublicId != Guid.Empty)
                throw new BadRequestException("The All audience does not take a target.");

            return new ResolvedAudienceTarget(null, null, null);
        }

        if (targetPublicId == Guid.Empty)
            throw new BadRequestException("A target is required for this audience.");

        switch (audienceType)
        {
            case CampaignAudienceType.LandingPage:
                var landingPageId = await _creatorContextProvider.ResolveLandingPageIdAsync(creatorId, targetPublicId, ct)
                    ?? throw new NotFoundException("Landing page not found.");
                return new ResolvedAudienceTarget(landingPageId, null, null);

            case CampaignAudienceType.Product:
                var productId = await _creatorContextProvider.ResolveProductIdAsync(creatorId, targetPublicId, ct)
                    ?? throw new NotFoundException("Product not found.");
                return new ResolvedAudienceTarget(null, productId, null);

            case CampaignAudienceType.CampaignNonOpeners:
                var source = await _campaignRepository.GetByPublicIdAsync(targetPublicId, ct);
                if (source is null || source.CreatorId != creatorId)
                    throw new NotFoundException("Campaign not found.");
                return new ResolvedAudienceTarget(null, null, source);

            default:
                throw new BadRequestException("Invalid audience type.");
        }
    }

    /// <summary>A non-opener audience needs a sent original: 409 with the reason code otherwise.</summary>
    public static void EnsureResendableSource(Campaign source)
    {
        if (NonOpenerResendPolicy.SourceReason(source) is { } reason)
            throw new ConflictException(NonOpenerResendReasons.Message(reason), reason);
    }
}
